using ConsultasRemotas.Api.Models;
using ConsultasRemotas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasRemotas.Api.Controllers;

[ApiController]
[Route("api")]
public class QueryController : ControllerBase
{
    private readonly IQueryExecutorService _queryExecutor;
    private readonly IExportService _exportService;
    private readonly ISharePointService _sharePointService;
    private readonly ILogStreamService _logStreamService;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        IQueryExecutorService queryExecutor,
        IExportService exportService,
        ISharePointService sharePointService,
        ILogStreamService logStreamService,
        ILogger<QueryController> logger)
    {
        _queryExecutor = queryExecutor;
        _exportService = exportService;
        _sharePointService = sharePointService;
        _logStreamService = logStreamService;
        _logger = logger;
    }

    /// <summary>
    /// Retorna lista de consultas disponíveis
    /// </summary>
    [HttpGet("consultas_disponiveis")]
    public IActionResult GetAvailableQueries()
    {
        var consultas = new List<ConsultaDisponivel>
        {
            new ConsultaDisponivel
            {
                Id = "lotes_sem_anexo",
                Nome = "Lotes sem Anexo",
                Descricao = "Busca documentos sem anexos em todos os servidores",
                Parametros = new List<ParametroConsulta>
                {
                    new ParametroConsulta { Nome = "entidade", Tipo = "string", Obrigatorio = true },
                    new ParametroConsulta { Nome = "data_inicio", Tipo = "date", Obrigatorio = false },
                    new ParametroConsulta { Nome = "data_fim", Tipo = "date", Obrigatorio = false }
                }
            },
            new ConsultaDisponivel
            {
                Id = "aquisicoes",
                Nome = "Aquisições de Bens",
                Descricao = "Consulta aquisições de bens patrimoniais por entidade e período",
                Parametros = new List<ParametroConsulta>
                {
                    new ParametroConsulta { Nome = "entidade", Tipo = "string", Obrigatorio = true },
                    new ParametroConsulta { Nome = "ano", Tipo = "int", Obrigatorio = true }
                }
            },
            new ConsultaDisponivel
            {
                Id = "baixas",
                Nome = "Baixas de Bens",
                Descricao = "Consulta baixas de bens patrimoniais",
                Parametros = new List<ParametroConsulta>
                {
                    new ParametroConsulta { Nome = "entidade", Tipo = "string", Obrigatorio = true },
                    new ParametroConsulta { Nome = "ano", Tipo = "int", Obrigatorio = true }
                }
            }
        };

        return Ok(consultas);
    }

    /// <summary>
    /// Executa consulta em um único servidor
    /// </summary>
    [HttpPost("consultar")]
    public async Task<IActionResult> ExecuteQuery([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { error = "Query é obrigatória" });
            }

            var result = await _queryExecutor.ExecuteQueryAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar consulta");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Executa consulta em múltiplos servidores
    /// </summary>
    [HttpPost("consultar_multi")]
    public async Task<IActionResult> ExecuteMultiServerQuery(
        [FromBody] MultiServerQueryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { error = "Query é obrigatória" });
            }

            var result = await _queryExecutor.ExecuteMultiServerQueryAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar consulta multi-servidor");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtém logs de uma requisição
    /// </summary>
    [HttpGet("logs/{requestId}")]
    public IActionResult GetLogs(string requestId)
    {
        var logs = _logStreamService.GetLogs(requestId);
        return Ok(new { request_id = requestId, logs });
    }

    /// <summary>
    /// Cancela uma consulta específica
    /// </summary>
    [HttpPost("cancelar/{requestId}")]
    public IActionResult CancelQuery(string requestId)
    {
        _queryExecutor.CancelQuery(requestId);
        return Ok(new { message = "Consulta cancelada", request_id = requestId });
    }

    /// <summary>
    /// Cancela todas as consultas em execução
    /// </summary>
    [HttpPost("cancelar_todas")]
    public IActionResult CancelAllQueries()
    {
        _queryExecutor.CancelAllQueries();
        return Ok(new { message = "Todas as consultas foram canceladas" });
    }

    /// <summary>
    /// Obtém o resultado de uma consulta executada
    /// </summary>
    [HttpGet("resultado/{requestId}")]
    public IActionResult GetQueryResult(string requestId)
    {
        var result = _queryExecutor.GetQueryResults(requestId);

        if (result == null)
        {
            return NotFound(new { error = "Resultado não encontrado", request_id = requestId });
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtém o status de uma consulta
    /// </summary>
    [HttpGet("status/{requestId}")]
    public IActionResult GetStatus(string requestId)
    {
        var status = _queryExecutor.GetExecutionStatus(requestId);

        if (status == null)
        {
            return NotFound(new { error = "Status não encontrado", request_id = requestId });
        }

        return Ok(status);
    }

    /// <summary>
    /// Exporta resultados em diferentes formatos
    /// </summary>
    [HttpPost("exportar/{formato}")]
    public async Task<IActionResult> ExportResults(string formato, [FromBody] ExportRequest request)
    {
        try
        {
            var result = _queryExecutor.GetQueryResults(request.RequestId);

            if (result == null)
            {
                return NotFound(new { error = "Resultado não encontrado" });
            }

            byte[] fileContent;
            var fileName = _exportService.GenerateFileName(formato, request.NomeArquivo);

            switch (formato.ToLower())
            {
                case "csv":
                    fileContent = await _exportService.ExportToCsvAsync(result.Results);
                    break;

                case "xlsx":
                    fileContent = await _exportService.ExportToExcelAsync(result.Results, "Consulta");
                    break;

                default:
                    return BadRequest(new { error = "Formato não suportado. Use: csv ou xlsx" });
            }

            var contentType = _exportService.GetContentType(formato);
            return File(fileContent, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao exportar resultados");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Faz upload de resultados para o SharePoint
    /// </summary>
    [HttpPost("upload_sharepoint")]
    public async Task<IActionResult> UploadToSharePoint([FromBody] SharePointUploadRequest request)
    {
        try
        {
            var result = _queryExecutor.GetQueryResults(request.RequestId);

            if (result == null)
            {
                return NotFound(new { error = "Resultado não encontrado" });
            }

            byte[] fileContent;
            var fileName = request.NomeArquivo;

            if (!fileName.EndsWith($".{request.Formato}"))
            {
                fileName = $"{fileName}.{request.Formato}";
            }

            switch (request.Formato.ToLower())
            {
                case "csv":
                    fileContent = await _exportService.ExportToCsvAsync(result.Results);
                    break;

                case "xlsx":
                    fileContent = await _exportService.ExportToExcelAsync(result.Results, "Consulta");
                    break;

                default:
                    return BadRequest(new { error = "Formato não suportado. Use: csv ou xlsx" });
            }

            var url = await _sharePointService.UploadFileAsync(fileContent, fileName);

            return Ok(new
            {
                message = "Arquivo enviado para SharePoint com sucesso",
                file_name = fileName,
                url
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fazer upload para SharePoint");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Health check da API
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetApiStatus()
    {
        return Ok(new
        {
            status = "online",
            timestamp = DateTime.UtcNow,
            version = "2.0.0",
            platform = "ASP.NET Core 8.0",
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        });
    }
}
