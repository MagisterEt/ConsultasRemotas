namespace ConsultasRemotas.Api.Configuration;

public class SqlServerSettings
{
    public int DefaultTimeout { get; set; } = 180;
    public int ConnectionTimeout { get; set; } = 60;
    public int MaxConcurrentQueries { get; set; } = 16;
    public bool TrustServerCertificate { get; set; } = true;
    public bool Encrypt { get; set; } = false;
    public List<SqlServerInfo> Servers { get; set; } = new();
    public List<string> Databases { get; set; } = new();
}

public class SqlServerInfo
{
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 1433;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DefaultDatabase { get; set; } = "AASI";

    public string GetConnectionString(string? database = null)
    {
        var db = database ?? DefaultDatabase;
        return $"Server={Host},{Port};" +
               $"Database={db};" +
               $"User Id={User};" +
               $"Password={Password};" +
               $"TrustServerCertificate=True;" +
               $"Encrypt=False;" +
               $"Connection Timeout=60;" +
               $"Command Timeout=180;";
    }
}
