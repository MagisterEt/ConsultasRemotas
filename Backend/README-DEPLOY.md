# 🚀 Guia de Deploy - Consultas Remotas

Sistema de Consultas SQL Distribuídas com autenticação corporativa.

## 📋 Pré-requisitos

- Ubuntu Server 20.04+ ou 22.04+
- Docker e Docker Compose
- Acesso SSH ao servidor
- Credenciais SQL Server
- Azure AD configurado (para SharePoint)

---

## 🔧 1. Atualizar Código no Servidor

```bash
# SSH no servidor
ssh usuario@servidor

# Ir para o diretório do projeto
cd ~/consultas_sql/Backend

# Puxar últimas alterações
git pull origin claude/optimize-csharp-ubuntu-du8RP

# Verificar mudanças
git log --oneline -5
```

---

## ⚙️ 2. Configurar Credenciais

```bash
# Executar wizard de configuração (se ainda não foi feito)
./setup-wizard.sh
```

O script vai pedir:
- ✅ **SQL_USER**: Usuário do SQL Server
- ✅ **SQL_PASSWORD**: Senha do SQL Server
- ✅ **AZURE_TENANT_ID**: ID do tenant Azure AD
- ✅ **AZURE_CLIENT_ID**: ID da aplicação Azure AD
- ✅ **AZURE_CLIENT_SECRET**: Secret da aplicação
- ✅ **SHAREPOINT_SITE_ID**: ID do site SharePoint
- ✅ **SHAREPOINT_DRIVE_ID**: ID da biblioteca de documentos

---

## 🏗️ 3. Build e Deploy

### Opção A: Usar Script Automático (Recomendado)

```bash
sudo ./deploy-docker.sh
```

### Opção B: Manual

```bash
# Build da imagem Docker
sudo docker compose build

# Iniciar containers
sudo docker compose up -d

# Verificar se subiu
sudo docker ps | grep consultas
```

---

## ✅ 4. Verificar Deployment

### 4.1 Health Check

```bash
# Testar API
curl http://localhost:8080/health

# Deve retornar: Healthy
```

### 4.2 Verificar Logs

```bash
# Logs em tempo real
sudo docker logs -f consultas-remotas-api

# Últimas 50 linhas
sudo docker logs --tail 50 consultas-remotas-api
```

### 4.3 Testar Endpoints da API

```bash
# Executar script de testes
chmod +x test-api.sh
./test-api.sh http://localhost:8080
```

---

## 🌐 5. Acessar Frontend

### No Navegador:

```
http://SEU_SERVIDOR:8080
```

### Tela de Login:
1. Clique em **"Entrar com Microsoft"**
2. Digite seu e-mail: `seunome@adventistas.org`
3. Sistema validará o domínio automaticamente

### Funcionalidades Disponíveis:
- ✅ Consultas predefinidas (Lotes sem Anexo, Aquisições, Baixas)
- ✅ SQL Personalizado
- ✅ Exportar para CSV
- ✅ Exportar para Excel
- ✅ Upload para SharePoint
- ✅ Copiar resultados para clipboard
- ✅ Cancelar consultas em andamento

---

## 🧪 6. Testar Consultas

### 6.1 Consulta Predefinida

1. Selecione **"Lotes sem Anexo"**
2. Preencha **Entidade**: `3123`
3. Clique em **"Executar Consulta"**
4. Aguarde os resultados
5. Experimente exportar para Excel

### 6.2 SQL Personalizado

1. Selecione **"💻 SQL Personalizado"**
2. Digite uma query:
```sql
SELECT TOP 10 * FROM AASI.INFORMATION_SCHEMA.TABLES
```
3. Clique em **"Executar Consulta"**

---

## 📊 7. Estrutura de Endpoints da API

### Consultas:
- `POST /api/consultar` - Consulta em servidor único
- `POST /api/consultar_multi` - Consulta em múltiplos servidores
- `GET /api/consultas_disponiveis` - Lista consultas disponíveis

### Resultados:
- `GET /api/resultado/{requestId}` - Buscar resultado
- `GET /api/status/{requestId}` - Status de execução
- `GET /api/logs/{requestId}` - Logs da consulta

### Exportação:
- `POST /api/exportar/csv` - Exportar como CSV
- `POST /api/exportar/xlsx` - Exportar como Excel
- `POST /api/upload_sharepoint` - Upload para SharePoint

### Controle:
- `POST /api/cancelar/{requestId}` - Cancelar consulta
- `POST /api/cancelar_todas` - Cancelar todas
- `GET /api/status` - Status da API
- `GET /health` - Health check

---

## 🔐 8. Configuração de Autenticação

### Autenticação Básica (Atual):
- Validação de domínio `@adventistas.org`
- Armazenamento local no navegador

### Para Azure AD Completo (Opcional):

1. **Criar App Registration no Azure Portal**:
   - Acesse: https://portal.azure.com
   - Azure Active Directory > App registrations > New registration
   - Nome: "Consultas Remotas USEB"
   - Redirect URI: `http://SEU_SERVIDOR:8080`

2. **Configurar Permissões**:
   - API permissions > Microsoft Graph
   - User.Read (Delegated)

3. **Atualizar Frontend**:
   ```javascript
   // Em js/auth.js, substituir por MSAL.js
   // Documentação: https://learn.microsoft.com/en-us/entra/msal/
   ```

---

## 🛠️ 9. Troubleshooting

### Problema: Container não sobe

```bash
# Ver logs de erro
sudo docker logs consultas-remotas-api

# Reconstruir forçando
sudo docker compose down
sudo docker compose build --no-cache
sudo docker compose up -d
```

### Problema: Erro de conexão SQL

```bash
# Verificar variáveis de ambiente
sudo docker exec consultas-remotas-api env | grep SQL

# Reconfigurar
./setup-wizard.sh
sudo docker compose restart
```

### Problema: Frontend não carrega

```bash
# Verificar se arquivos existem
ls -la wwwroot/

# Ver logs do Kestrel
sudo docker logs consultas-remotas-api | grep "Hosting"
```

### Problema: Erro 403 ao fazer push

```bash
# Garantir que a branch começa com 'claude/' e termina com session ID
git branch
# Deve ser: claude/optimize-csharp-ubuntu-du8RP
```

---

## 📂 10. Estrutura de Arquivos

```
Backend/
├── ConsultasRemotas.Api/
│   ├── Controllers/           # Endpoints da API
│   ├── Services/             # Lógica de negócio
│   ├── Models/               # DTOs e models
│   ├── Configuration/        # Settings
│   ├── wwwroot/              # Frontend (NOVO!)
│   │   ├── index.html        # UI Principal
│   │   ├── css/style.css     # Estilos
│   │   └── js/
│   │       ├── auth.js       # Autenticação
│   │       └── app.js        # Aplicação
│   ├── Program.cs            # Startup
│   └── appsettings.json      # Configurações
├── Dockerfile                # Build da imagem
├── docker-compose.yml        # Orquestração
├── deploy-docker.sh          # Script de deploy
├── test-api.sh              # Script de testes
└── README-DEPLOY.md         # Este arquivo
```

---

## 🔄 11. Atualizar Sistema

```bash
cd ~/consultas_sql/Backend
git pull origin claude/optimize-csharp-ubuntu-du8RP
sudo docker compose down
sudo docker compose build
sudo docker compose up -d
```

---

## 📞 12. Suporte

### Logs Importantes:
- **Container**: `sudo docker logs consultas-remotas-api`
- **Build**: `sudo docker compose build 2>&1 | tee build.log`
- **Runtime**: `/app/logs/consultas-YYYYMMDD.log`

### Status do Sistema:
```bash
sudo docker ps                # Containers rodando
sudo docker stats             # Uso de recursos
curl http://localhost:8080/api/status  # Status da API
```

---

## 🎯 Checklist de Deployment

- [ ] Código atualizado (`git pull`)
- [ ] Credenciais configuradas (`.env` existe)
- [ ] Build sem erros (`docker compose build`)
- [ ] Container rodando (`docker ps`)
- [ ] Health check OK (`curl /health`)
- [ ] Frontend acessível no navegador
- [ ] Login funcionando (@adventistas.org)
- [ ] Consulta de teste executada
- [ ] Exportação funcionando

---

✅ **Sistema Pronto para Uso!**

Acesse: `http://SEU_SERVIDOR:8080`
