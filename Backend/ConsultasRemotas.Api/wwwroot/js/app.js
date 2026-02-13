/**
 * Aplicação Principal - Consultas Remotas
 */

class ConsultasApp {
    constructor() {
        this.apiBase = '/api';
        this.currentRequestId = null;
        this.availableQueries = [];
        this.currentResults = null;
    }

    init() {
        console.log('📱 Inicializando aplicação...');

        // Event listeners
        this.setupEventListeners();

        // Carregar consultas disponíveis
        this.loadAvailableQueries();

        // Verificar status da API
        this.checkApiStatus();
    }

    setupEventListeners() {
        // Formulário
        document.getElementById('queryForm')?.addEventListener('submit', (e) => {
            e.preventDefault();
            this.executeQuery();
        });

        document.getElementById('clearBtn')?.addEventListener('click', () => this.clearForm());
        document.getElementById('queryType')?.addEventListener('change', (e) => this.handleQueryTypeChange(e.target.value));

        // Botões de ação
        document.getElementById('cancelBtn')?.addEventListener('click', () => this.cancelQuery());
        document.getElementById('copyBtn')?.addEventListener('click', () => this.copyResults());
        document.getElementById('exportCsvBtn')?.addEventListener('click', () => this.exportResults('csv'));
        document.getElementById('exportExcelBtn')?.addEventListener('click', () => this.exportResults('xlsx'));
        document.getElementById('sharePointBtn')?.addEventListener('click', () => this.uploadToSharePoint());
        document.getElementById('clearLogsBtn')?.addEventListener('click', () => this.clearLogs());
    }

    async loadAvailableQueries() {
        try {
            const response = await fetch(`${this.apiBase}/consultas_disponiveis`);

            if (!response.ok) {
                throw new Error(`Erro ${response.status}: ${response.statusText}`);
            }

            const data = await response.json();
            this.availableQueries = data;
            this.populateQuerySelect(data);
        } catch (error) {
            this.showError('Erro ao carregar consultas disponíveis: ' + error.message);
            console.error('Erro detalhado:', error);
        }
    }

    populateQuerySelect(queries) {
        const select = document.getElementById('queryType');
        if (!select) return;

        select.innerHTML = '<option value="">Selecione uma consulta...</option>';

        // Adicionar consultas predefinidas
        queries.forEach(q => {
            const option = document.createElement('option');
            option.value = q.id;
            option.textContent = q.nome;
            option.dataset.descricao = q.descricao;
            select.appendChild(option);
        });

        // Adicionar opção de SQL personalizado
        const customOption = document.createElement('option');
        customOption.value = 'custom';
        customOption.textContent = '💻 SQL Personalizado';
        select.appendChild(customOption);
    }

    handleQueryTypeChange(queryType) {
        const customSqlGroup = document.getElementById('customSqlGroup');
        const dynamicParams = document.getElementById('dynamicParams');

        if (queryType === 'custom') {
            customSqlGroup.style.display = 'block';
            dynamicParams.innerHTML = '';
        } else if (queryType) {
            customSqlGroup.style.display = 'none';
            const query = this.availableQueries.find(q => q.id === queryType);
            if (query) {
                this.renderDynamicParams(query.parametros);
            }
        } else {
            customSqlGroup.style.display = 'none';
            dynamicParams.innerHTML = '';
        }
    }

    renderDynamicParams(parametros) {
        const container = document.getElementById('dynamicParams');
        container.innerHTML = '';

        if (!parametros || parametros.length === 0) return;

        parametros.forEach(param => {
            const formGroup = document.createElement('div');
            formGroup.className = 'form-group';

            const label = document.createElement('label');
            label.textContent = param.nome.replace(/_/g, ' ').toUpperCase();
            label.htmlFor = `param_${param.nome}`;

            let input;
            if (param.tipo === 'date') {
                input = document.createElement('input');
                input.type = 'date';
            } else if (param.tipo === 'int') {
                input = document.createElement('input');
                input.type = 'number';
            } else {
                input = document.createElement('input');
                input.type = 'text';
            }

            input.id = `param_${param.nome}`;
            input.name = param.nome;
            input.required = param.obrigatorio;

            if (param.descricao) {
                input.placeholder = param.descricao;
            }

            formGroup.appendChild(label);
            formGroup.appendChild(input);
            container.appendChild(formGroup);
        });
    }

    async executeQuery() {
        const queryType = document.getElementById('queryType')?.value;
        if (!queryType) {
            this.showError('Selecione um tipo de consulta');
            return;
        }

        // Coletar parâmetros
        const parametros = {};
        const dynamicParams = document.querySelectorAll('#dynamicParams input, #dynamicParams select');
        dynamicParams.forEach(input => {
            if (input.value) {
                parametros[input.name] = input.value;
            }
        });

        let requestBody;
        if (queryType === 'custom') {
            const customSql = document.getElementById('customSql')?.value;
            if (!customSql) {
                this.showError('Digite uma consulta SQL');
                return;
            }
            requestBody = {
                query: customSql,
                banco: 'AASI',
                parametros
            };
        } else {
            // Consulta predefinida - construir query baseada no tipo
            requestBody = {
                tipo_consulta: queryType,
                banco: 'AASI',
                parametros
            };
        }

        this.showLoading('Executando consulta...');
        this.clearMessages();

        try {
            const endpoint = queryType === 'custom' ? 'consultar' : 'consultar_multi';
            const response = await fetch(`${this.apiBase}/${endpoint}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(requestBody)
            });

            const data = await response.json();

            if (!response.ok) {
                // Mostrar erro mais detalhado
                const errorMsg = data.error || data.message || `Erro ${response.status}: ${response.statusText}`;
                throw new Error(errorMsg);
            }

            this.currentRequestId = data.request_id;
            this.currentResults = data.results;

            this.hideLoading();
            this.displayResults(data);
            this.showSuccess(`Consulta executada com sucesso! ${data.total_rows} registros encontrados.`);

        } catch (error) {
            this.hideLoading();
            this.showError('Erro ao executar consulta: ' + error.message);
            console.error('Erro detalhado:', error);
        }
    }

    displayResults(data) {
        if (!data.results || data.results.length === 0) {
            this.showError('Nenhum resultado encontrado');
            document.getElementById('resultsCard').style.display = 'none';
            return;
        }

        const resultsCard = document.getElementById('resultsCard');
        const resultsInfo = document.getElementById('resultsInfo');
        const resultsHead = document.getElementById('resultsHead');
        const resultsBody = document.getElementById('resultsBody');

        // Info
        resultsInfo.textContent = `${data.total_rows} registros encontrados em ${data.execution_time_ms}ms`;

        // Headers
        const headers = Object.keys(data.results[0]);
        resultsHead.innerHTML = headers.map(h => `<th>${h}</th>`).join('');

        // Rows
        resultsBody.innerHTML = data.results.map(row => {
            return '<tr>' + headers.map(h => {
                const value = row[h];
                return `<td>${value !== null && value !== undefined ? value : ''}</td>`;
            }).join('') + '</tr>';
        }).join('');

        resultsCard.style.display = 'block';
    }

    async exportResults(format) {
        if (!this.currentRequestId) {
            this.showError('Nenhum resultado para exportar');
            return;
        }

        try {
            const response = await fetch(`${this.apiBase}/exportar/${format}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    request_id: this.currentRequestId,
                    formato: format
                })
            });

            if (!response.ok) {
                throw new Error('Erro ao exportar');
            }

            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `consulta_${new Date().getTime()}.${format}`;
            a.click();
            window.URL.revokeObjectURL(url);

            this.showSuccess(`Arquivo ${format.toUpperCase()} baixado com sucesso!`);
        } catch (error) {
            this.showError('Erro ao exportar: ' + error.message);
        }
    }

    async uploadToSharePoint() {
        if (!this.currentRequestId) {
            this.showError('Nenhum resultado para enviar');
            return;
        }

        const fileName = prompt('Nome do arquivo (sem extensão):');
        if (!fileName) return;

        try {
            this.showLoading('Enviando para SharePoint...');

            const response = await fetch(`${this.apiBase}/upload_sharepoint`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    request_id: this.currentRequestId,
                    nome_arquivo: fileName,
                    formato: 'xlsx'
                })
            });

            const data = await response.json();

            if (!response.ok) {
                throw new Error(data.error || 'Erro ao enviar');
            }

            this.hideLoading();
            this.showSuccess(`Arquivo enviado para SharePoint com sucesso!`);
        } catch (error) {
            this.hideLoading();
            this.showError('Erro ao enviar: ' + error.message);
        }
    }

    copyResults() {
        if (!this.currentResults || this.currentResults.length === 0) {
            this.showError('Nenhum resultado para copiar');
            return;
        }

        const headers = Object.keys(this.currentResults[0]);
        const rows = this.currentResults.map(row =>
            headers.map(h => row[h] || '').join('\t')
        );

        const text = [headers.join('\t'), ...rows].join('\n');

        navigator.clipboard.writeText(text).then(() => {
            this.showSuccess('Resultados copiados para área de transferência!');
        }).catch(() => {
            this.showError('Erro ao copiar resultados');
        });
    }

    async cancelQuery() {
        if (!this.currentRequestId) return;

        try {
            await fetch(`${this.apiBase}/cancelar/${this.currentRequestId}`, {
                method: 'POST'
            });
            this.hideLoading();
            this.showError('Consulta cancelada');
        } catch (error) {
            console.error('Erro ao cancelar:', error);
        }
    }

    async checkApiStatus() {
        try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 5000); // 5s timeout

            const response = await fetch('/health', {
                signal: controller.signal
            });
            clearTimeout(timeoutId);

            const statusDot = document.getElementById('apiStatus');
            const statusText = document.getElementById('apiStatusText');

            if (response.ok) {
                statusDot.classList.remove('offline');
                statusText.textContent = 'API Conectada';
            } else {
                statusDot.classList.add('offline');
                statusText.textContent = `API Offline (${response.status})`;
            }
        } catch (error) {
            const statusDot = document.getElementById('apiStatus');
            const statusText = document.getElementById('apiStatusText');
            statusDot.classList.add('offline');

            if (error.name === 'AbortError') {
                statusText.textContent = 'API Offline (timeout)';
            } else {
                statusText.textContent = `API Offline: ${error.message}`;
            }
            console.error('Status check error:', error);
        }

        // Verificar novamente em 30s
        setTimeout(() => this.checkApiStatus(), 30000);
    }

    clearForm() {
        document.getElementById('queryForm')?.reset();
        document.getElementById('dynamicParams').innerHTML = '';
        document.getElementById('customSqlGroup').style.display = 'none';
        this.clearMessages();
    }

    clearLogs() {
        document.getElementById('logsContainer').innerHTML = '';
        document.getElementById('logsCard').style.display = 'none';
    }

    showLoading(message = 'Carregando...') {
        document.getElementById('loadingText').textContent = message;
        document.getElementById('loadingDiv').style.display = 'block';
    }

    hideLoading() {
        document.getElementById('loadingDiv').style.display = 'none';
    }

    showError(message) {
        const errorDiv = document.getElementById('errorDiv');
        errorDiv.textContent = '❌ ' + message;
        errorDiv.style.display = 'block';
        setTimeout(() => errorDiv.style.display = 'none', 5000);
    }

    showSuccess(message) {
        const successDiv = document.getElementById('successDiv');
        successDiv.textContent = '✅ ' + message;
        successDiv.style.display = 'block';
        setTimeout(() => successDiv.style.display = 'none', 5000);
    }

    clearMessages() {
        document.getElementById('errorDiv').style.display = 'none';
        document.getElementById('successDiv').style.display = 'none';
    }
}

// Instanciar aplicação (será iniciada após login)
window.app = new ConsultasApp();
