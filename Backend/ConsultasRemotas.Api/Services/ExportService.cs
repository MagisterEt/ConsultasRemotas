using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;

namespace ConsultasRemotas.Api.Services;

public class ExportService : IExportService
{
    private readonly ILogger<ExportService> _logger;

    public ExportService(ILogger<ExportService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> ExportToCsvAsync(List<Dictionary<string, object>> data)
    {
        if (!data.Any())
        {
            return Encoding.UTF8.GetBytes("No data");
        }

        await using var memoryStream = new MemoryStream();
        await using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            Encoding = Encoding.UTF8,
            HasHeaderRecord = true
        });

        // Escrever header
        var headers = data.First().Keys.ToList();
        foreach (var header in headers)
        {
            csv.WriteField(header);
        }
        await csv.NextRecordAsync();

        // Escrever dados
        foreach (var row in data)
        {
            foreach (var header in headers)
            {
                var value = row.ContainsKey(header) ? row[header] : null;
                csv.WriteField(ConvertToString(value));
            }
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync();
        return memoryStream.ToArray();
    }

    public async Task<byte[]> ExportToExcelAsync(List<Dictionary<string, object>> data, string? sheetName = null)
    {
        if (!data.Any())
        {
            throw new InvalidOperationException("Não há dados para exportar");
        }

        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName ?? "Consulta");

            var headers = data.First().Keys.ToList();

            // Escrever headers
            for (int i = 0; i < headers.Count; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            // Escrever dados
            for (int rowIndex = 0; rowIndex < data.Count; rowIndex++)
            {
                var row = data[rowIndex];
                for (int colIndex = 0; colIndex < headers.Count; colIndex++)
                {
                    var header = headers[colIndex];
                    var value = row.ContainsKey(header) ? row[header] : null;

                    var cell = worksheet.Cell(rowIndex + 2, colIndex + 1);

                    if (value == null || value == DBNull.Value)
                    {
                        cell.Value = "";
                    }
                    else if (value is DateTime dateTime)
                    {
                        cell.Value = dateTime;
                        cell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";
                    }
                    else if (IsNumeric(value))
                    {
                        cell.Value = Convert.ToDouble(value);
                    }
                    else
                    {
                        cell.Value = value.ToString();
                    }
                }
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        });
    }

    public string GetContentType(string format)
    {
        return format.ToLower() switch
        {
            "csv" => "text/csv",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
    }

    public string GenerateFileName(string format, string? baseName = null)
    {
        var name = baseName ?? "consulta";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{name}_{timestamp}.{format.ToLower()}";
    }

    private string ConvertToString(object? value)
    {
        if (value == null || value == DBNull.Value)
        {
            return "";
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return value.ToString() ?? "";
    }

    private bool IsNumeric(object value)
    {
        return value is int || value is long || value is float || value is double || value is decimal;
    }
}
