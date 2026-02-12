using ConsultasRemotas.Api.Models;

namespace ConsultasRemotas.Api.Services;

public interface IExportService
{
    /// <summary>
    /// Exporta dados para CSV
    /// </summary>
    Task<byte[]> ExportToCsvAsync(List<Dictionary<string, object>> data);

    /// <summary>
    /// Exporta dados para Excel (XLSX)
    /// </summary>
    Task<byte[]> ExportToExcelAsync(List<Dictionary<string, object>> data, string? sheetName = null);

    /// <summary>
    /// Determina o content type baseado no formato
    /// </summary>
    string GetContentType(string format);

    /// <summary>
    /// Gera nome de arquivo com timestamp
    /// </summary>
    string GenerateFileName(string format, string? baseName = null);
}
