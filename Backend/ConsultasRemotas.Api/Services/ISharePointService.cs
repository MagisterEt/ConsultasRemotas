namespace ConsultasRemotas.Api.Services;

public interface ISharePointService
{
    /// <summary>
    /// Faz upload de um arquivo para o SharePoint
    /// </summary>
    Task<string> UploadFileAsync(byte[] fileContent, string fileName, string? folderPath = null);

    /// <summary>
    /// Verifica se a pasta existe no SharePoint
    /// </summary>
    Task<bool> FolderExistsAsync(string folderPath);

    /// <summary>
    /// Cria uma pasta no SharePoint
    /// </summary>
    Task CreateFolderAsync(string folderPath);
}
