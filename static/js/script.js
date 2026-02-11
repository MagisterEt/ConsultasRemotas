// ==============================================================
// script.js - Com Cancelamento de Consultas
// ==============================================================

let dadosConsulta = [];
let colunasOrdenadas = [];
let tipoConsultaAtual = '';
let parametrosUltimaConsulta = {};
let consultasDisponiveis = {};
let eventSource = null;
let requestIdAtual = null;
let consultaEmAndamento = false;

document.addEventListener('DOMContentLoaded', function() {
    verificarStatusServidor();
    carregarConsultasDisponiveis();
    setInterval(verificarStatusServidor, 60000);
});

// ============================================================
// Carregar consultas
// ============================================================
function carregarConsultasDisponiveis() {
    fetch('/api/consultas_disponiveis')
        .then(response => response.json())
        .then(data => {
            if (data.status === 'sucesso' && data.consultas) {
                consultasDisponiveis = {};
                data.consultas.forEach(c => consultasDisponiveis[c.tipo] = c);
                popularDropdownConsultas(data.consultas);
            }
        })
        .catch(error => console.error('Erro:', error));
}

function popularDropdownConsultas(consultas) {
    const select = document.getElementById('tipoConsulta');
    select.innerHTML = '<option value="">Selecione...</option>';
    consultas.forEach(c => {
        const opt = document.createElement('option');
        opt.value = c.tipo;
        opt.textContent = c.nome;
        opt.title = c.descricao;
        select.appendChild(opt);
    });
}

// ============================================================
// Atualizar campos
// ============================================================
function atualizarCamposFormulario(tipo) {
    // Se mudou de consulta e tem uma em andamento, cancelar
    if (consultaEmAndamento && tipo !== tipoConsultaAtual) {
        cancelarConsulta(true); // silencioso
    }
    
    const consulta = consultasDisponiveis[tipo];
    esconderTodosCampos();
    if (!consulta) return;
    
    if (consulta.requer_entidade) document.getElementById('entidadeGroup').style.display = 'block';
    if (consulta.requer_ano) document.getElementById('anoGroup').style.display = 'block';
    if (consulta.requer_periodo) document.getElementById('periodoGroup').style.display = 'block';
    if (consulta.requer_data_limite) {
        document.getElementById('dataLimiteGroup').style.display = 'block';
        if (consulta.data_limite_padrao === 'hoje') {
            document.getElementById('dataLimite').value = new Date().toISOString().split('T')[0];
        }
    }
    if (consulta.requer_codigo) document.getElementById('codigoGroup').style.display = 'block';
    if (consulta.requer_saldo_anterior) document.getElementById('saldoAnteriorGroup').style.display = 'block';
    if (consulta.requer_meses_atras) document.getElementById('mesesAtrasGroup').style.display = 'block';
    if (tipo === 'personalizada') document.getElementById('sqlGroup').style.display = 'block';
}

function esconderTodosCampos() {
    ['entidadeGroup', 'periodoGroup', 'anoGroup', 'dataLimiteGroup', 
     'codigoGroup', 'sqlGroup', 'saldoAnteriorGroup', 'mesesAtrasGroup'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.style.display = 'none';
    });
}

// ============================================================
// Status
// ============================================================
function verificarStatusServidor() {
    fetch('/api/status', { method: 'GET', cache: 'no-cache' })
        .then(r => r.json())
        .then(data => {
            const indicator = document.getElementById('statusIndicator');
            const text = document.getElementById('statusText');
            if (data.status === 'online') {
                indicator.className = 'status-indicator online';
                text.textContent = 'Online';
                if (data.consultas_ativas > 0) {
                    text.textContent += ` (${data.consultas_ativas} ativa${data.consultas_ativas > 1 ? 's' : ''})`;
                }
            } else {
                indicator.className = 'status-indicator offline';
                text.textContent = `Offline - ${data.mensagem}`;
            }
        })
        .catch(() => {});
}

// ============================================================
// Cancelar consulta
// ============================================================
function cancelarConsulta(silencioso = false) {
    if (!consultaEmAndamento && !requestIdAtual) {
        if (!silencioso) mostrarToast('Nenhuma consulta em andamento', 'info');
        return;
    }
    
    // Fechar SSE
    if (eventSource) {
        eventSource.close();
        eventSource = null;
    }
    
    // Chamar API de cancelamento
    if (requestIdAtual) {
        fetch(`/api/cancelar/${requestIdAtual}`, { method: 'POST' })
            .then(r => r.json())
            .then(data => {
                if (!silencioso) {
                    adicionarLog('⛔ Consulta cancelada pelo usuário', 'warning');
                    mostrarToast('Consulta cancelada', 'warning');
                }
            })
            .catch(() => {});
    }
    
    // Resetar estado
    finalizarConsulta();
    
    if (!silencioso) {
        esconderLoading();
    }
}

function cancelarTodasConsultas() {
    fetch('/api/cancelar_todas', { method: 'POST' })
        .then(r => r.json())
        .then(data => {
            mostrarToast(data.mensagem, 'info');
        })
        .catch(() => {});
    
    if (eventSource) {
        eventSource.close();
        eventSource = null;
    }
    
    finalizarConsulta();
    esconderLoading();
}

function finalizarConsulta() {
    consultaEmAndamento = false;
    requestIdAtual = null;
    
    // Mostrar/esconder botões
    document.getElementById('consultarBtn').disabled = false;
    document.getElementById('cancelarBtn').style.display = 'none';
    
    const cancelarLogsBtn = document.getElementById('cancelarLogsBtn');
    if (cancelarLogsBtn) cancelarLogsBtn.style.display = 'none';
}

function iniciarConsulta() {
    consultaEmAndamento = true;
    
    // Mostrar/esconder botões
    document.getElementById('consultarBtn').disabled = true;
    document.getElementById('cancelarBtn').style.display = 'inline-flex';
    
    const cancelarLogsBtn = document.getElementById('cancelarLogsBtn');
    if (cancelarLogsBtn) cancelarLogsBtn.style.display = 'inline-block';
}

// ============================================================
// Executar
// ============================================================
function executarConsulta() {
    const form = document.getElementById('consultaForm');
    const formData = new FormData(form);
    const tipo = formData.get('tipo');
    
    if (!tipo) { mostrarErro('Selecione o tipo de consulta'); return; }
    
    const consulta = consultasDisponiveis[tipo];
    if (!consulta) { mostrarErro('Consulta não encontrada'); return; }

    // Se já tem consulta em andamento, cancelar primeiro
    if (consultaEmAndamento) {
        cancelarConsulta(true);
    }

    tipoConsultaAtual = tipo;
    parametrosUltimaConsulta = {
        tipo, ano: formData.get('ano'), periodo: formData.get('periodo'),
        entidade: formData.get('entidade'), data_limite: formData.get('data_limite'),
        meses_atras: formData.get('meses_atras')
    };
    
    esconderResultados();
    esconderErro();
    mostrarLoading();
    iniciarConsulta();

    if (consulta.tipo_execucao === 'multi_servidor' || consulta.tipo_execucao === 'multi_banco') {
        executarConsultaMultiServidor(tipo, formData, consulta);
    } else {
        executarConsultaSingleServidor(tipo, formData);
    }
}

// ============================================================
// Single servidor
// ============================================================
function executarConsultaSingleServidor(tipo, formData) {
    fetch('/api/consultar', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            tipo, entidade: formData.get('entidade'), ano: formData.get('ano'),
            periodo: formData.get('periodo'), codigo_imobilizado: formData.get('codigo_imobilizado'),
            upload_sharepoint: false
        })
    })
    .then(r => r.json())
    .then(data => {
        esconderLoading();
        finalizarConsulta();
        if (data.status === 'erro') mostrarErro(data.mensagem);
        else { dadosConsulta = data.dados; colunasOrdenadas = data.colunas || []; mostrarResultados(data); }
    })
    .catch(e => { esconderLoading(); finalizarConsulta(); mostrarErro('Erro: ' + e.message); });
}

// ============================================================
// Multi-servidor
// ============================================================
function executarConsultaMultiServidor(tipo, formData, consulta) {
    let requestData = { tipo, upload_sharepoint: false };
    
    if (tipo === 'personalizada') {
        const sql = document.getElementById('sqlPersonalizado').value;
        if (!sql.trim()) { esconderLoading(); finalizarConsulta(); mostrarErro('Digite uma query SQL'); return; }
        requestData.query = sql;
    }
    
    if (consulta.requer_ano || consulta.requer_periodo) {
        requestData.ano = formData.get('ano') || new Date().getFullYear();
        requestData.periodo = formData.get('periodo') || new Date().getMonth() + 1;
    }
    
    if (consulta.requer_data_limite) {
        requestData.data_limite = formData.get('data_limite') || new Date().toISOString().split('T')[0];
    }
    
    if (consulta.requer_saldo_anterior) {
        const cb = document.getElementById('incluirSaldoAnterior');
        if (cb && cb.checked) requestData.incluir_saldo_anterior = true;
    }
    
    if (consulta.requer_meses_atras) {
        requestData.meses_atras = formData.get('meses_atras') || 2;
    }

    // Painel de logs
    mostrarPainelLogs();
    limparLogs();
    adicionarLog('🚀 Iniciando consulta...', 'info');

    fetch('/api/consultar_multi', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(requestData)
    })
    .then(r => r.json())
    .then(data => {
        if (data.status === 'iniciado' && data.request_id) {
            requestIdAtual = data.request_id;
            adicionarLog(`📡 Consulta iniciada (ID: ${data.request_id.slice(0, 8)}...)`, 'info');
            
            // Iniciar SSE para logs em tempo real
            iniciarSSE(data.request_id);
        } else if (data.status === 'erro') {
            esconderLoading();
            finalizarConsulta();
            adicionarLog('❌ ' + data.mensagem, 'error');
            mostrarErro(data.mensagem);
        }
    })
    .catch(e => {
        esconderLoading();
        finalizarConsulta();
        adicionarLog('❌ Erro: ' + e.message, 'error');
        mostrarErro('Erro: ' + e.message);
    });
}

// ============================================================
// SSE - Server-Sent Events
// ============================================================
function iniciarSSE(requestId) {
    if (eventSource) eventSource.close();
    
    eventSource = new EventSource(`/api/logs/${requestId}`);
    
    eventSource.onmessage = function(event) {
        const msg = event.data;
        
        if (msg === 'DONE') {
            eventSource.close();
            eventSource = null;
            
            // Buscar resultado final
            buscarResultado(requestId);
            return;
        }
        
        if (msg.trim() === '') return; // heartbeat
        
        let tipo = 'info';
        if (msg.includes('✅') || msg.includes('sucesso') || msg.includes('Concluído')) tipo = 'success';
        else if (msg.includes('❌') || msg.includes('erro') || msg.includes('Erro')) tipo = 'error';
        else if (msg.includes('⏱️') || msg.includes('⚠️') || msg.includes('⛔') || msg.includes('Cancelad')) tipo = 'warning';
        
        adicionarLog(msg, tipo);
    };
    
    eventSource.onerror = function() {
        if (eventSource) {
            eventSource.close();
            eventSource = null;
        }
        // Tentar buscar resultado mesmo com erro no SSE
        if (requestIdAtual) {
            setTimeout(() => buscarResultado(requestIdAtual), 1000);
        }
    };
}

function buscarResultado(requestId) {
    fetch(`/api/resultado/${requestId}`)
        .then(r => r.json())
        .then(data => {
            esconderLoading();
            finalizarConsulta();
            
            if (data.status === 'processando') {
                // Ainda processando, tentar novamente
                setTimeout(() => buscarResultado(requestId), 1000);
                return;
            }
            
            if (data.status === 'cancelado') {
                adicionarLog('⛔ Consulta foi cancelada', 'warning');
                if (data.dados && data.dados.length > 0) {
                    dadosConsulta = data.dados;
                    colunasOrdenadas = data.colunas || [];
                    mostrarResultados(data);
                    mostrarToast(`Cancelado com ${data.dados.length} resultados parciais`, 'warning');
                }
                return;
            }
            
            if (data.status === 'erro') {
                adicionarLog('❌ ' + data.mensagem, 'error');
                mostrarErro(data.mensagem);
                return;
            }
            
            // Sucesso
            dadosConsulta = data.dados || [];
            colunasOrdenadas = data.colunas || [];
            
            let logMsg = `✅ Finalizado: ${data.linhas_afetadas || 0} linhas`;
            if (data.tempo_total) logMsg += ` em ${data.tempo_total}s`;
            adicionarLog(logMsg, 'success');
            
            mostrarResultados(data);
        })
        .catch(e => {
            esconderLoading();
            finalizarConsulta();
            adicionarLog('❌ Erro ao buscar resultado: ' + e.message, 'error');
        });
}

// ============================================================
// Logs
// ============================================================
function mostrarPainelLogs() {
    const painel = document.getElementById('logsPainelDiv');
    if (painel) painel.style.display = 'block';
}

function esconderPainelLogs() {
    const painel = document.getElementById('logsPainelDiv');
    if (painel) painel.style.display = 'none';
}

function limparLogs() {
    const container = document.getElementById('logsContainer');
    if (container) container.innerHTML = '';
}

function adicionarLog(mensagem, tipo = 'info') {
    const container = document.getElementById('logsContainer');
    if (!container) return;
    
    const logItem = document.createElement('div');
    logItem.className = `log-item log-${tipo}`;
    const hora = new Date().toLocaleTimeString('pt-BR');
    logItem.textContent = `[${hora}] ${mensagem}`;
    container.appendChild(logItem);
    container.scrollTop = container.scrollHeight;
}

// ============================================================
// Mostrar resultados
// ============================================================
function mostrarResultados(data) {
    if (!data.dados || data.dados.length === 0) {
        if (data.avisos && data.avisos.length > 0) {
            const avisos = data.avisos.join('\n');
            mostrarErro(`Nenhum resultado encontrado.\n\nAvisos:\n${avisos}`);
        } else {
            mostrarErro('Nenhum resultado encontrado');
        }
        return;
    }

    const resultadoDiv = document.getElementById('resultadoDiv');
    const resultadoInfo = document.getElementById('resultadoInfo');
    const tableHead = document.getElementById('tableHead');
    const tableBody = document.getElementById('tableBody');

    tableHead.innerHTML = '';
    tableBody.innerHTML = '';
    
    // Remover avisos anteriores
    const avisosAnteriores = resultadoDiv.querySelector('.avisos-box');
    if (avisosAnteriores) avisosAnteriores.remove();

    // Info
    let infoTexto = `${data.linhas_afetadas || data.dados.length} linha(s) retornada(s)`;
    if (data.servidores_processados) {
        infoTexto += ` de ${data.servidores_processados}/${data.servidores_total} servidor(es)`;
        if (data.servidores_timeout > 0) infoTexto += ` | ⏱️ ${data.servidores_timeout} timeout`;
        if (data.servidores_erro > 0) infoTexto += ` | ❌ ${data.servidores_erro} erro(s)`;
    }
    if (data.tempo_total) infoTexto += ` em ${data.tempo_total}s`;
    resultadoInfo.textContent = infoTexto;
    
    // Avisos
    if (data.avisos && data.avisos.length > 0) {
        const avisoDiv = document.createElement('div');
        avisoDiv.className = 'avisos-box';
        avisoDiv.innerHTML = `
            <strong>⚠️ Avisos:</strong>
            <ul>${data.avisos.map(aviso => `<li>${aviso}</li>`).join('')}</ul>
        `;
        resultadoDiv.querySelector('.result-info').after(avisoDiv);
    }

    const colunas = data.colunas || Object.keys(data.dados[0]);
    colunasOrdenadas = colunas;
    
    const headerRow = document.createElement('tr');
    colunas.forEach(coluna => {
        const th = document.createElement('th');
        th.textContent = coluna;
        headerRow.appendChild(th);
    });
    tableHead.appendChild(headerRow);

    // Limitar a 1000 linhas
    const dadosExibir = data.dados.slice(0, 1000);
    
    dadosExibir.forEach(row => {
        const tr = document.createElement('tr');
        colunas.forEach(coluna => {
            const td = document.createElement('td');
            let valor = row[coluna];
            
            // Dados já vêm formatados do backend (datas pt-BR, números com vírgula)
            if (valor === null || valor === undefined) {
                valor = '';
            }
            
            td.textContent = valor;
            tr.appendChild(td);
        });
        tableBody.appendChild(tr);
    });
    
    // Aviso de mais linhas
    if (data.dados.length > 1000) {
        const tr = document.createElement('tr');
        const td = document.createElement('td');
        td.colSpan = colunas.length;
        td.className = 'mais-linhas-aviso';
        td.textContent = `... e mais ${data.dados.length - 1000} linha(s). Use Exportar CSV/Excel para ver todos os dados.`;
        tr.appendChild(td);
        tableBody.appendChild(tr);
    }

    resultadoDiv.style.display = 'block';
}

// ============================================================
// SharePoint
// ============================================================
function enviarParaSharePoint() {
    if (dadosConsulta.length === 0) { mostrarToast('❌ Sem dados', 'error'); return; }
    
    const btn = document.getElementById('sharepointBtn');
    const original = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<span class="btn-icon">⏳</span> Enviando...';
    mostrarToast('📤 Enviando...', 'info');
    
    fetch('/api/upload_sharepoint', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            tipo: tipoConsultaAtual, dados: dadosConsulta, 
            colunas: colunasOrdenadas, ...parametrosUltimaConsulta
        })
    })
    .then(r => r.json())
    .then(data => {
        btn.disabled = false; btn.innerHTML = original;
        mostrarToast(data.status === 'sucesso' ? '✅ ' + data.mensagem : '❌ ' + data.mensagem, 
                     data.status === 'sucesso' ? 'success' : 'error');
    })
    .catch(e => {
        btn.disabled = false; btn.innerHTML = original;
        mostrarToast('❌ Erro: ' + e.message, 'error');
    });
}

// ============================================================
// Exportar
// ============================================================
function exportarCSV() {
    if (dadosConsulta.length === 0) { mostrarToast('Sem dados', 'error'); return; }
    
    const colunas = colunasOrdenadas.length > 0 ? colunasOrdenadas : Object.keys(dadosConsulta[0]);
    let csv = colunas.join(';') + '\n';
    
    dadosConsulta.forEach(row => {
        const valores = colunas.map(col => {
            let val = row[col];
            if (val === null || val === undefined) return '';
            val = String(val);
            if (val.includes(';') || val.includes('"') || val.includes('\n')) 
                val = '"' + val.replace(/"/g, '""') + '"';
            return val;
        });
        csv += valores.join(';') + '\n';
    });
    
    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `consulta_${tipoConsultaAtual}_${new Date().toISOString().slice(0,10)}.csv`;
    link.click();
    URL.revokeObjectURL(link.href);
    mostrarToast('✅ CSV baixado!', 'success');
}

function exportarExcel() {
    if (dadosConsulta.length === 0) { mostrarToast('Sem dados', 'error'); return; }
    
    const colunas = colunasOrdenadas.length > 0 ? colunasOrdenadas : Object.keys(dadosConsulta[0]);
    const dadosOrdenados = dadosConsulta.map(row => {
        const obj = {};
        colunas.forEach(col => obj[col] = row[col]);
        return obj;
    });
    
    const ws = XLSX.utils.json_to_sheet(dadosOrdenados, { header: colunas });
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, "Resultados");
    XLSX.writeFile(wb, `consulta_${tipoConsultaAtual}_${new Date().toISOString().slice(0,10)}.xlsx`);
    mostrarToast('✅ Excel baixado!', 'success');
}

function copiarResultados() {
    if (dadosConsulta.length === 0) { mostrarToast('Sem dados', 'error'); return; }

    const colunas = colunasOrdenadas.length > 0 ? colunasOrdenadas : Object.keys(dadosConsulta[0]);
    let texto = colunas.join('\t') + '\n';
    dadosConsulta.forEach(row => {
        texto += colunas.map(col => row[col] ?? '').join('\t') + '\n';
    });

    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(texto)
            .then(() => mostrarToast('✅ Copiado!', 'success'))
            .catch(() => copiarFallback(texto));
    } else {
        copiarFallback(texto);
    }
}

function copiarFallback(texto) {
    const ta = document.createElement('textarea');
    ta.value = texto;
    ta.style.cssText = 'position:fixed;top:0;left:0;width:1px;height:1px;opacity:0';
    document.body.appendChild(ta);
    ta.select();
    try {
        document.execCommand('copy') ? mostrarToast('✅ Copiado!', 'success') 
                                      : mostrarToast('❌ Erro', 'error');
    } catch(e) { mostrarToast('❌ Erro', 'error'); }
    document.body.removeChild(ta);
}

// ============================================================
// Auxiliares
// ============================================================
function mostrarToast(msg, tipo = 'info') {
    document.querySelectorAll('.toast').forEach(t => t.remove());
    const toast = document.createElement('div');
    toast.className = 'toast';
    toast.textContent = msg;
    toast.style.background = tipo === 'error' ? '#ef4444' : tipo === 'success' ? '#10b981' : tipo === 'warning' ? '#f59e0b' : '#3b82f6';
    document.body.appendChild(toast);
    setTimeout(() => { toast.style.opacity = '0'; setTimeout(() => toast.remove(), 300); }, 3000);
}

function mostrarLoading() { 
    document.getElementById('loadingDiv').style.display = 'block'; 
    document.getElementById('loadingText').textContent = 'Executando consulta...';
}
function esconderLoading() { document.getElementById('loadingDiv').style.display = 'none'; }
function mostrarErro(msg) { 
    const div = document.getElementById('errorDiv'); 
    div.textContent = '❌ ' + msg; 
    div.style.display = 'block'; 
}
function esconderErro() { document.getElementById('errorDiv').style.display = 'none'; }
function esconderResultados() { document.getElementById('resultadoDiv').style.display = 'none'; }

function limparFormulario() {
    // Cancelar se tiver consulta em andamento
    if (consultaEmAndamento) {
        cancelarConsulta(true);
    }
    
    document.getElementById('consultaForm').reset();
    esconderResultados(); esconderErro(); esconderTodosCampos(); esconderPainelLogs();
    dadosConsulta = []; colunasOrdenadas = []; parametrosUltimaConsulta = {};
    
    if (eventSource) { eventSource.close(); eventSource = null; }
    finalizarConsulta();
}

// Atalhos de teclado
document.addEventListener('keydown', function(e) {
    if (e.ctrlKey && e.key === 'Enter') executarConsulta();
    if (e.key === 'Escape') {
        if (consultaEmAndamento) {
            cancelarConsulta();
        } else {
            limparFormulario();
        }
    }
});