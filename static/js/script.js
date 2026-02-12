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
let intervalLogs = null;
let ultimoLogIndex = 0;


async function fetchJsonWithFallback(urls) {
    let lastError = null;
    for (const url of urls) {
        try {
            const response = await fetch(url, { cache: 'no-cache' });
            if (!response.ok) throw new Error(`HTTP ${response.status} em ${url}`);
            return await response.json();
        } catch (err) {
            lastError = err;
        }
    }
    throw lastError || new Error('Falha ao carregar dados');
}

document.addEventListener('DOMContentLoaded', function() {
    verificarStatusServidor();
    carregarConsultasDisponiveis();
    setInterval(verificarStatusServidor, 60000);
});

// ============================================================
// Carregar consultas
// ============================================================
function carregarConsultasDisponiveis() {
    fetchJsonWithFallback(['/api/consultas/disponiveis', '/api/consultas_disponiveis'])
        .then(data => {
            let consultas = [];
            if (Array.isArray(data)) {
                consultas = data;
            } else if (Array.isArray(data.consultas)) {
                consultas = data.consultas;
            } else if (Array.isArray(data)) {
                consultas = data;
            }

            consultasDisponiveis = {};
            consultas.forEach(c => {
                const tipo = c.tipo || c.id;
                if (!tipo) return;
                consultasDisponiveis[tipo] = {
                    ...c,
                    tipo,
                    requer_entidade: c.requer_entidade ?? c.parametros?.some?.(p => p.nome === 'entidade' && p.obrigatorio) ?? false,
                    requer_ano: c.requer_ano ?? c.parametros?.some?.(p => p.nome === 'ano' && p.obrigatorio) ?? false,
                    requer_periodo: c.requer_periodo ?? c.parametros?.some?.(p => p.nome === 'periodo' && p.obrigatorio) ?? false
                };
            });

            popularDropdownConsultas(Object.values(consultasDisponiveis));
        })
        .catch(error => {
            console.error('Erro ao carregar consultas disponíveis:', error);
            mostrarErro('Não foi possível carregar as consultas. Atualize a página ou verifique a API.');
        });
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
    fetchJsonWithFallback(['/api/status'])
        .then(data => {
            const indicator = document.getElementById('statusIndicator');
            const text = document.getElementById('statusText');
            if (!indicator || !text) return;

            if (data.status === 'online') {
                indicator.className = 'status-indicator online';
                text.textContent = 'Online';
                if (data.consultas_ativas > 0) {
                    text.textContent += ` (${data.consultas_ativas} ativa${data.consultas_ativas > 1 ? 's' : ''})`;
                }
            } else {
                indicator.className = 'status-indicator offline';
                text.textContent = `Offline${data.mensagem ? ' - ' + data.mensagem : ''}`;
            }
        })
        .catch((error) => {
            const indicator = document.getElementById('statusIndicator');
            const text = document.getElementById('statusText');
            if (indicator) indicator.className = 'status-indicator offline';
            if (text) text.textContent = 'Offline - API indisponível';
            console.error('Erro ao consultar status da API:', error);
        });
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

    executarConsultaPreDefinida(tipo, formData);
}

// ============================================================
// Single servidor
// ============================================================
function executarConsultaPreDefinida(tipo, formData) {
    fetch('/api/consultas/executar', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            tipo,
            entidade: formData.get('entidade') || null,
            ano: formData.get('ano') ? parseInt(formData.get('ano')) : null,
            periodo: formData.get('periodo') ? parseInt(formData.get('periodo')) : null,
            data_limite: formData.get('data_limite') || null,
            meses_atras: formData.get('meses_atras') ? parseInt(formData.get('meses_atras')) : null
        })
    })
    .then(r => r.json())
    .then(data => {
        esconderLoading();
        finalizarConsulta();

        if (data.error) {
            mostrarErro(data.error);
            return;
        }

        if (data.status && data.status !== 'sucesso') {
            mostrarErro(data.mensagem || 'Erro ao executar consulta');
            return;
        }

        const normalizado = normalizarResultadoApi(data);
        dadosConsulta = normalizado.dados || [];
        colunasOrdenadas = normalizado.colunas || [];
        requestIdAtual = data.request_id || null;

        if (normalizado.avisos && normalizado.avisos.length) {
            mostrarPainelLogs();
            normalizado.avisos.forEach(a => adicionarLog('⚠️ ' + a, 'warning'));
        }

        if (requestIdAtual) {
            iniciarSSE(requestIdAtual);
        } else {
            mostrarResultados(normalizado);
        }
    })
    .catch(e => {
        esconderLoading();
        finalizarConsulta();
        mostrarErro('Erro: ' + e.message);
    });
}

// ============================================================
// Multi-servidor
// ============================================================
function executarConsultaMultiServidor(tipo, formData, consulta) {
    mostrarPainelLogs();
    limparLogs();
    adicionarLog('🚀 Iniciando consulta...', 'info');

    executarConsultaPreDefinida(tipo, formData);
}

function iniciarSSE(requestId) {
    if (!requestId) return;

    if (intervalLogs) {
        clearInterval(intervalLogs);
        intervalLogs = null;
    }

    ultimoLogIndex = 0;
    requestIdAtual = requestId;

    const poll = () => {
        fetch(`/api/logs/${requestId}`)
            .then(r => r.json())
            .then(data => {
                const logs = data.logs || [];
                for (let i = ultimoLogIndex; i < logs.length; i++) {
                    const msg = logs[i];
                    let tipo = 'info';
                    const lower = msg.toLowerCase();
                    if (lower.includes('erro')) tipo = 'error';
                    else if (lower.includes('conclu') || lower.includes('sucesso')) tipo = 'success';
                    else if (lower.includes('cancel')) tipo = 'warning';
                    adicionarLog(msg, tipo);
                }
                ultimoLogIndex = logs.length;
            })
            .catch(() => {});

        fetch(`/api/status/${requestId}`)
            .then(r => r.json())
            .then(status => {
                const st = (status.status || '').toLowerCase();
                if (st === 'completed' || st === 'error' || st === 'cancelled') {
                    clearInterval(intervalLogs);
                    intervalLogs = null;
                    buscarResultado(requestId);
                }
            })
            .catch(() => {});
    };

    poll();
    intervalLogs = setInterval(poll, 2000);
}

function buscarResultado(requestId) {
    fetch(`/api/resultado/${requestId}`)
        .then(r => r.json())
        .then(data => {
            const normalizado = normalizarResultadoApi(data);
            dadosConsulta = normalizado.dados || [];
            colunasOrdenadas = normalizado.colunas || [];
            mostrarResultados(normalizado);
            esconderLoading();
            finalizarConsulta();
        })
        .catch(() => {
            esconderLoading();
            finalizarConsulta();
        });
}

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


function normalizarResultadoApi(data) {
    if (data && data.dados) {
        return {
            ...data,
            linhas_afetadas: data.total_linhas || data.dados.length,
            tempo_total: data.tempo_segundos
        };
    }

    if (data && data.results) {
        return {
            status: data.status || 'sucesso',
            dados: data.results || [],
            colunas: data.results && data.results.length ? Object.keys(data.results[0]) : [],
            avisos: (data.errors || []).map(e => `${e.servidor || 'Servidor'}: ${e.error}`),
            linhas_afetadas: data.total_rows || 0,
            tempo_total: data.execution_time_ms ? (data.execution_time_ms / 1000).toFixed(2) : null
        };
    }

    return { status: 'erro', dados: [], colunas: [], avisos: ['Resposta inválida da API'] };
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