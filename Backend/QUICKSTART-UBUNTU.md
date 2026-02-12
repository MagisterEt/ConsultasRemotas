# 🚀 Guia de Início Rápido - Ubuntu

Guia simplificado para colocar o ConsultasRemotas funcionando no Ubuntu em **3 passos**.

---

## Pré-requisitos

- Ubuntu 20.04+ ou 22.04 LTS
- Acesso root/sudo
- Credenciais SQL Server em mãos

---

## Passo 1: Preparação

Clone o repositório e acesse o diretório:

```bash
git clone https://github.com/MagisterEt/ConsultasRemotas.git
cd ConsultasRemotas/Backend
```

Execute o wizard de configuração:

```bash
chmod +x setup-wizard.sh
./setup-wizard.sh
```

O wizard irá perguntar:
- ✅ Usuário e senha SQL Server (obrigatório)
- ✅ Credenciais APS (se diferentes)
- ✅ Configuração SharePoint (opcional)

Um arquivo `.env` será criado automaticamente com suas configurações.

---

## Passo 2: Validação

Valide que o ambiente está pronto:

```bash
chmod +x validate-prereqs.sh
./validate-prereqs.sh
```

O script verificará:
- Sistema operacional e versão
- Porta 8080 disponível
- Docker instalado (para deploy via Docker)
- Arquivo .env configurado
- Conectividade com servidores SQL
- Recursos do sistema (disco e memória)

**Se houver erros**, siga as dicas fornecidas pelo script.

---

## Passo 3: Deploy

### Opção A: Docker (Recomendado - Mais Simples)

```bash
chmod +x deploy-docker.sh
sudo ./deploy-docker.sh
```

**O que o script faz:**
- Instala Docker e Docker Compose (se necessário)
- Valida pré-requisitos automaticamente
- Compila a imagem otimizada para Ubuntu
- Inicia os containers

**Tempo estimado:** 5-10 minutos

### Opção B: Instalação Direta

```bash
chmod +x deploy-ubuntu.sh
sudo ./deploy-ubuntu.sh
```

**O que o script faz:**
- Instala .NET 8 SDK/Runtime
- Instala ODBC Driver 18 para SQL Server
- Cria usuário do sistema
- Compila e publica a aplicação
- Configura systemd service
- Inicia o serviço automaticamente

**Tempo estimado:** 10-15 minutos

---

## Passo 4: Verificação

### Health Check

```bash
curl http://localhost:8080/health
```

**Resposta esperada:**
```json
{
  "status": "Healthy"
}
```

### Swagger UI

Acesse no navegador:
```
http://localhost:8080/swagger
```

### Teste de Consulta SQL

Via Swagger ou curl:

```bash
curl -X POST http://localhost:8080/api/consultar \
  -H "Content-Type: application/json" \
  -d '{
    "query": "SELECT 1 as Teste, GETDATE() as Data",
    "servidor": "Server1",
    "banco": "AASI"
  }'
```

---

## Comandos Úteis

### Docker

```bash
# Ver logs
docker compose logs -f

# Parar
docker compose stop

# Reiniciar
docker compose restart

# Remover tudo
docker compose down
```

### Instalação Direta

```bash
# Status do serviço
sudo systemctl status consultas-remotas

# Logs em tempo real
sudo journalctl -u consultas-remotas -f

# Reiniciar
sudo systemctl restart consultas-remotas

# Parar
sudo systemctl stop consultas-remotas
```

---

## Próximos Passos

### 1. Copiar Frontend (Opcional)

Se você tem os arquivos estáticos HTML/CSS/JS:

```bash
# Voltar para raiz
cd /home/user/ConsultasRemotas

# Copiar frontend
cp -r templates/* Backend/ConsultasRemotas.Api/wwwroot/
cp -r static/* Backend/ConsultasRemotas.Api/wwwroot/

# Atualizar URLs da API no JavaScript
nano Backend/ConsultasRemotas.Api/wwwroot/js/script.js
# Trocar de: http://localhost:5555
# Para: http://localhost:8080

# Reiniciar
cd Backend
docker compose restart  # Docker
# OU
sudo systemctl restart consultas-remotas  # Instalação direta
```

### 2. Configurar Firewall

```bash
# Permitir porta 8080
sudo ufw allow 8080/tcp

# Verificar
sudo ufw status
```

### 3. Testar Consulta Real

Acesse o Swagger e teste com uma consulta real do seu sistema.

---

## Troubleshooting

### Erro: Porta 8080 em uso

```bash
# Verificar o que está usando
sudo lsof -i :8080

# Matar processo (se necessário)
sudo kill -9 <PID>
```

### Erro: Conexão SQL falhou

```bash
# Testar conectividade manualmente
chmod +x test-sql-connection.sh
./test-sql-connection.sh
```

### Erro: Docker não inicia

```bash
# Iniciar Docker daemon
sudo systemctl start docker

# Verificar status
sudo systemctl status docker
```

### Erro: Credenciais inválidas

```bash
# Reconfigurar
./setup-wizard.sh
```

---

## Suporte

- **Documentação Completa**: [README.md](README.md)
- **Checklist de Deployment**: [DEPLOYMENT-CHECKLIST.md](DEPLOYMENT-CHECKLIST.md)
- **Issues**: https://github.com/MagisterEt/ConsultasRemotas/issues

---

## Resumo dos 3 Passos

```bash
# 1. Configurar
./setup-wizard.sh

# 2. Validar
./validate-prereqs.sh

# 3. Deploy
./deploy-docker.sh
```

**Pronto!** Seu sistema estará rodando em `http://localhost:8080` 🎉
