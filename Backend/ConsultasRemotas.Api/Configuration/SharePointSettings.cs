namespace ConsultasRemotas.Api.Configuration;

public class SharePointSettings
{
    public string SiteId { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string FolderPath { get; set; } = "Consultas";
}

public class AzureAdSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public string Authority => $"https://login.microsoftonline.com/{TenantId}";
    public string[] Scopes => new[] { "https://graph.microsoft.com/.default" };
}
