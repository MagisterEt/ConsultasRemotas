#!/bin/bash
# ========================================
# WIZARD DE CONFIGURAÇÃO - ConsultasRemotas
# ========================================

set -e

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Arquivo de configuração
ENV_FILE=".env"
ENV_BACKUP=".env.backup.$(date +%Y%m%d_%H%M%S)"

clear
echo -e "${CYAN}=========================================${NC}"
echo -e "${CYAN}  Wizard de Configuração${NC}"
echo -e "${CYAN}  ConsultasRemotas - Backend C#${NC}"
echo -e "${CYAN}=========================================${NC}"
echo ""

# Verificar se já existe .env
if [ -f "$ENV_FILE" ]; then
    echo -e "${YELLOW}⚠️  Arquivo .env já existe!${NC}"
    echo ""
    read -p "Deseja sobrescrever? Um backup será criado (s/N): " overwrite
    if [[ ! $overwrite =~ ^[Ss]$ ]]; then
        echo -e "${BLUE}Operação cancelada.${NC}"
        exit 0
    fi
    cp "$ENV_FILE" "$ENV_BACKUP"
    echo -e "${GREEN}✓ Backup criado: $ENV_BACKUP${NC}"
    echo ""
fi

# Função para ler senha (modo oculto)
read_password() {
    local prompt="$1"
    local var_name="$2"

    while true; do
        read -s -p "$prompt" password
        echo ""

        if [ -z "$password" ]; then
            echo -e "${RED}✗ Senha não pode ser vazia${NC}"
            continue
        fi

        read -s -p "Confirme a senha: " password_confirm
        echo ""

        if [ "$password" != "$password_confirm" ]; then
            echo -e "${RED}✗ Senhas não conferem. Tente novamente.${NC}"
            continue
        fi

        eval "$var_name='$password'"
        break
    done
}

# Função para validar UUID
validate_uuid() {
    local uuid="$1"
    if [[ $uuid =~ ^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$ ]]; then
        return 0
    else
        return 1
    fi
}

echo -e "${BLUE}══════════════════════════════════════${NC}"
echo -e "${BLUE}  1. CREDENCIAIS SQL SERVER${NC}"
echo -e "${BLUE}══════════════════════════════════════${NC}"
echo ""
echo "Estas credenciais serão usadas para conectar aos servidores SQL."
echo ""

# SQL User
while true; do
    read -p "Usuário SQL Server: " SQL_USER
    if [ -n "$SQL_USER" ]; then
        break
    fi
    echo -e "${RED}✗ Usuário não pode ser vazio${NC}"
done

# SQL Password
read_password "Senha SQL Server: " SQL_PASSWORD

echo ""
echo -e "${GREEN}✓ Credenciais SQL Server configuradas${NC}"
echo ""

# SQL Server APS (opcional)
echo -e "${BLUE}══════════════════════════════════════${NC}"
echo -e "${BLUE}  2. SERVIDOR APS (Opcional)${NC}"
echo -e "${BLUE}══════════════════════════════════════${NC}"
echo ""
read -p "O servidor APS usa credenciais diferentes? (s/N): " use_different_aps
echo ""

if [[ $use_different_aps =~ ^[Ss]$ ]]; then
    while true; do
        read -p "Usuário APS: " SQL_USER_APS
        if [ -n "$SQL_USER_APS" ]; then
            break
        fi
        echo -e "${RED}✗ Usuário não pode ser vazio${NC}"
    done

    read_password "Senha APS: " SQL_PASSWORD_APS
    echo -e "${GREEN}✓ Credenciais APS configuradas${NC}"
else
    SQL_USER_APS="$SQL_USER"
    SQL_PASSWORD_APS="$SQL_PASSWORD"
    echo -e "${BLUE}Usando as mesmas credenciais do SQL Server principal${NC}"
fi
echo ""

# Azure AD e SharePoint (opcional)
echo -e "${BLUE}══════════════════════════════════════${NC}"
echo -e "${BLUE}  3. INTEGRAÇÃO SHAREPOINT (Opcional)${NC}"
echo -e "${BLUE}══════════════════════════════════════${NC}"
echo ""
echo "A integração com SharePoint permite upload automático de resultados."
echo "Requer configuração de Azure AD App Registration."
echo ""
read -p "Deseja configurar integração com SharePoint? (s/N): " configure_sharepoint
echo ""

if [[ $configure_sharepoint =~ ^[Ss]$ ]]; then
    # Azure Tenant ID
    while true; do
        read -p "Azure Tenant ID (UUID): " AZURE_TENANT_ID
        if validate_uuid "$AZURE_TENANT_ID"; then
            break
        fi
        echo -e "${RED}✗ Formato inválido. Esperado: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx${NC}"
    done

    # Azure Client ID
    while true; do
        read -p "Azure Client ID (UUID): " AZURE_CLIENT_ID
        if validate_uuid "$AZURE_CLIENT_ID"; then
            break
        fi
        echo -e "${RED}✗ Formato inválido. Esperado: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx${NC}"
    done

    # Azure Client Secret
    while true; do
        read -s -p "Azure Client Secret: " AZURE_CLIENT_SECRET
        echo ""
        if [ -n "$AZURE_CLIENT_SECRET" ]; then
            break
        fi
        echo -e "${RED}✗ Client Secret não pode ser vazio${NC}"
    done

    # SharePoint Site ID
    read -p "SharePoint Site ID: " SHAREPOINT_SITE_ID

    # SharePoint Drive ID
    read -p "SharePoint Drive ID: " SHAREPOINT_DRIVE_ID

    echo ""
    echo -e "${GREEN}✓ Configuração SharePoint completa${NC}"
else
    AZURE_TENANT_ID=""
    AZURE_CLIENT_ID=""
    AZURE_CLIENT_SECRET=""
    SHAREPOINT_SITE_ID=""
    SHAREPOINT_DRIVE_ID=""
    echo -e "${YELLOW}SharePoint não será configurado (pode ser adicionado depois)${NC}"
fi
echo ""

# Configurações de ambiente
echo -e "${BLUE}══════════════════════════════════════${NC}"
echo -e "${BLUE}  4. CONFIGURAÇÕES DE AMBIENTE${NC}"
echo -e "${BLUE}══════════════════════════════════════${NC}"
echo ""

ASPNETCORE_ENVIRONMENT="Production"
ASPNETCORE_URLS="http://+:8080"
DOTNET_gcServer=1
DOTNET_GCHeapHardLimit=2000000000

echo "Ambiente: $ASPNETCORE_ENVIRONMENT"
echo "URL: $ASPNETCORE_URLS"
echo "Server GC: Habilitado"
echo "Heap Limit: 2GB"
echo ""

# Resumo
echo -e "${CYAN}=========================================${NC}"
echo -e "${CYAN}  RESUMO DA CONFIGURAÇÃO${NC}"
echo -e "${CYAN}=========================================${NC}"
echo ""
echo -e "${GREEN}SQL Server:${NC}"
echo "  Usuário: $SQL_USER"
echo "  Senha: ********"
echo ""

if [[ $use_different_aps =~ ^[Ss]$ ]]; then
    echo -e "${GREEN}Servidor APS:${NC}"
    echo "  Usuário: $SQL_USER_APS"
    echo "  Senha: ********"
    echo ""
fi

if [[ $configure_sharepoint =~ ^[Ss]$ ]]; then
    echo -e "${GREEN}SharePoint:${NC}"
    echo "  Tenant ID: $AZURE_TENANT_ID"
    echo "  Client ID: $AZURE_CLIENT_ID"
    echo "  Client Secret: ********"
    echo "  Site ID: $SHAREPOINT_SITE_ID"
    echo "  Drive ID: $SHAREPOINT_DRIVE_ID"
    echo ""
else
    echo -e "${YELLOW}SharePoint: Não configurado${NC}"
    echo ""
fi

echo -e "${GREEN}Ambiente:${NC}"
echo "  Modo: $ASPNETCORE_ENVIRONMENT"
echo "  Porta: 8080"
echo ""

read -p "Confirma as configurações acima? (S/n): " confirm
if [[ $confirm =~ ^[Nn]$ ]]; then
    echo -e "${RED}Configuração cancelada.${NC}"
    exit 1
fi

# Criar arquivo .env
echo "# ========================================" > "$ENV_FILE"
echo "# ConsultasRemotas - Configuração" >> "$ENV_FILE"
echo "# Gerado em: $(date)" >> "$ENV_FILE"
echo "# ========================================" >> "$ENV_FILE"
echo "" >> "$ENV_FILE"

echo "# SQL Server - Servidor Principal" >> "$ENV_FILE"
echo "SQL_USER=$SQL_USER" >> "$ENV_FILE"
echo "SQL_PASSWORD=$SQL_PASSWORD" >> "$ENV_FILE"
echo "" >> "$ENV_FILE"

echo "# SQL Server - Servidor APS" >> "$ENV_FILE"
echo "SQL_USER_APS=$SQL_USER_APS" >> "$ENV_FILE"
echo "SQL_PASSWORD_APS=$SQL_PASSWORD_APS" >> "$ENV_FILE"
echo "" >> "$ENV_FILE"

echo "# Azure AD / Microsoft Graph" >> "$ENV_FILE"
echo "AZURE_TENANT_ID=$AZURE_TENANT_ID" >> "$ENV_FILE"
echo "AZURE_CLIENT_ID=$AZURE_CLIENT_ID" >> "$ENV_FILE"
echo "AZURE_CLIENT_SECRET=$AZURE_CLIENT_SECRET" >> "$ENV_FILE"
echo "" >> "$ENV_FILE"

echo "# SharePoint" >> "$ENV_FILE"
echo "SHAREPOINT_SITE_ID=$SHAREPOINT_SITE_ID" >> "$ENV_FILE"
echo "SHAREPOINT_DRIVE_ID=$SHAREPOINT_DRIVE_ID" >> "$ENV_FILE"
echo "" >> "$ENV_FILE"

echo "# Servidor Web" >> "$ENV_FILE"
echo "ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT" >> "$ENV_FILE"
echo "ASPNETCORE_URLS=$ASPNETCORE_URLS" >> "$ENV_FILE"
echo "" >> "$ENV_FILE"

echo "# Performance" >> "$ENV_FILE"
echo "DOTNET_gcServer=$DOTNET_gcServer" >> "$ENV_FILE"
echo "DOTNET_GCHeapHardLimit=$DOTNET_GCHeapHardLimit" >> "$ENV_FILE"

# Proteger arquivo
chmod 600 "$ENV_FILE"

echo ""
echo -e "${GREEN}=========================================${NC}"
echo -e "${GREEN}✓ Configuração concluída com sucesso!${NC}"
echo -e "${GREEN}=========================================${NC}"
echo ""
echo -e "Arquivo criado: ${CYAN}$ENV_FILE${NC}"
echo -e "Permissões: ${YELLOW}600 (somente leitura pelo dono)${NC}"
echo ""
echo -e "${BLUE}Próximos passos:${NC}"
echo ""
echo -e "1. ${CYAN}Validar pré-requisitos:${NC}"
echo -e "   ${YELLOW}./validate-prereqs.sh${NC}"
echo ""
echo -e "2. ${CYAN}Fazer deploy:${NC}"
echo -e "   ${YELLOW}./deploy-docker.sh${NC}  (recomendado)"
echo -e "   ou"
echo -e "   ${YELLOW}sudo ./deploy-ubuntu.sh${NC}  (instalação direta)"
echo ""
echo -e "3. ${CYAN}Verificar funcionamento:${NC}"
echo -e "   ${YELLOW}curl http://localhost:8080/health${NC}"
echo ""
