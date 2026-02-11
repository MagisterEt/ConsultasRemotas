"""Módulo de Consultas Multi-Servidor"""
import pyodbc
import pandas as pd
from concurrent.futures import ThreadPoolExecutor, as_completed
from time import perf_counter
import logging
from config import get_connection_string, ENTIDADES_POR_SERVIDOR, SERVIDORES
from formatador import converter_para_json

logger = logging.getLogger(__name__)
TIMEOUT_GLOBAL = 300  # 5 minutos


class ConsultaMultiServidor:
    """Executa consultas simultâneas em múltiplos servidores SQL"""
    
    def __init__(self):
        self.servidores = SERVIDORES
        self.entidades_por_servidor = ENTIDADES_POR_SERVIDOR

    def _executar_query(self, servidor: str, query: str, database: str = "AASI", 
                        log_callback=None) -> tuple:
        """Executa query em um servidor com timeout"""
        try:
            if log_callback:
                log_callback(f"📌 Conectando {servidor}...")
            
            conn_str = get_connection_string(servidor, database)
            
            # Timeout de conexão: 60s, timeout de query: 180s (3 min)
            with pyodbc.connect(conn_str, timeout=60) as conn:
                conn.timeout = 180  # Timeout para execução de query
                
                cursor = conn.cursor()
                cursor.execute(query)
                
                # Ler colunas
                colunas = [col[0] for col in cursor.description] if cursor.description else []
                
                # Ler dados
                rows = cursor.fetchall()
                df = pd.DataFrame.from_records(rows, columns=colunas)
                
                cursor.close()
                
                if log_callback:
                    log_callback(f"✅ {servidor}: {len(df)} linhas")
                
                return (servidor, True, df, None)
        
        except pyodbc.OperationalError as e:
            msg = "Timeout" if "timeout" in str(e).lower() else str(e)[:50]
            if log_callback:
                log_callback(f"⏱️ {servidor}: {msg}")
            return (servidor, False, pd.DataFrame(), msg)
        
        except Exception as e:
            if log_callback:
                log_callback(f"❌ {servidor}: {str(e)[:50]}")
            logger.error(f"❌ {servidor}: {e}")
            return (servidor, False, pd.DataFrame(), str(e))

    def executar_consulta_simultanea(self, query: str, servidores: list = None,
                                      config_consulta: dict = None, 
                                      log_callback=None, cancelado_callback=None) -> dict:
        """Executa query em múltiplos servidores"""
        inicio = perf_counter()
        servidores = servidores or self.servidores
        
        # Preparar queries por servidor
        queries = self._preparar_queries(query, config_consulta)
        servidores = list(queries.keys()) if queries else servidores
        
        if log_callback:
            log_callback(f"🔍 Consultando {len(servidores)} servidores...")
        
        resultados, erros = [], []
        servidores_ok, servidores_timeout, servidores_erro = 0, [], []
        
        with ThreadPoolExecutor(max_workers=len(servidores)) as executor:
            futures = {
                executor.submit(self._executar_query, srv, queries.get(srv, query), 
                              "AASI", log_callback): srv
                for srv in servidores if srv in queries
            }
            
            try:
                for future in as_completed(futures, timeout=TIMEOUT_GLOBAL):
                    # Verificar cancelamento
                    if cancelado_callback and cancelado_callback():
                        if log_callback:
                            log_callback("⛔ Cancelando consultas pendentes...")
                        for f in futures:
                            f.cancel()
                        break
                    
                    servidor = futures[future]
                    try:
                        _, sucesso, df, erro = future.result(timeout=5)
                        if sucesso and not df.empty:
                            resultados.append(df)
                            servidores_ok += 1
                        elif erro:
                            erros.append(f"{servidor}: {erro}")
                            servidores_erro.append(servidor)
                    except Exception as e:
                        erros.append(f"{servidor}: {str(e)}")
                        servidores_timeout.append(servidor)
                        
            except TimeoutError:
                if log_callback:
                    log_callback(f"⏱️ Timeout global ({TIMEOUT_GLOBAL}s)")
                for f in futures:
                    if not f.done():
                        servidores_timeout.append(futures[f])
                        f.cancel()
        
        # Consolidar
        df_final = pd.concat(resultados, ignore_index=True) if resultados else pd.DataFrame()
        
        if config_consulta and config_consulta.get('subcontas_por_entidade') and not df_final.empty:
            df_final = df_final.drop_duplicates()
        
        tempo = perf_counter() - inicio
        
        if log_callback:
            log_callback(f"✅ Concluído: {len(df_final)} linhas em {tempo:.2f}s")
        
        return self._formatar_resposta(df_final, servidores_ok, len(servidores),
                                       servidores_timeout, servidores_erro, erros, tempo)

    def _preparar_queries(self, query_template: str, config: dict) -> dict:
        """Prepara queries específicas por servidor"""
        if not config:
            return {srv: query_template for srv in self.servidores}
        
        queries = {}
        entidades_config = config.get('entidades_por_servidor', self.entidades_por_servidor)
        subcontas = config.get('subcontas_por_entidade', {})
        
        for servidor, entidades in entidades_config.items():
            # Gerar SET @Entidade
            ent_set = ""
            for i in range(8):
                val = f"'{entidades[i]}'" if i < len(entidades) else "NULL"
                ent_set += f"    SET @Entidade{i+1} = {val}\n"
            
            query = query_template.replace('{entidades}', ent_set)
            query = query.replace('{entidades_set}', ent_set)
            
            # Subcontas
            if subcontas:
                subs = set()
                for ent in entidades:
                    subs.update(subcontas.get(ent, []))
                query = query.replace('{subcontas}', ",".join(f"'{s}'" for s in subs) or "''")
            
            queries[servidor] = query
        
        return queries

    def _formatar_resposta(self, df: pd.DataFrame, ok: int, total: int,
                           timeout: list, erro: list, avisos: list, tempo: float) -> dict:
        """Formata resposta padronizada"""
        if df.empty:
            return {
                'status': 'erro' if ok == 0 else 'aviso',
                'dados': [], 'colunas': [], 'linhas_afetadas': 0,
                'servidores_processados': ok, 'servidores_total': total,
                'tempo_total': round(tempo, 2),
                'mensagem': f"0 linhas - {ok} ok, {len(timeout)} timeout, {len(erro)} erro",
                'dataframe': df, 'avisos': avisos or None
            }
        
        # Formatar dados (datas pt-BR, números com 2 casas, colunas em português)
        dados, colunas = converter_para_json(df)
        
        return {
            'status': 'sucesso',
            'dados': dados, 'colunas': colunas,
            'linhas_afetadas': len(df),
            'servidores_processados': ok, 'servidores_total': total,
            'tempo_total': round(tempo, 2),
            'mensagem': f"{len(df)} linhas de {ok}/{total} servidores em {tempo:.2f}s",
            'dataframe': df, 'avisos': avisos or None
        }