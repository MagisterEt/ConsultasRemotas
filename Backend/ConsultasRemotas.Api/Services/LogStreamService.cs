using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace ConsultasRemotas.Api.Services;

public class LogStreamService : ILogStreamService
{
    private readonly IHubContext<LogHub> _hubContext;
    private readonly ILogger<LogStreamService> _logger;

    // Cache de logs por request ID
    private readonly ConcurrentDictionary<string, List<LogEntry>> _logsCache = new();

    public LogStreamService(
        IHubContext<LogHub> hubContext,
        ILogger<LogStreamService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;

        // Iniciar limpeza periódica de logs antigos
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(10));
                CleanupOldLogs();
            }
        });
    }

    public async Task LogAsync(string requestId, string message)
    {
        var logEntry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Message = message
        };

        // Adicionar ao cache
        var logs = _logsCache.GetOrAdd(requestId, _ => new List<LogEntry>());
        logs.Add(logEntry);

        // Enviar via SignalR para clientes conectados
        try
        {
            await _hubContext.Clients.Group(requestId).SendAsync("ReceiveLog", new
            {
                timestamp = logEntry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                message = logEntry.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar log via SignalR");
        }
    }

    public List<string> GetLogs(string requestId)
    {
        if (_logsCache.TryGetValue(requestId, out var logs))
        {
            return logs.Select(l => $"[{l.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {l.Message}").ToList();
        }

        return new List<string>();
    }

    public void CleanupOldLogs()
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-10);
        var keysToRemove = new List<string>();

        foreach (var kvp in _logsCache)
        {
            var oldestLog = kvp.Value.FirstOrDefault();
            if (oldestLog != null && oldestLog.Timestamp < cutoffTime)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _logsCache.TryRemove(key, out _);
        }

        if (keysToRemove.Any())
        {
            _logger.LogInformation("Limpou {Count} logs antigos", keysToRemove.Count);
        }
    }

    private class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

/// <summary>
/// SignalR Hub para streaming de logs em tempo real
/// </summary>
public class LogHub : Hub
{
    private readonly ILogger<LogHub> _logger;

    public LogHub(ILogger<LogHub> logger)
    {
        _logger = logger;
    }

    public async Task SubscribeToRequest(string requestId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, requestId);
        _logger.LogDebug("Cliente {ConnectionId} inscrito para logs de {RequestId}", Context.ConnectionId, requestId);
    }

    public async Task UnsubscribeFromRequest(string requestId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, requestId);
        _logger.LogDebug("Cliente {ConnectionId} desinscrito de logs de {RequestId}", Context.ConnectionId, requestId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Cliente conectado: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Cliente desconectado: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
