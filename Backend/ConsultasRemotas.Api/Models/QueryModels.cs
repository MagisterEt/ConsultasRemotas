using System.Text.Json.Serialization;

namespace ConsultasRemotas.Api.Models;

/// <summary>
/// Request para executar consulta em um único servidor
/// </summary>
public class QueryRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("servidor")]
    public string? Servidor { get; set; }

    [JsonPropertyName("banco")]
    public string Banco { get; set; } = "AASI";

    [JsonPropertyName("parametros")]
    public Dictionary<string, object>? Parametros { get; set; }
}

/// <summary>
/// Request para executar consulta em múltiplos servidores
/// </summary>
public class MultiServerQueryRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("tipo_consulta")]
    public string? TipoConsulta { get; set; }

    [JsonPropertyName("banco")]
    public string Banco { get; set; } = "AASI";

    [JsonPropertyName("parametros")]
    public Dictionary<string, object>? Parametros { get; set; }

    [JsonPropertyName("servidores")]
    public List<string>? Servidores { get; set; }
}

/// <summary>
/// Resposta de consulta com resultados
/// </summary>
public class QueryResponse
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "completed";

    [JsonPropertyName("total_rows")]
    public int TotalRows { get; set; }

    [JsonPropertyName("execution_time_ms")]
    public long ExecutionTimeMs { get; set; }

    [JsonPropertyName("results")]
    public List<Dictionary<string, object>> Results { get; set; } = new();

    [JsonPropertyName("errors")]
    public List<QueryError> Errors { get; set; } = new();

    [JsonPropertyName("server_results")]
    public Dictionary<string, ServerQueryResult>? ServerResults { get; set; }
}

/// <summary>
/// Resultado de consulta de um servidor específico
/// </summary>
public class ServerQueryResult
{
    [JsonPropertyName("servidor")]
    public string Servidor { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "success";

    [JsonPropertyName("rows")]
    public int Rows { get; set; }

    [JsonPropertyName("execution_time_ms")]
    public long ExecutionTimeMs { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("data")]
    public List<Dictionary<string, object>>? Data { get; set; }
}

/// <summary>
/// Erro durante execução de consulta
/// </summary>
public class QueryError
{
    [JsonPropertyName("servidor")]
    public string? Servidor { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request para exportação
/// </summary>
public class ExportRequest
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("formato")]
    public string Formato { get; set; } = "csv";

    [JsonPropertyName("nome_arquivo")]
    public string? NomeArquivo { get; set; }
}

/// <summary>
/// Request para upload no SharePoint
/// </summary>
public class SharePointUploadRequest
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("nome_arquivo")]
    public string NomeArquivo { get; set; } = string.Empty;

    [JsonPropertyName("formato")]
    public string Formato { get; set; } = "xlsx";
}

/// <summary>
/// Informações sobre consultas disponíveis
/// </summary>
public class ConsultaDisponivel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = string.Empty;

    [JsonPropertyName("parametros")]
    public List<ParametroConsulta> Parametros { get; set; } = new();
}

/// <summary>
/// Parâmetro de consulta
/// </summary>
public class ParametroConsulta
{
    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = "string";

    [JsonPropertyName("obrigatorio")]
    public bool Obrigatorio { get; set; } = false;

    [JsonPropertyName("descricao")]
    public string? Descricao { get; set; }
}

/// <summary>
/// Status de execução
/// </summary>
public class ExecutionStatus
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("progress")]
    public int Progress { get; set; } = 0;

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
