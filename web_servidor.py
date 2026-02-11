"""Servidor Web para Consultas SQL - Com Logs em Tempo Real"""
from flask import Flask, render_template, request, jsonify, send_file, Response
from datetime import datetime, timedelta
import pandas as pd
import io
import logging
import traceback
import uuid
import sys
from queue import Queue, Empty
from threading import Thread, Timer
from config import WEB_PORT, WEB_HOST, DEBUG_MODE, SECRET_KEY

logging.basicConfig(
    level=logging.DEBUG if DEBUG_MODE else logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[logging.StreamHandler(sys.stdout)]
)
logger = logging.getLogger(__name__)

app = Flask(__name__)
app.secret_key = SECRET_KEY

# Armazenamento de logs e resultados
logs_queues = {}      # Queue para SSE em tempo real
resultados = {}       # Resultados das consultas
consultas_ativas = {} # Flags de cancelamento {request_id: {'cancelado': False}}
LOG_TTL_MINUTES = 10


def limpar_dados_antigos():
    """Remove dados antigos"""
    agora = datetime.now()
    for rid in list(resultados.keys()):
        if resultados.get(f"{rid}_ts"):
            if agora - resultados[f"{rid}_ts"] > timedelta(minutes=LOG_TTL_MINUTES):
                resultados.pop(rid, None)
                resultados.pop(f"{rid}_ts", None)
                logs_queues.pop(rid, None)
                consultas_ativas.pop(rid, None)
    Timer(60, limpar_dados_antigos).start()

Timer(60, limpar_dados_antigos).start()


def enviar_log(request_id: str, msg: str):
    """Envia log para SSE em tempo real"""
    logger.info(f"[{request_id[:8]}] {msg}")
    if request_id in logs_queues:
        logs_queues[request_id].put(msg)


@app.errorhandler(Exception)
def handle_exception(e):
    logger.error(f"Erro: {e}\n{traceback.format_exc()}")
    return jsonify({'status': 'erro', 'mensagem': str(e)}), 500


@app.route('/')
def index():
    return render_template('index.html')


@app.route('/api/consultas_disponiveis')
def listar_consultas():
    """Lista consultas disponíveis"""
    try:
        from consultas_config import CONSULTAS_PREDEFINIDAS
        consultas = [{
            'tipo': t,
            'nome': c.get('nome', t),
            'descricao': c.get('descricao', ''),
            'tipo_execucao': c.get('tipo'),
            'requer_entidade': c.get('requer_entidade', False),
            'requer_ano': c.get('requer_ano', False),
            'requer_periodo': c.get('requer_periodo', False),
            'requer_data_limite': c.get('requer_data_limite', False),
            'requer_meses_atras': c.get('requer_meses_atras', False),
            'requer_saldo_anterior': c.get('requer_saldo_anterior', False),
        } for t, c in CONSULTAS_PREDEFINIDAS.items()]
        return jsonify({'status': 'sucesso', 'consultas': consultas})
    except Exception as e:
        return jsonify({'status': 'erro', 'mensagem': str(e)}), 500


@app.route('/api/logs/<request_id>')
def stream_logs(request_id):
    """Stream de logs em tempo real via SSE"""
    def generate():
        # Criar queue se não existir
        if request_id not in logs_queues:
            logs_queues[request_id] = Queue()
        
        queue = logs_queues[request_id]
        
        # Enviar heartbeat inicial
        yield "data: 🔄 Conectado ao stream de logs...\n\n"
        
        timeout_count = 0
        while timeout_count < 300:  # 5 minutos máximo
            try:
                msg = queue.get(timeout=0.5)
                timeout_count = 0
                
                if msg == "DONE":
                    yield "data: DONE\n\n"
                    break
                    
                yield f"data: {msg}\n\n"
                
            except Empty:
                timeout_count += 1
                # Heartbeat a cada 15 segundos
                if timeout_count % 30 == 0:
                    yield "data: \n\n"
    
    return Response(
        generate(), 
        mimetype='text/event-stream',
        headers={
            'Cache-Control': 'no-cache, no-store, must-revalidate',
            'Pragma': 'no-cache',
            'Expires': '0',
            'X-Accel-Buffering': 'no',
            'Connection': 'keep-alive'
        }
    )


@app.route('/api/consultar', methods=['POST'])
def consultar_single():
    """Executa consulta em servidor único (por entidade)"""
    try:
        data = request.json or {}
        tipo = data.get('tipo')
        entidade = data.get('entidade')
        ano = int(data.get('ano', datetime.now().year))
        periodo = int(data.get('periodo', datetime.now().month))
        
        if not tipo:
            return jsonify({'status': 'erro', 'mensagem': 'Tipo de consulta não informado'}), 400
        
        from consultas_config import obter_consulta
        from config import SERVIDOR_POR_ENTIDADE, get_connection_string
        from formatador import converter_para_json
        
        config = obter_consulta(tipo)
        if not config:
            return jsonify({'status': 'erro', 'mensagem': f'Consulta não encontrada: {tipo}'}), 404
        
        if config.get('requer_entidade') and not entidade:
            return jsonify({'status': 'erro', 'mensagem': 'Entidade não informada'}), 400
        
        # Encontrar servidor da entidade
        servidor = SERVIDOR_POR_ENTIDADE.get(entidade)
        if not servidor:
            return jsonify({'status': 'erro', 'mensagem': f'Entidade {entidade} não encontrada'}), 404
        
        # Montar query
        query = config['sql_template']
        query = query.replace('{ano}', str(ano))
        query = query.replace('{periodo}', str(periodo))
        query = query.replace('{entidade}', entidade)
        
        # Executar
        conn_str = get_connection_string(servidor, 'AASI')
        
        import pyodbc
        with pyodbc.connect(conn_str, timeout=30) as conn:
            conn.timeout = 90
            cursor = conn.cursor()
            cursor.execute(query)
            
            colunas = [col[0] for col in cursor.description] if cursor.description else []
            rows = cursor.fetchall()
            df = pd.DataFrame.from_records(rows, columns=colunas)
            cursor.close()
        
        # Formatar dados (datas pt-BR, números com 2 casas, colunas em português)
        dados, colunas = converter_para_json(df)
        
        return jsonify({
            'status': 'sucesso',
            'dados': dados,
            'colunas': colunas,
            'linhas_afetadas': len(df),
            'servidor': servidor,
            'mensagem': f'{len(df)} linhas de {servidor}'
        })
        
    except Exception as e:
        logger.error(f"Erro consultar: {e}\n{traceback.format_exc()}")
        return jsonify({'status': 'erro', 'mensagem': str(e)}), 500


@app.route('/api/consultar_multi', methods=['POST'])
def consultar_multi():
    """Inicia consulta em background e retorna request_id para acompanhar"""
    request_id = str(uuid.uuid4())
    
    # Criar queue para logs
    logs_queues[request_id] = Queue()
    resultados[request_id] = None
    resultados[f"{request_id}_ts"] = datetime.now()
    consultas_ativas[request_id] = {'cancelado': False}
    
    data = request.json or {}
    
    # Iniciar consulta em thread separada
    thread = Thread(target=_executar_consulta_async, args=(request_id, data))
    thread.daemon = True
    thread.start()
    
    # Retornar imediatamente com request_id
    return jsonify({
        'status': 'iniciado',
        'request_id': request_id,
        'mensagem': 'Consulta iniciada. Acompanhe os logs via SSE.'
    })


@app.route('/api/cancelar/<request_id>', methods=['POST'])
def cancelar_consulta(request_id):
    """Cancela uma consulta em andamento"""
    if request_id not in consultas_ativas:
        return jsonify({'status': 'erro', 'mensagem': 'Consulta não encontrada'}), 404
    
    consultas_ativas[request_id]['cancelado'] = True
    enviar_log(request_id, "⛔ Cancelamento solicitado...")
    
    return jsonify({'status': 'sucesso', 'mensagem': 'Cancelamento solicitado'})


@app.route('/api/cancelar_todas', methods=['POST'])
def cancelar_todas():
    """Cancela todas as consultas em andamento"""
    canceladas = 0
    for rid, estado in consultas_ativas.items():
        if not estado.get('cancelado') and resultados.get(rid) is None:
            estado['cancelado'] = True
            enviar_log(rid, "⛔ Cancelamento solicitado...")
            canceladas += 1
    
    return jsonify({
        'status': 'sucesso', 
        'mensagem': f'{canceladas} consulta(s) cancelada(s)'
    })


def _executar_consulta_async(request_id: str, data: dict):
    """Executa consulta em background"""
    def log_cb(msg):
        enviar_log(request_id, msg)
    
    def foi_cancelado():
        """Verifica se a consulta foi cancelada"""
        return consultas_ativas.get(request_id, {}).get('cancelado', False)
    
    try:
        tipo = data.get('tipo')
        ano = int(data.get('ano', datetime.now().year))
        periodo = int(data.get('periodo', datetime.now().month))
        upload_sharepoint = data.get('upload_sharepoint', False)
        incluir_saldo = data.get('incluir_saldo_anterior', False)
        data_limite = data.get('data_limite')
        meses_atras = int(data.get('meses_atras', 2))
        
        log_cb(f"🔍 Iniciando consulta: {tipo}")
        log_cb(f"📅 Ano: {ano} | Período: {periodo}")
        
        # Verificar cancelamento
        if foi_cancelado():
            log_cb("⛔ Consulta cancelada antes de iniciar")
            resultados[request_id] = {'status': 'cancelado', 'mensagem': 'Consulta cancelada'}
            return
        
        from consultas_config import obter_consulta
        config = obter_consulta(tipo)
        
        if not config:
            raise ValueError(f"Consulta não encontrada: {tipo}")
        
        # Multi-banco (conferencia_13)
        if config.get('tipo') == 'multi_banco':
            log_cb("🔄 Modo: Multi-banco (APS + AASI)")
            from consulta_multi_banco import ConsultaMultiBanco
            resposta = ConsultaMultiBanco().executar_conferencia_13(ano, periodo, config, foi_cancelado, log_cb)
        
        # Multi-servidor
        else:
            log_cb(f"🔄 Modo: Multi-servidor")
            from consulta_multi_servidor import ConsultaMultiServidor
            
            query = config['sql_template']
            if config.get('requer_data_limite') and data_limite:
                query = query.replace('{data_limite}', data_limite)
            if config.get('requer_ano'):
                query = query.replace('{ano}', str(ano))
            if config.get('requer_periodo'):
                query = query.replace('{periodo}', str(periodo))
            if config.get('requer_meses_atras'):
                query = query.replace('{meses_atras}', str(meses_atras))
            
            consulta = ConsultaMultiServidor()
            servidores = list(config.get('entidades_por_servidor', {}).keys())
            log_cb(f"📡 Servidores: {len(servidores)}")
            
            resposta = consulta.executar_consulta_simultanea(query, servidores, config, log_cb, foi_cancelado)
            
            # Verificar cancelamento antes do saldo anterior
            if foi_cancelado():
                log_cb("⛔ Consulta cancelada")
                resultados[request_id] = {'status': 'cancelado', 'mensagem': 'Consulta cancelada', 'dados': resposta.get('dados', [])}
                return
            
            # Saldo anterior
            if incluir_saldo and config.get('requer_saldo_anterior'):
                log_cb("💳 Buscando saldo anterior...")
                config_saldo = obter_consulta('saldo_anterior')
                if config_saldo:
                    query_saldo = config_saldo['sql_template'].replace('{meses_atras}', str(meses_atras))
                    resp_saldo = consulta.executar_consulta_simultanea(
                        query_saldo, servidores, config_saldo, log_cb, foi_cancelado)
                    
                    if resp_saldo['status'] == 'sucesso' and resp_saldo['dados']:
                        for r in resposta['dados']:
                            r['Origem'] = 'Atual'
                        for r in resp_saldo['dados']:
                            r['Origem'] = 'Saldo Anterior'
                        resposta['dados'].extend(resp_saldo['dados'])
                        resposta['linhas_afetadas'] = len(resposta['dados'])
                        if 'Origem' not in resposta['colunas']:
                            resposta['colunas'].append('Origem')
                        log_cb(f"✅ Combinado: {len(resposta['dados'])} linhas")
        
        # Verificar cancelamento antes do upload
        if foi_cancelado():
            log_cb("⛔ Consulta cancelada antes do upload")
            resultados[request_id] = {'status': 'cancelado', 'mensagem': 'Consulta cancelada', 'dados': resposta.get('dados', [])}
            return
        
        # Upload SharePoint
        if upload_sharepoint and resposta['status'] == 'sucesso' and resposta.get('dados'):
            log_cb("☁️ Enviando para SharePoint...")
            df = pd.DataFrame(resposta['dados'])
            if resposta.get('colunas'):
                cols = [c for c in resposta['colunas'] if c in df.columns]
                df = df[cols]
            resposta['sharepoint'] = _upload_sharepoint(df, tipo, ano, periodo)
            if resposta['sharepoint'].get('status') == 'sucesso':
                log_cb(f"✅ Upload concluído: {resposta['sharepoint'].get('url', '')}")
            else:
                log_cb(f"❌ Erro upload: {resposta['sharepoint'].get('mensagem', '')}")
        
        resposta.pop('dataframe', None)
        log_cb(f"✅ Consulta finalizada! {resposta.get('linhas_afetadas', 0)} linhas")
        
        # Salvar resultado
        resultados[request_id] = resposta
        
    except Exception as e:
        log_cb(f"❌ Erro: {str(e)}")
        logger.error(f"Erro: {e}\n{traceback.format_exc()}")
        resultados[request_id] = {'status': 'erro', 'mensagem': str(e)}
    
    finally:
        # Sinalizar fim
        enviar_log(request_id, "DONE")


@app.route('/api/resultado/<request_id>')
def obter_resultado(request_id):
    """Obtém resultado de uma consulta pelo request_id"""
    if request_id not in resultados:
        return jsonify({'status': 'erro', 'mensagem': 'Request ID não encontrado'}), 404
    
    resultado = resultados.get(request_id)
    
    if resultado is None:
        return jsonify({'status': 'processando', 'mensagem': 'Consulta ainda em andamento'})
    
    resultado['request_id'] = request_id
    return jsonify(resultado)


def _upload_sharepoint(df: pd.DataFrame, tipo: str, ano: int, periodo: int) -> dict:
    """Helper para upload SharePoint"""
    try:
        from sharepoint_uploader import SharePointUploader
        
        nomes = {
            'ficha_loja': "BalanceteLoja.csv",
            'lotes_sem_anexo': "LotesSemAnexo.csv",
            'conferencia_13': "Conferencia13.csv"
        }
        filename = nomes.get(tipo, f"Consulta_{tipo}.csv")
        
        return SharePointUploader().upload_csv(df, filename)
    except Exception as e:
        return {'status': 'erro', 'mensagem': str(e)}


@app.route('/api/upload_sharepoint', methods=['POST'])
def upload_sharepoint():
    """Upload manual para SharePoint"""
    try:
        data = request.json or {}
        dados = data.get('dados', [])
        colunas = data.get('colunas', [])
        tipo = data.get('tipo', 'consulta')
        ano = int(data.get('ano', datetime.now().year))
        periodo = int(data.get('periodo', datetime.now().month))
        
        if not dados:
            raise ValueError("Sem dados para enviar")
        
        df = pd.DataFrame(dados)
        if colunas:
            cols = [c for c in colunas if c in df.columns]
            df = df[cols]
        
        resultado = _upload_sharepoint(df, tipo, ano, periodo)
        return jsonify(resultado)
    except Exception as e:
        logger.error(f"Erro upload SharePoint: {e}")
        return jsonify({'status': 'erro', 'mensagem': str(e)}), 500


@app.route('/api/exportar/<formato>', methods=['POST'])
def exportar(formato):
    """Exporta dados para Excel ou CSV"""
    try:
        data = request.json or {}
        dados = data.get('dados', [])
        nome = data.get('nome_arquivo', 'exportacao')
        colunas = data.get('colunas', [])
        
        if not dados:
            raise ValueError("Sem dados")
        
        df = pd.DataFrame(dados)
        if colunas:
            cols = [c for c in colunas if c in df.columns]
            df = df[cols]
        
        if formato == 'excel':
            output = io.BytesIO()
            with pd.ExcelWriter(output, engine='openpyxl') as w:
                df.to_excel(w, index=False, sheet_name='Dados')
            output.seek(0)
            return send_file(output, mimetype='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
                           as_attachment=True, download_name=f'{nome}.xlsx')
        
        elif formato == 'csv':
            output = io.StringIO()
            df.to_csv(output, index=False, sep=';', encoding='utf-8-sig')
            return send_file(io.BytesIO(output.getvalue().encode('utf-8-sig')),
                           mimetype='text/csv', as_attachment=True, download_name=f'{nome}.csv')
        
        raise ValueError(f"Formato inválido: {formato}")
    except Exception as e:
        return jsonify({'erro': str(e)}), 400


@app.route('/api/status')
def status():
    """Status do servidor"""
    return jsonify({
        'status': 'online', 
        'timestamp': datetime.now().isoformat(),
        'consultas_ativas': len([r for r in resultados.values() if r is None])
    })


if __name__ == '__main__':
    print(f"\n{'='*50}")
    print(f"SERVIDOR WEB - CONSULTAS SQL")
    print(f"Porta: {WEB_PORT}")
    print(f"Logs em tempo real: /api/logs/<request_id>")
    print(f"{'='*50}\n")
    
    # Usar threaded=True para suportar SSE
    app.run(host=WEB_HOST, port=WEB_PORT, debug=DEBUG_MODE, threaded=True)