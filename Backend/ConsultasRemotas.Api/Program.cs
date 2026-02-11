using ConsultasRemotas.Api.Configuration;
using ConsultasRemotas.Api.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// CONFIGURAÇÃO DE LOGGING COM SERILOG
// ========================================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/consultas-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ========================================
// CONFIGURAÇÃO KESTREL OTIMIZADA PARA UBUNTU
// ========================================
builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    var port = context.Configuration.GetValue<int>("Server:Port", 8080);

    serverOptions.Listen(IPAddress.Any, port, listenOptions =>
    {
        // Otimizações para Linux/Ubuntu
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });

    // Limites otimizados para consultas grandes
    serverOptions.Limits.MaxConcurrentConnections = 1000;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 1000;
    serverOptions.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);

    // Thread pool otimizado para múltiplas consultas SQL simultâneas
    serverOptions.Limits.MaxRequestHeaderCount = 100;
});

// ========================================
// CONFIGURAÇÃO DE SERVIÇOS
// ========================================

// Carregar configurações
builder.Services.Configure<SqlServerSettings>(builder.Configuration.GetSection("SqlServer"));
builder.Services.Configure<SharePointSettings>(builder.Configuration.GetSection("SharePoint"));
builder.Services.Configure<AzureAdSettings>(builder.Configuration.GetSection("AzureAd"));

// Adicionar controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.WriteIndented = false; // Compacto para performance
    });

// SignalR para logs em tempo real
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1 MB
    options.StreamBufferCapacity = 50;
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "ConsultasRemotas API",
        Version = "v1",
        Description = "API para consultas SQL distribuídas em múltiplos servidores"
    });
});

// CORS configurável
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                             ?? new[] { "*" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Memory Cache para resultados
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024; // Limitar cache
    options.CompactionPercentage = 0.25;
});

// Health Checks
builder.Services.AddHealthChecks();

// ========================================
// REGISTRAR SERVIÇOS CUSTOMIZADOS
// ========================================
builder.Services.AddSingleton<IQueryExecutorService, QueryExecutorService>();
builder.Services.AddSingleton<ISharePointService, SharePointService>();
builder.Services.AddSingleton<IExportService, ExportService>();
builder.Services.AddSingleton<ILogStreamService, LogStreamService>();

// HttpClient para Graph API
builder.Services.AddHttpClient("GraphAPI", client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
    client.Timeout = TimeSpan.FromMinutes(5);
});

// ========================================
// BUILD DA APLICAÇÃO
// ========================================
var app = builder.Build();

// ========================================
// CONFIGURAÇÃO DO PIPELINE HTTP
// ========================================

// Swagger em todos os ambientes (pode remover em produção se necessário)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConsultasRemotas API v1");
    c.RoutePrefix = "swagger";
});

// CORS
app.UseCors("DefaultPolicy");

// Servir arquivos estáticos (HTML/CSS/JS frontend)
app.UseStaticFiles();

// Routing
app.UseRouting();

// Health check endpoint
app.MapHealthChecks("/health");

// Controllers
app.MapControllers();

// SignalR Hub para logs
app.MapHub<LogHub>("/hubs/logs");

// Fallback para servir o index.html
app.MapFallbackToFile("index.html");

// ========================================
// LOG DE INICIALIZAÇÃO
// ========================================
Log.Information("==========================================================");
Log.Information("ConsultasRemotas API iniciando...");
Log.Information("Ambiente: {Environment}", app.Environment.EnvironmentName);
Log.Information("Porta: {Port}", builder.Configuration.GetValue<int>("Server:Port", 8080));
Log.Information("Runtime: {Runtime}", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
Log.Information("SO: {OS}", System.Runtime.InteropServices.RuntimeInformation.OSDescription);
Log.Information("==========================================================");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicação terminou inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}
