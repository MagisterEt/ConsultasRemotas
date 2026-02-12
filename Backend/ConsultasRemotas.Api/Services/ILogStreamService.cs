namespace ConsultasRemotas.Api.Services;

public interface ILogStreamService
{
    /// <summary>
    /// Envia uma mensagem de log para clientes conectados
    /// </summary>
    Task LogAsync(string requestId, string message);

    /// <summary>
    /// Obtém histórico de logs de uma requisição
    /// </summary>
    List<string> GetLogs(string requestId);

    /// <summary>
    /// Limpa logs antigos
    /// </summary>
    void CleanupOldLogs();
}
