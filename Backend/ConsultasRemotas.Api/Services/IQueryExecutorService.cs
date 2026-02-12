using ConsultasRemotas.Api.Models;

namespace ConsultasRemotas.Api.Services;

public interface IQueryExecutorService
{
    /// <summary>
    /// Executa uma consulta em um único servidor
    /// </summary>
    Task<QueryResponse> ExecuteQueryAsync(QueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa uma consulta em múltiplos servidores simultaneamente
    /// </summary>
    Task<QueryResponse> ExecuteMultiServerQueryAsync(MultiServerQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela uma consulta em execução
    /// </summary>
    void CancelQuery(string requestId);

    /// <summary>
    /// Cancela todas as consultas em execução
    /// </summary>
    void CancelAllQueries();

    /// <summary>
    /// Obtém o status de uma consulta
    /// </summary>
    ExecutionStatus? GetExecutionStatus(string requestId);

    /// <summary>
    /// Obtém os resultados de uma consulta executada
    /// </summary>
    QueryResponse? GetQueryResults(string requestId);
}
