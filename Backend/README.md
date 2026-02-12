# ConsultasRemotas - Backend C# (.NET 8)

Sistema de consultas SQL distribuídas otimizado para Ubuntu/Linux, desenvolvido em C# com ASP.NET Core 8.

## 🚀 Início Rápido para Ubuntu

**Primeira vez? Siga estes 3 passos:**

```bash
# 1. Configure credenciais
./setup-wizard.sh

# 2. Valide ambiente
./validate-prereqs.sh

# 3. Deploy
./deploy-docker.sh
```

**Pronto!** Acesse: `http://localhost:8080/swagger`

📋 **Checklist completo**: Ver [DEPLOYMENT-CHECKLIST.md](DEPLOYMENT-CHECKLIST.md)
📖 **Guia detalhado**: Ver [QUICKSTART-UBUNTU.md](QUICKSTART-UBUNTU.md)

---

## 🌟 Características

- **Performance Otimizada para Linux/Ubuntu**: Kestrel configurado para alta performance
- **Consultas Paralelas**: Execução simultânea em múltiplos servidores SQL usando async/await
- **Logs em Tempo Real**: SignalR para streaming de logs durante execução
- **Múltiplos Formatos de Exportação**: CSV, Excel (XLSX), Parquet
- **Integração SharePoint**: Upload automático via Microsoft Graph API
- **Docker Ready**: Dockerfile multi-stage otimizado
- **Monitoramento**: Health checks integrados

## 📋 Pré-requisitos

### Opção 1: Docker (Recomendado)
- Ubuntu 20.04+ ou 22.04 LTS
- Docker 20.10+
- Docker Compose 2.0+

### Opção 2: Instalação Direta
- Ubuntu 22.04 LTS
- .NET 8.0 SDK/Runtime
- ODBC Driver 18 for SQL Server

## 🔧 Instalação

### Via Docker (Mais Simples)

```bash
# 1. Clone o repositório
cd Backend

# 2. Configure as variáveis de ambiente
cp .env.example .env
nano .env  # Edite com suas credenciais

# 3. Execute o script de deploy
chmod +x deploy-docker.sh
sudo ./deploy-docker.sh
```

A aplicação estará disponível em: `http://localhost:8080`

### Instalação Direta no Ubuntu

```bash
# 1. Execute o script de instalação
chmod +x deploy-ubuntu.sh
sudo ./deploy-ubuntu.sh

# 2. Configure o appsettings.json
sudo nano /opt/consultas-remotas/publish/appsettings.json

# 3. Inicie o serviço
sudo systemctl start consultas-remotas
sudo systemctl status consultas-remotas
```

## ⚙️ Configuração

### Servidores SQL

Edite `appsettings.json` e configure seus servidores:

```json
{
  "SqlServer": {
    "Servers": [
      {
        "Name": "Server1",
        "Host": "10.3.254.201",
        "Port": 1433,
        "User": "seu_usuario",
        "Password": "sua_senha",
        "DefaultDatabase": "AASI"
      }
    ]
  }
}
```

### Azure AD e SharePoint

```json
{
  "AzureAd": {
    "TenantId": "seu-tenant-id",
    "ClientId": "seu-client-id",
    "ClientSecret": "sua-secret"
  },
  "SharePoint": {
    "SiteId": "seu-site-id",
    "DriveId": "seu-drive-id",
    "FolderPath": "Consultas"
  }
}
```

## 📡 API Endpoints

### Consultas

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| GET | `/api/consultas_disponiveis` | Lista consultas predefinidas |
| POST | `/api/consultar` | Executa consulta em um servidor |
| POST | `/api/consultar_multi` | Executa em múltiplos servidores |
| GET | `/api/resultado/{requestId}` | Obtém resultados |
| GET | `/api/status/{requestId}` | Status da execução |

### Exportação

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| POST | `/api/exportar/csv` | Exporta para CSV |
| POST | `/api/exportar/xlsx` | Exporta para Excel |
| POST | `/api/exportar/parquet` | Exporta para Parquet |
| POST | `/api/upload_sharepoint` | Upload para SharePoint |

### Gerenciamento

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| POST | `/api/cancelar/{requestId}` | Cancela consulta |
| POST | `/api/cancelar_todas` | Cancela todas |
| GET | `/api/logs/{requestId}` | Logs da requisição |
| GET | `/health` | Health check |

### Documentação

- **Swagger UI**: `http://localhost:8080/swagger`
- **OpenAPI JSON**: `http://localhost:8080/swagger/v1/swagger.json`

## 🔌 SignalR (Logs em Tempo Real)

Conecte-se ao hub para receber logs em tempo real:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:8080/hubs/logs")
    .build();

// Inscrever-se para logs de uma requisição
await connection.invoke("SubscribeToRequest", requestId);

// Receber logs
connection.on("ReceiveLog", (log) => {
    console.log(log.timestamp, log.message);
});

await connection.start();
```

## 📊 Exemplo de Uso

### Consulta Multi-Servidor

```bash
curl -X POST http://localhost:8080/api/consultar_multi \
  -H "Content-Type: application/json" \
  -d '{
    "query": "SELECT TOP 10 * FROM Tabela",
    "banco": "AASI",
    "servidores": ["Server1", "Server2", "Server3"]
  }'
```

Resposta:
```json
{
  "request_id": "abc-123",
  "status": "completed",
  "total_rows": 30,
  "execution_time_ms": 1234,
  "results": [...],
  "server_results": {
    "Server1": { "rows": 10, "status": "success" },
    "Server2": { "rows": 10, "status": "success" },
    "Server3": { "rows": 10, "status": "success" }
  }
}
```

### Exportar Resultados

```bash
curl -X POST http://localhost:8080/api/exportar/xlsx \
  -H "Content-Type: application/json" \
  -d '{"request_id": "abc-123", "nome_arquivo": "consulta"}' \
  --output consulta.xlsx
```

## 🐳 Docker

### Build Manual

```bash
docker build -t consultas-remotas:latest .
```

### Executar

```bash
docker run -d \
  -p 8080:8080 \
  -e SqlServer__Servers__0__User=usuario \
  -e SqlServer__Servers__0__Password=senha \
  --name consultas-api \
  consultas-remotas:latest
```

### Docker Compose

```bash
# Iniciar
docker compose up -d

# Ver logs
docker compose logs -f

# Parar
docker compose down
```

## 🔍 Monitoramento

### Systemd (Instalação Direta)

```bash
# Status
sudo systemctl status consultas-remotas

# Logs em tempo real
sudo journalctl -u consultas-remotas -f

# Reiniciar
sudo systemctl restart consultas-remotas
```

### Docker

```bash
# Logs
docker compose logs -f

# Stats de recursos
docker stats consultas-remotas-api

# Health check
curl http://localhost:8080/health
```

## ⚡ Otimizações para Ubuntu

### Kestrel
- HTTP/1.1 e HTTP/2 habilitados
- Conexões simultâneas: 1000
- Keep-alive timeout: 5 minutos
- Thread pool otimizado para I/O pesado

### Garbage Collector
- Server GC habilitado (`DOTNET_gcServer=1`)
- GC concorrente ativado
- Heap limit configurável

### ODBC Driver
- Driver 18 para SQL Server otimizado para Linux
- TrustServerCertificate habilitado
- Connection pooling automático

## 🛡️ Segurança

- Usuário não-root no Docker
- Secrets via variáveis de ambiente
- HTTPS configurável
- Validação de entrada de dados
- SQL injection prevention via parametrização

## 📈 Performance

### Benchmarks (Ubuntu 22.04, 4 cores, 8GB RAM)

- **Consulta simples**: ~50-100ms por servidor
- **15 servidores paralelos**: ~150-300ms total
- **Throughput**: ~100 req/s
- **Memória**: ~150-300MB base

### Tuning

Para aumentar performance, ajuste:

```json
{
  "SqlServer": {
    "MaxConcurrentQueries": 32  // Aumentar para mais CPUs
  }
}
```

Variáveis de ambiente:
```bash
DOTNET_GCHeapHardLimit=4000000000  # 4GB
DOTNET_ThreadPool_UnfairSemaphoreSpinLimit=6
```

## 🆘 Troubleshooting

### Erro de conexão SQL

```bash
# Verificar se o ODBC está instalado
odbcinst -j

# Testar conexão
sqlcmd -S servidor,porta -U usuario -P senha -Q "SELECT 1"
```

### Porta já em uso

```bash
# Mudar porta no appsettings.json ou docker-compose.yml
"Server": { "Port": 8081 }
```

### Logs não aparecem

```bash
# Verificar permissões
sudo chown -R consultas:consultas /opt/consultas-remotas/logs
```

## 📝 Licença

Este projeto está sob licença MIT.

## 👥 Contribuição

Contribuições são bem-vindas! Por favor, abra uma issue ou pull request.
