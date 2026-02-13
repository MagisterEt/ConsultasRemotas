using ConsultasRemotas.Api.Configuration;
using ConsultasRemotas.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;

namespace ConsultasRemotas.Api.Services;

public class QueryExecutorService : IQueryExecutorService
{
    private readonly SqlServerSettings _sqlSettings;
    private readonly ILogger<QueryExecutorService> _logger;
    private readonly ILogStreamService _logStreamService;

    // Cache de resultados e status de execução
    private readonly ConcurrentDictionary<string, QueryResponse> _resultsCache = new();
    private readonly ConcurrentDictionary<string, ExecutionStatus> _statusCache = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();

    public QueryExecutorService(
        IOptions<SqlServerSettings> sqlSettings,
        ILogger<QueryExecutorService> logger,
        ILogStreamService logStreamService)
    {
        _sqlSettings = sqlSettings.Value;
        _logger = logger;
        _logStreamService = logStreamService;
    }

    public async Task<QueryResponse> ExecuteQueryAsync(
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            UpdateStatus(requestId, "running", "Iniciando consulta...");
            await _logStreamService.LogAsync(requestId, $"Iniciando consulta no servidor: {request.Servidor}");

            var server = _sqlSettings.Servers.FirstOrDefault(s => s.Name == request.Servidor);
            if (server == null)
            {
                throw new ArgumentException($"Servidor não encontrado: {request.Servidor}");
            }

            var result = await ExecuteQueryOnServerAsync(
                server,
                request.Query,
                request.Banco,
                requestId,
                cancellationToken);

            stopwatch.Stop();

            var response = new QueryResponse
            {
                RequestId = requestId,
                Status = "completed",
                TotalRows = result.Data?.Count ?? 0,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Results = result.Data ?? new List<Dictionary<string, object>>()
            };

            if (!string.IsNullOrEmpty(result.Error))
            {
                response.Errors.Add(new QueryError
                {
                    Servidor = server.Name,
                    Error = result.Error
                });
            }

            _resultsCache[requestId] = response;
            UpdateStatus(requestId, "completed", $"Consulta concluída: {response.TotalRows} linhas");
            await _logStreamService.LogAsync(requestId, $"Consulta concluída: {response.TotalRows} linhas em {stopwatch.ElapsedMilliseconds}ms");

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Erro ao executar consulta");
            await _logStreamService.LogAsync(requestId, $"ERRO: {ex.Message}");

            var errorResponse = new QueryResponse
            {
                RequestId = requestId,
                Status = "error",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Errors = new List<QueryError>
                {
                    new QueryError { Error = ex.Message }
                }
            };

            UpdateStatus(requestId, "error", ex.Message);
            return errorResponse;
        }
    }

    public async Task<QueryResponse> ExecuteMultiServerQueryAsync(
        MultiServerQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokens[requestId] = cts;

        try
        {
            // Construir query se tipo_consulta foi fornecido
            if (!string.IsNullOrWhiteSpace(request.TipoConsulta) && string.IsNullOrWhiteSpace(request.Query))
            {
                request.Query = BuildPredefinedQuery(request.TipoConsulta, request.Parametros ?? new Dictionary<string, object>());
            }

            // Validar que temos uma query para executar
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                throw new ArgumentException("Query ou tipo_consulta deve ser fornecido");
            }

            UpdateStatus(requestId, "running", "Iniciando consulta em múltiplos servidores...");
            await _logStreamService.LogAsync(requestId, "=== INÍCIO DA CONSULTA MULTI-SERVIDOR ===");
            await _logStreamService.LogAsync(requestId, $"Query: {request.Query}");

            // Determinar quais servidores usar
            var servers = request.Servidores != null && request.Servidores.Any()
                ? _sqlSettings.Servers.Where(s => request.Servidores.Contains(s.Name)).ToList()
                : _sqlSettings.Servers;

            await _logStreamService.LogAsync(requestId, $"Executando em {servers.Count} servidores simultaneamente");

            // Executar em paralelo com otimização para Linux
            var tasks = servers.Select(server => ExecuteQueryOnServerAsync(
                server,
                request.Query,
                request.Banco,
                requestId,
                cts.Token));

            var results = await Task.WhenAll(tasks);

            stopwatch.Stop();

            // Agregar resultados
            var allData = new List<Dictionary<string, object>>();
            var serverResults = new Dictionary<string, ServerQueryResult>();
            var errors = new List<QueryError>();

            foreach (var result in results)
            {
                serverResults[result.Servidor] = result;

                if (result.Data != null)
                {
                    allData.AddRange(result.Data);
                }

                if (!string.IsNullOrEmpty(result.Error))
                {
                    errors.Add(new QueryError
                    {
                        Servidor = result.Servidor,
                        Error = result.Error
                    });
                }
            }

            var response = new QueryResponse
            {
                RequestId = requestId,
                Status = errors.Any() ? "completed_with_errors" : "completed",
                TotalRows = allData.Count,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Results = allData,
                Errors = errors,
                ServerResults = serverResults
            };

            _resultsCache[requestId] = response;
            UpdateStatus(requestId, "completed", $"Consulta concluída: {response.TotalRows} linhas de {servers.Count} servidores");

            await _logStreamService.LogAsync(requestId, "=== CONSULTA CONCLUÍDA ===");
            await _logStreamService.LogAsync(requestId, $"Total de linhas: {response.TotalRows}");
            await _logStreamService.LogAsync(requestId, $"Tempo total: {stopwatch.ElapsedMilliseconds}ms");
            await _logStreamService.LogAsync(requestId, $"Servidores com erro: {errors.Count}/{servers.Count}");

            return response;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            await _logStreamService.LogAsync(requestId, "Consulta cancelada pelo usuário");

            var canceledResponse = new QueryResponse
            {
                RequestId = requestId,
                Status = "cancelled",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            UpdateStatus(requestId, "cancelled", "Cancelado pelo usuário");
            return canceledResponse;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Erro ao executar consulta multi-servidor");
            await _logStreamService.LogAsync(requestId, $"ERRO FATAL: {ex.Message}");

            var errorResponse = new QueryResponse
            {
                RequestId = requestId,
                Status = "error",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Errors = new List<QueryError>
                {
                    new QueryError { Error = ex.Message }
                }
            };

            UpdateStatus(requestId, "error", ex.Message);
            return errorResponse;
        }
        finally
        {
            _cancellationTokens.TryRemove(requestId, out _);
        }
    }

    private async Task<ServerQueryResult> ExecuteQueryOnServerAsync(
        SqlServerInfo server,
        string query,
        string database,
        string requestId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ServerQueryResult
        {
            Servidor = server.Name,
            Status = "running"
        };

        try
        {
            await _logStreamService.LogAsync(requestId, $"[{server.Name}] Conectando...");

            var (resolvedUser, resolvedPassword) = ResolveServerCredentials(server, database);
            var connectionString = server.GetConnectionString(database, resolvedUser, resolvedPassword);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await _logStreamService.LogAsync(requestId, $"[{server.Name}] Executando consulta...");

            await using var command = new SqlCommand(query, connection)
            {
                CommandTimeout = _sqlSettings.DefaultTimeout,
                CommandType = CommandType.Text
            };

            // Executar e ler resultados
            var data = new List<Dictionary<string, object>>();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columnName] = value ?? DBNull.Value;
                }

                data.Add(row);
            }

            stopwatch.Stop();

            result.Status = "success";
            result.Rows = data.Count;
            result.Data = data;
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            await _logStreamService.LogAsync(requestId, $"[{server.Name}] Concluído: {data.Count} linhas em {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (OperationCanceledException)
        {
            result.Status = "cancelled";
            result.Error = "Consulta cancelada";
            await _logStreamService.LogAsync(requestId, $"[{server.Name}] Cancelado");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Status = "error";
            result.Error = ex.Message;
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogError(ex, "Erro ao executar consulta no servidor {Server}", server.Name);
            await _logStreamService.LogAsync(requestId, $"[{server.Name}] ERRO: {ex.Message}");
        }

        return result;
    }


    private static string? ReadEnv(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private (string User, string Password) ResolveServerCredentials(SqlServerInfo server, string? database)
    {
        var isApsDatabase = !string.IsNullOrWhiteSpace(database)
            && database.Contains("aps", StringComparison.OrdinalIgnoreCase);

        var fallbackUser = isApsDatabase
            ? ReadEnv("SQL_USER_APS", "SQL_USER", "SqlServer__Servers__0__User")
            : ReadEnv("SQL_USER", "SQL_USER_APS", "SqlServer__Servers__0__User");

        var fallbackPassword = isApsDatabase
            ? ReadEnv("SQL_PASSWORD_APS", "SQL_PASSWORD", "SqlServer__Servers__0__Password")
            : ReadEnv("SQL_PASSWORD", "SQL_PASSWORD_APS", "SqlServer__Servers__0__Password");

        var configuredUser = _sqlSettings.Servers
            .Select(s => s.User)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));

        var configuredPassword = _sqlSettings.Servers
            .Select(s => s.Password)
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

        var resolvedUser = string.IsNullOrWhiteSpace(server.User)
            ? (fallbackUser ?? configuredUser)
            : server.User;

        var resolvedPassword = string.IsNullOrWhiteSpace(server.Password)
            ? (fallbackPassword ?? configuredPassword)
            : server.Password;

        if (string.IsNullOrWhiteSpace(resolvedUser) || string.IsNullOrWhiteSpace(resolvedPassword))
        {
            throw new InvalidOperationException(
                $"Credenciais SQL não configuradas para o servidor '{server.Name}'. " +
                "Preencha SqlServer:Servers:<index>:User/Password no appsettings ou defina SQL_USER/SQL_PASSWORD (e SQL_USER_APS/SQL_PASSWORD_APS para APS). Em Docker Compose, também aceitamos SqlServer__Servers__0__User/Password e reaproveitamos credenciais já resolvidas em outros servidores configurados."
            );
        }

        return (resolvedUser, resolvedPassword);
    }

    public void CancelQuery(string requestId)
    {
        if (_cancellationTokens.TryGetValue(requestId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Consulta {RequestId} cancelada", requestId);
        }
    }

    public void CancelAllQueries()
    {
        foreach (var cts in _cancellationTokens.Values)
        {
            cts.Cancel();
        }

        _cancellationTokens.Clear();
        _logger.LogInformation("Todas as consultas foram canceladas");
    }

    public ExecutionStatus? GetExecutionStatus(string requestId)
    {
        return _statusCache.TryGetValue(requestId, out var status) ? status : null;
    }

    public QueryResponse? GetQueryResults(string requestId)
    {
        return _resultsCache.TryGetValue(requestId, out var results) ? results : null;
    }

    private void UpdateStatus(string requestId, string status, string? message = null)
    {
        var executionStatus = _statusCache.GetOrAdd(requestId, _ => new ExecutionStatus
        {
            RequestId = requestId,
            StartedAt = DateTime.UtcNow
        });

        executionStatus.Status = status;
        executionStatus.Message = message;

        if (status == "completed" || status == "error" || status == "cancelled")
        {
            executionStatus.CompletedAt = DateTime.UtcNow;
            executionStatus.Progress = 100;
        }
    }

    /// <summary>
    /// Constrói query SQL baseada em tipo predefinido e parâmetros
    /// </summary>
    private string BuildPredefinedQuery(string tipoConsulta, Dictionary<string, object> parametros)
    {
        switch (tipoConsulta.ToLower())
        {
            case "lotes_sem_anexo":
                return BuildLotesSemAnexoQuery(parametros);

            case "aquisicoes":
                return BuildAquisicoesQuery(parametros);

            case "baixas":
                return BuildBaixasQuery(parametros);

            default:
                throw new ArgumentException($"Tipo de consulta não reconhecido: {tipoConsulta}");
        }
    }

    /// <summary>
    /// Sanitiza input para prevenir SQL injection
    /// </summary>
    private static string SanitizeSqlInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        // Escapa aspas simples (padrão SQL) e remove caracteres perigosos
        return input.Replace("'", "''")
                    .Replace(";", "")
                    .Replace("--", "")
                    .Replace("/*", "")
                    .Replace("*/", "")
                    .Replace("xp_", "")
                    .Replace("sp_", "")
                    .Replace("EXEC", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("EXECUTE", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("DROP", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("DELETE", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("TRUNCATE", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();
    }

    private string BuildLotesSemAnexoQuery(Dictionary<string, object> parametros)
    {
        var entidade = SanitizeSqlInput(parametros.GetValueOrDefault("entidade", "").ToString());
        var dataInicio = SanitizeSqlInput(parametros.GetValueOrDefault("data_inicio", "").ToString());
        var dataFim = SanitizeSqlInput(parametros.GetValueOrDefault("data_fim", "").ToString());

        var query = @"
            SELECT
                L.NumeroLote,
                L.DataEmissao,
                L.Entidade,
                L.TipoDocumento,
                L.ValorTotal,
                CASE WHEN A.IdAnexo IS NULL THEN 'SEM ANEXO' ELSE 'COM ANEXO' END AS Status
            FROM
                Lotes L
            LEFT JOIN
                Anexos A ON L.IdLote = A.IdLote
            WHERE
                A.IdAnexo IS NULL";

        if (!string.IsNullOrWhiteSpace(entidade))
        {
            query += $" AND L.Entidade LIKE '%{entidade}%'";
        }

        if (!string.IsNullOrWhiteSpace(dataInicio))
        {
            query += $" AND L.DataEmissao >= '{dataInicio}'";
        }

        if (!string.IsNullOrWhiteSpace(dataFim))
        {
            query += $" AND L.DataEmissao <= '{dataFim}'";
        }

        query += " ORDER BY L.DataEmissao DESC";

        return query;
    }

    private string BuildAquisicoesQuery(Dictionary<string, object> parametros)
    {
        var entidade = SanitizeSqlInput(parametros.GetValueOrDefault("entidade", "").ToString());
        var ano = SanitizeSqlInput(parametros.GetValueOrDefault("ano", "").ToString());

        var query = @"
            SELECT
                A.NumeroAquisicao,
                A.DataAquisicao,
                A.Entidade,
                A.DescricaoBem,
                A.ValorAquisicao,
                A.NumeroPatrimonio,
                A.Fornecedor
            FROM
                Aquisicoes A
            WHERE
                1=1";

        if (!string.IsNullOrWhiteSpace(entidade))
        {
            query += $" AND A.Entidade LIKE '%{entidade}%'";
        }

        if (!string.IsNullOrWhiteSpace(ano))
        {
            query += $" AND YEAR(A.DataAquisicao) = {ano}";
        }

        query += " ORDER BY A.DataAquisicao DESC";

        return query;
    }

    private string BuildBaixasQuery(Dictionary<string, object> parametros)
    {
        var entidade = SanitizeSqlInput(parametros.GetValueOrDefault("entidade", "").ToString());
        var ano = SanitizeSqlInput(parametros.GetValueOrDefault("ano", "").ToString());

        var query = @"
            SELECT
                B.NumeroBaixa,
                B.DataBaixa,
                B.Entidade,
                B.DescricaoBem,
                B.NumeroPatrimonio,
                B.MotivoBaixa,
                B.ValorBaixa
            FROM
                Baixas B
            WHERE
                1=1";

        if (!string.IsNullOrWhiteSpace(entidade))
        {
            query += $" AND B.Entidade LIKE '%{entidade}%'";
        }

        if (!string.IsNullOrWhiteSpace(ano))
        {
            query += $" AND YEAR(B.DataBaixa) = {ano}";
        }

        query += " ORDER BY B.DataBaixa DESC";

        return query;
    }
}
