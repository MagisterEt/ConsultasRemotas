using System.Text.Json.Serialization;

namespace ConsultasRemotas.Api.Models;

public class ExecutarConsultaPreDefinidaRequest
{
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("entidade")]
    public string? Entidade { get; set; }

    [JsonPropertyName("ano")]
    public int? Ano { get; set; }

    [JsonPropertyName("periodo")]
    public int? Periodo { get; set; }

    [JsonPropertyName("meses_atras")]
    public int? MesesAtras { get; set; }

    [JsonPropertyName("data_limite")]
    public string? DataLimite { get; set; }
}

public class ConsultaPreDefinidaInfo
{
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = string.Empty;

    [JsonPropertyName("tipo_execucao")]
    public string TipoExecucao { get; set; } = string.Empty;

    [JsonPropertyName("requer_entidade")]
    public bool RequerEntidade { get; set; }

    [JsonPropertyName("requer_periodo")]
    public bool RequerPeriodo { get; set; }

    [JsonPropertyName("requer_ano")]
    public bool RequerAno { get; set; }
}

public class ConsultaPreDefinidaResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "sucesso";

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("dados")]
    public List<Dictionary<string, object>> Dados { get; set; } = new();

    [JsonPropertyName("colunas")]
    public List<string> Colunas { get; set; } = new();

    [JsonPropertyName("total_linhas")]
    public int TotalLinhas { get; set; }

    [JsonPropertyName("tempo_segundos")]
    public double TempoSegundos { get; set; }

    [JsonPropertyName("avisos")]
    public List<string> Avisos { get; set; } = new();
}
