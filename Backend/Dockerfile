# ========================================
# MULTI-STAGE DOCKERFILE OTIMIZADO PARA UBUNTU/LINUX
# ========================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR /src

# Copiar apenas os arquivos de projeto primeiro (melhor cache)
COPY ConsultasRemotas.Api/*.csproj ./ConsultasRemotas.Api/
RUN dotnet restore "ConsultasRemotas.Api/ConsultasRemotas.Api.csproj"

# Copiar o resto dos arquivos
COPY ConsultasRemotas.Api/. ./ConsultasRemotas.Api/

# Garantir que wwwroot seja copiado (frontend)
COPY ConsultasRemotas.Api/wwwroot ./ConsultasRemotas.Api/wwwroot

# Build otimizado
WORKDIR /src/ConsultasRemotas.Api
RUN dotnet build "ConsultasRemotas.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "ConsultasRemotas.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --runtime linux-x64 \
    --self-contained false \
    /p:PublishTrimmed=false

# Stage 3: Runtime (Ubuntu Jammy - 22.04 LTS)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS final

# Instalar dependências necessárias para SQL Server
USER root

# Instalar ODBC Driver para SQL Server (otimizado para Ubuntu)
RUN apt-get update && apt-get install -y \
    curl \
    gnupg2 \
    apt-transport-https \
    ca-certificates \
    && curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - \
    && curl https://packages.microsoft.com/config/ubuntu/22.04/prod.list > /etc/apt/sources.list.d/mssql-release.list \
    && apt-get update \
    && ACCEPT_EULA=Y apt-get install -y \
    msodbcsql18 \
    unixodbc-dev \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Criar usuário não-root para segurança
RUN useradd -m -u 1000 appuser

# Criar diretórios necessários
WORKDIR /app
RUN mkdir -p /app/logs && chown -R appuser:appuser /app

# Copiar arquivos publicados
COPY --from=publish /app/publish .

# Mudar para usuário não-root
USER appuser

# Variáveis de ambiente para otimização
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_gcServer=1 \
    DOTNET_GCHeapHardLimit=1C00000000 \
    DOTNET_ThreadPool_UnfairSemaphoreSpinLimit=6

# Expor porta
EXPOSE 8080

# Health check (curl já instalado no stage anterior)
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Executar aplicação
ENTRYPOINT ["dotnet", "ConsultasRemotas.Api.dll"]
