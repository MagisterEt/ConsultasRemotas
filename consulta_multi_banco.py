"""Módulo de Consultas Multi-Banco (APS + AASI)"""
import pyodbc
import pandas as pd
from concurrent.futures import ThreadPoolExecutor, as_completed
from time import perf_counter
import logging
from config import get_connection_string, ENTIDADES_POR_SERVIDOR, SERVIDORES
from formatador import converter_para_json

logger = logging.getLogger(__name__)
TIMEOUT_GLOBAL = 300  # 5 minutos


class ConsultaMultiBanco:
    """Executa consultas simultâneas em múltiplos bancos (APS + AASI)"""
    
    def __init__(self):
        self.servidores = SERVIDORES
        self.entidades_por_servidor = ENTIDADES_POR_SERVIDOR

    def _executar_query(self, servidor: str, query: str, database: str,
                        log_callback=None) -> tuple:
        """Executa query em um servidor/banco com timeout"""
        try:
            if log_callback:
                log_callback(f"📌 {database} {servidor}...")
            
            conn_str = get_connection_string(servidor, database)
            
            with pyodbc.connect(conn_str, timeout=60) as conn:
                conn.timeout = 180  # 3 minutos para execução
                
                cursor = conn.cursor()
                cursor.execute(query)
                
                colunas = [col[0] for col in cursor.description] if cursor.description else []
                rows = cursor.fetchall()
                df = pd.DataFrame.from_records(rows, columns=colunas)
                cursor.close()
                
                if log_callback:
                    log_callback(f"✅ {database} {servidor}: {len(df)} linhas")
                
                return (servidor, True, df, None)
        
        except pyodbc.OperationalError as e:
            msg = "Timeout" if "timeout" in str(e).lower() else str(e)[:50]
            if log_callback:
                log_callback(f"⏱️ {database} {servidor}: {msg}")
            return (servidor, False, pd.DataFrame(), msg)
        
        except Exception as e:
            if log_callback:
                log_callback(f"❌ {database} {servidor}: {str(e)[:50]}")
            return (servidor, False, pd.DataFrame(), str(e))

    def _gerar_entidades_sql(self, servidor: str) -> str:
        """Gera SET statements para entidades"""
        entidades = self.entidades_por_servidor.get(servidor, [])
        sql = ""
        for i in range(8):
            val = f"'{entidades[i]}'" if i < len(entidades) else "NULL"
            sql += f"    SET @Entidade{i+1} = {val}\n"
        return sql

    def executar_conferencia_13(self, ano: int, periodo: int, config: dict,
                                 cancelado_callback=None, log_callback=None) -> dict:
        """Executa Conferência 13º em Mineiracao_APS + AASI"""
        inicio = perf_counter()
        if log_callback:
            log_callback(f"🔍 Conferência 13º: Ano {ano}, Período {periodo}")
        
        query_aps = config['query_aps'].format(ano=ano, periodo=periodo)
        query_aasi_tpl = config['query_aasi'].replace('{ano}', str(ano)).replace('{periodo}', str(periodo))
        
        resultados_aps, resultados_aasi, erros = [], [], []
        
        with ThreadPoolExecutor(max_workers=16) as executor:
            # Mineiracao_APS: apenas 10.31.11.2
            futures = {
                executor.submit(self._executar_query, '10.31.11.2', query_aps, 'Mineiracao_APS', log_callback): 'Mineiracao_APS'
            }
            
            # AASI: todos os servidores
            for srv in self.servidores:
                query = query_aasi_tpl.replace('{entidades}', self._gerar_entidades_sql(srv))
                futures[executor.submit(self._executar_query, srv, query, 'AASI', log_callback)] = srv
            
            for future in as_completed(futures, timeout=TIMEOUT_GLOBAL):
                # Verificar cancelamento
                if cancelado_callback and cancelado_callback():
                    if log_callback:
                        log_callback("⛔ Cancelando consultas pendentes...")
                    for f in futures:
                        f.cancel()
                    break
                
                origem = futures[future]
                try:
                    _, sucesso, df, erro = future.result(timeout=5)
                    if sucesso and not df.empty:
                        if origem == 'Mineiracao_APS':
                            resultados_aps.append(df)
                        else:
                            resultados_aasi.append(df)
                    elif erro:
                        erros.append(f"{origem}: {erro}")
                except Exception as e:
                    erros.append(f"{origem}: {str(e)}")
        
        # Consolidar
        df_aps = pd.concat(resultados_aps, ignore_index=True) if resultados_aps else pd.DataFrame()
        df_aasi = pd.concat(resultados_aasi, ignore_index=True) if resultados_aasi else pd.DataFrame()
        
        # Merge
        df_final = self._merge_aps_aasi(df_aps, df_aasi)
        tempo = perf_counter() - inicio
        
        # Formatar dados
        dados, colunas = converter_para_json(df_final)
        
        return {
            'status': 'sucesso' if not df_final.empty else 'erro',
            'dados': dados,
            'colunas': colunas,
            'linhas_afetadas': len(df_final),
            'linhas_aps': len(df_aps),
            'linhas_aasi': len(df_aasi),
            'tempo_total': round(tempo, 2),
            'mensagem': f"{len(df_final)} linhas | APS: {len(df_aps)} | AASI: {len(df_aasi)} em {tempo:.2f}s",
            'dataframe': df_final,
            'avisos': erros or None
        }

    def _merge_aps_aasi(self, df_aps: pd.DataFrame, df_aasi: pd.DataFrame) -> pd.DataFrame:
        """Faz merge entre dados APS e AASI"""
        if df_aps.empty and df_aasi.empty:
            return pd.DataFrame()
        
        # Garantir que colunas de merge sejam string
        if not df_aps.empty:
            df_aps['Entidade'] = df_aps['Entidade'].astype(str)
            df_aps['Conta'] = df_aps['Conta'].astype(str)
            df_aps['saldo_totalAPS'] = pd.to_numeric(df_aps['saldo_totalAPS'], errors='coerce').fillna(0)
        if not df_aasi.empty:
            df_aasi['Entidade'] = df_aasi['Entidade'].astype(str)
            df_aasi['Conta'] = df_aasi['Conta'].astype(str)
            df_aasi['saldo_totalAASI'] = pd.to_numeric(df_aasi['saldo_totalAASI'], errors='coerce').fillna(0)
        
        if not df_aps.empty and not df_aasi.empty:
            df = pd.merge(df_aps, df_aasi, on=['Entidade', 'Conta'], 
                         how='outer', suffixes=('_APS', '_AASI'))
            df['saldo_totalAPS'] = pd.to_numeric(df['saldo_totalAPS'], errors='coerce').fillna(0)
            df['saldo_totalAASI'] = pd.to_numeric(df['saldo_totalAASI'], errors='coerce').fillna(0)
            df['Diferenca'] = df['saldo_totalAPS'] - df['saldo_totalAASI']
            return df.sort_values(['Entidade', 'Conta'])
        
        if not df_aps.empty:
            df_aps['saldo_totalAASI'] = 0.0
            df_aps['Diferenca'] = df_aps['saldo_totalAPS']
            return df_aps
        
        df_aasi['saldo_totalAPS'] = 0.0
        df_aasi['Diferenca'] = -df_aasi['saldo_totalAASI']
        return df_aasi

    def executar_conta_verbas_aps(self, ano: int, config: dict, log_callback=None) -> dict:
        """Executa consulta Conta Verbas em todos os servidores APS"""
        inicio = perf_counter()
        
        if log_callback:
            log_callback(f"🔍 Conta Verbas APS: {len(self.servidores)} servidores...")
        
        query = config['sql_template'].format(ano=ano)
        resultados, erros = [], []
        servidores_ok = 0
        
        with ThreadPoolExecutor(max_workers=len(self.servidores)) as executor:
            futures = {
                executor.submit(self._executar_query, srv, query, 'APS', log_callback): srv
                for srv in self.servidores
            }
            
            try:
                for future in as_completed(futures, timeout=TIMEOUT_GLOBAL):
                    _, sucesso, df, erro = future.result(timeout=5)
                    if sucesso and not df.empty:
                        resultados.append(df)
                        servidores_ok += 1
                    elif erro:
                        erros.append(erro)
            except TimeoutError:
                if log_callback:
                    log_callback(f"⏱️ Timeout global")
        
        df_final = pd.concat(resultados, ignore_index=True) if resultados else pd.DataFrame()
        tempo = perf_counter() - inicio
        
        if log_callback:
            log_callback(f"✅ {len(df_final)} linhas de {servidores_ok} servidores em {tempo:.2f}s")
        
        # Formatar dados
        dados, colunas = converter_para_json(df_final)
        
        return {
            'status': 'sucesso' if resultados else 'erro',
            'dados': dados,
            'colunas': colunas,
            'linhas_afetadas': len(df_final),
            'servidores_processados': servidores_ok,
            'tempo_total': round(tempo, 2),
            'mensagem': f"{len(df_final)} linhas de {servidores_ok}/{len(self.servidores)} servidores",
            'dataframe': df_final,
            'avisos': erros or None
        }