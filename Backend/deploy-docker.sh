#!/bin/bash
# ========================================
# SCRIPT DE DEPLOYMENT VIA DOCKER (UBUNTU)
# ========================================

set -e

echo "========================================="
echo "Deploy ConsultasRemotas API via Docker"
echo "========================================="

# Cores
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

# ========================================
# VALIDAÇÃO DE PRÉ-REQUISITOS
# ========================================
if [ -f validate-prereqs.sh ] && [ -x validate-prereqs.sh ]; then
    echo -e "${YELLOW}Validando pré-requisitos...${NC}"
    echo ""

    if ./validate-prereqs.sh; then
        echo ""
        echo -e "${GREEN}✓ Validação concluída. Continuando com deployment...${NC}"
        echo ""
    else
        EXIT_CODE=$?
        echo ""
        echo -e "${YELLOW}A validação encontrou problemas.${NC}"
        read -p "Deseja continuar mesmo assim? (s/N): " continue_deploy
        if [[ ! $continue_deploy =~ ^[Ss]$ ]]; then
            echo -e "${RED}Deployment cancelado.${NC}"
            exit $EXIT_CODE
        fi
        echo ""
        echo -e "${YELLOW}Continuando deployment apesar dos avisos...${NC}"
        echo ""
    fi
fi

# Verificar se Docker está instalado
if ! command -v docker &> /dev/null; then
    echo -e "${YELLOW}Docker não encontrado. Instalando...${NC}"

    # Instalar Docker
    apt-get update
    apt-get install -y ca-certificates curl gnupg lsb-release

    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg

    echo \
      "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu \
      $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null

    apt-get update
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin

    echo -e "${GREEN}Docker instalado com sucesso!${NC}"
fi

# Verificar se Docker Compose está instalado
if ! command -v docker compose &> /dev/null; then
    echo -e "${YELLOW}Docker Compose não encontrado. Instalando...${NC}"
    apt-get install -y docker-compose-plugin
    echo -e "${GREEN}Docker Compose instalado!${NC}"
fi

# Criar arquivo .env se não existir
if [ ! -f .env ]; then
    echo -e "${YELLOW}Criando arquivo .env de exemplo...${NC}"
    cat > .env <<EOF
# SQL Server
SQL_USER=seu_usuario
SQL_PASSWORD=sua_senha

# Azure AD
AZURE_TENANT_ID=seu_tenant_id
AZURE_CLIENT_ID=seu_client_id
AZURE_CLIENT_SECRET=seu_client_secret

# SharePoint
SHAREPOINT_SITE_ID=seu_site_id
SHAREPOINT_DRIVE_ID=seu_drive_id
EOF
    echo -e "${RED}IMPORTANTE: Edite o arquivo .env com suas credenciais!${NC}"
    echo -e "${RED}Caminho: $(pwd)/.env${NC}"
fi

# Criar diretórios necessários
mkdir -p logs
mkdir -p static

# Build da imagem
echo -e "${GREEN}Compilando imagem Docker...${NC}"
docker compose build

# Parar containers antigos
echo -e "${YELLOW}Parando containers antigos...${NC}"
docker compose down 2>/dev/null || true

# Iniciar containers
echo -e "${GREEN}Iniciando containers...${NC}"
docker compose up -d

# Aguardar inicialização
echo -e "${YELLOW}Aguardando inicialização...${NC}"
sleep 5

# Verificar status
if docker compose ps | grep -q "Up"; then
    echo ""
    echo -e "${GREEN}=========================================${NC}"
    echo -e "${GREEN}Deployment concluído com sucesso!${NC}"
    echo -e "${GREEN}=========================================${NC}"
    echo ""
    echo "A aplicação está rodando em: http://localhost:8080"
    echo "Swagger disponível em: http://localhost:8080/swagger"
    echo ""
    echo "Comandos úteis:"
    echo -e "  Ver logs:      ${YELLOW}docker compose logs -f${NC}"
    echo -e "  Parar:         ${YELLOW}docker compose stop${NC}"
    echo -e "  Reiniciar:     ${YELLOW}docker compose restart${NC}"
    echo -e "  Remover tudo:  ${YELLOW}docker compose down${NC}"
    echo ""
else
    echo -e "${RED}Erro ao iniciar a aplicação. Verifique os logs:${NC}"
    echo -e "${YELLOW}docker compose logs${NC}"
    exit 1
fi
