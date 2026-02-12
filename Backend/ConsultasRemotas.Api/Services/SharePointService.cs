using Azure.Identity;
using ConsultasRemotas.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace ConsultasRemotas.Api.Services;

public class SharePointService : ISharePointService
{
    private readonly SharePointSettings _settings;
    private readonly AzureAdSettings _azureSettings;
    private readonly ILogger<SharePointService> _logger;
    private readonly GraphServiceClient _graphClient;

    public SharePointService(
        IOptions<SharePointSettings> settings,
        IOptions<AzureAdSettings> azureSettings,
        ILogger<SharePointService> logger)
    {
        _settings = settings.Value;
        _azureSettings = azureSettings.Value;
        _logger = logger;

        // Configurar autenticação com Azure AD
        var credential = new ClientSecretCredential(
            _azureSettings.TenantId,
            _azureSettings.ClientId,
            _azureSettings.ClientSecret);

        _graphClient = new GraphServiceClient(credential);
    }

    public async Task<string> UploadFileAsync(byte[] fileContent, string fileName, string? folderPath = null)
    {
        try
        {
            var targetFolder = folderPath ?? _settings.FolderPath;

            // Garantir que a pasta existe
            await EnsureFolderExistsAsync(targetFolder);

            // Fazer upload do arquivo
            using var stream = new MemoryStream(fileContent);

            // Para arquivos pequenos (< 4MB), usar upload simples
            if (fileContent.Length < 4 * 1024 * 1024)
            {
                var uploadedFile = await _graphClient.Drives[_settings.DriveId]
                    .Items["root"]
                    .ItemWithPath($"{targetFolder}/{fileName}")
                    .Content
                    .PutAsync(stream);

                _logger.LogInformation("Arquivo {FileName} enviado para SharePoint com sucesso", fileName);
                return $"https://graph.microsoft.com/v1.0/drives/{_settings.DriveId}/items/{uploadedFile?.Id}";
            }
            else
            {
                // Para arquivos grandes, usar upload em sessão
                return await UploadLargeFileAsync(stream, fileName, targetFolder);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fazer upload do arquivo {FileName} para SharePoint", fileName);
            throw;
        }
    }

    public async Task<bool> FolderExistsAsync(string folderPath)
    {
        try
        {
            var folder = await _graphClient.Drives[_settings.DriveId]
                .Items["root"]
                .ItemWithPath(folderPath)
                .GetAsync();

            return folder?.Folder != null;
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
        {
            return false;
        }
    }

    public async Task CreateFolderAsync(string folderPath)
    {
        try
        {
            var pathParts = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";

            foreach (var part in pathParts)
            {
                var parentPath = string.IsNullOrEmpty(currentPath) ? "root" : $"root:/{currentPath}";
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";

                // Verificar se a pasta já existe
                if (!await FolderExistsAsync(currentPath))
                {
                    var driveItem = new DriveItem
                    {
                        Name = part,
                        Folder = new Folder(),
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "@microsoft.graph.conflictBehavior", "rename" }
                        }
                    };

                    await _graphClient.Drives[_settings.DriveId]
                        .Items[parentPath]
                        .Children
                        .PostAsync(driveItem);

                    _logger.LogInformation("Pasta {FolderPath} criada no SharePoint", currentPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar pasta {FolderPath} no SharePoint", folderPath);
            throw;
        }
    }

    private async Task EnsureFolderExistsAsync(string folderPath)
    {
        if (!await FolderExistsAsync(folderPath))
        {
            await CreateFolderAsync(folderPath);
        }
    }

    private async Task<string> UploadLargeFileAsync(Stream fileStream, string fileName, string folderPath)
    {
        try
        {
            // Criar sessão de upload
            var uploadSession = await _graphClient.Drives[_settings.DriveId]
                .Items["root"]
                .ItemWithPath($"{folderPath}/{fileName}")
                .CreateUploadSession
                .PostAsync(new Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession.CreateUploadSessionPostRequestBody
                {
                    Item = new DriveItemUploadableProperties
                    {
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "@microsoft.graph.conflictBehavior", "replace" }
                        }
                    }
                });

            if (uploadSession?.UploadUrl == null)
            {
                throw new InvalidOperationException("Não foi possível criar sessão de upload");
            }

            // Upload em chunks de 5MB
            const int chunkSize = 5 * 1024 * 1024;
            var buffer = new byte[chunkSize];
            long position = 0;
            int bytesRead;

            using var httpClient = new HttpClient();

            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, chunkSize)) > 0)
            {
                var content = new ByteArrayContent(buffer, 0, bytesRead);
                content.Headers.Add("Content-Range", $"bytes {position}-{position + bytesRead - 1}/{fileStream.Length}");

                var response = await httpClient.PutAsync(uploadSession.UploadUrl, content);
                response.EnsureSuccessStatusCode();

                position += bytesRead;
            }

            _logger.LogInformation("Arquivo grande {FileName} enviado para SharePoint com sucesso", fileName);
            return uploadSession.UploadUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fazer upload de arquivo grande {FileName}", fileName);
            throw;
        }
    }
}
