#!/bin/bash
# ========================================
# VALIDAÇÃO DE PRÉ-REQUISITOS
# ========================================

# Cores
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Contadores
CHECKS_PASSED=0
CHECKS_FAILED=0
CHECKS_WARNING=0

# Função para verificação com status
check() {
    local description="$1"
    local command="$2"
    local required="$3"  # true/false

    echo -n "$description ... "

    if eval "$command" &>/dev/null; then
        echo -e "${GREEN}✓${NC}"
        ((CHECKS_PASSED++))
        return 0
    else
        if [ "$required" = "true" ]; then
            echo -e "${RED}✗ ERRO${NC}"
            ((CHECKS_FAILED++))
            return 1
        else
            echo -e "${YELLOW}⚠ WARNING${NC}"
            ((CHECKS_WARNING++))
            return 2
        fi
    fi
}

# Função para checar comando existe
command_exists() {
    command -v "$1" &> /dev/null
}

clear
echo -e "${CYAN}=========================================${NC}"
echo -e "${CYAN}  Validação de Pré-requisitos${NC}"
echo -e "${CYAN}  ConsultasRemotas - Ubuntu${NC}"
echo -e "${CYAN}=========================================${NC}"
echo ""

# ========================================
# 1. SISTEMA OPERACIONAL
# ========================================
echo -e "${BLUE}[1/8] Sistema Operacional${NC}"

# Detectar Ubuntu
if [ -f /etc/os-release ]; then
    . /etc/os-release
    echo -n "Ubuntu detectado ... "
    if [[ "$ID" == "ubuntu" ]]; then
        echo -e "${GREEN}✓ $PRETTY_NAME${NC}"
        ((CHECKS_PASSED++))

        # Verificar versão
        MAJOR_VERSION=$(echo "$VERSION_ID" | cut -d. -f1)
        if [ "$MAJOR_VERSION" -ge 20 ]; then
            echo -e "  Versão suportada: ${GREEN}✓${NC}"
            ((CHECKS_PASSED++))
        else
            echo -e "  ${YELLOW}⚠ Versão antiga (< 20.04)${NC}"
            ((CHECKS_WARNING++))
        fi
    else
        echo -e "${YELLOW}⚠ Não é Ubuntu ($ID)${NC}"
        ((CHECKS_WARNING++))
    fi
else
    echo -e "${RED}✗ Não foi possível detectar o sistema${NC}"
    ((CHECKS_FAILED++))
fi

echo ""

# ========================================
# 2. CONECTIVIDADE
# ========================================
echo -e "${BLUE}[2/8] Conectividade de Rede${NC}"

check "Conectividade com internet" "ping -c 1 8.8.8.8" "false"

echo ""

# ========================================
# 3. PORTA 8080
# ========================================
echo -e "${BLUE}[3/8] Portas${NC}"

echo -n "Porta 8080 disponível ... "
if ! lsof -Pi :8080 -sTCP:LISTEN -t >/dev/null 2>&1 && ! netstat -tuln 2>/dev/null | grep -q ":8080 "; then
    echo -e "${GREEN}✓${NC}"
    ((CHECKS_PASSED++))
else
    echo -e "${RED}✗ Em uso${NC}"
    echo -e "  ${YELLOW}Execute: sudo lsof -i :8080${NC}"
    ((CHECKS_FAILED++))
fi

echo ""

# ========================================
# 4. DOCKER (se disponível)
# ========================================
echo -e "${BLUE}[4/8] Docker${NC}"

if command_exists docker; then
    DOCKER_VERSION=$(docker --version 2>/dev/null | awk '{print $3}' | tr -d ',')
    echo -e "Docker instalado ... ${GREEN}✓ $DOCKER_VERSION${NC}"
    ((CHECKS_PASSED++))

    # Verificar se Docker está rodando
    echo -n "Docker daemon ativo ... "
    if docker ps &>/dev/null; then
        echo -e "${GREEN}✓${NC}"
        ((CHECKS_PASSED++))
    else
        echo -e "${RED}✗ Não está rodando${NC}"
        echo -e "  ${YELLOW}Execute: sudo systemctl start docker${NC}"
        ((CHECKS_FAILED++))
    fi

    # Docker Compose
    if command_exists "docker compose" || docker compose version &>/dev/null; then
        COMPOSE_VERSION=$(docker compose version 2>/dev/null | awk '{print $NF}')
        echo -e "Docker Compose ... ${GREEN}✓ $COMPOSE_VERSION${NC}"
        ((CHECKS_PASSED++))
    else
        echo -e "Docker Compose ... ${RED}✗ Não instalado${NC}"
        ((CHECKS_FAILED++))
    fi
else
    echo -e "Docker ... ${YELLOW}⚠ Não instalado${NC}"
    echo -e "  ${CYAN}Para instalação direta, ignore este aviso${NC}"
    echo -e "  ${CYAN}Para usar Docker, execute deploy-docker.sh que instalará automaticamente${NC}"
    ((CHECKS_WARNING++))
fi

echo ""

# ========================================
# 5. .NET SDK/Runtime (se instalação direta)
# ========================================
echo -e "${BLUE}[5/8] .NET Runtime${NC}"

if command_exists dotnet; then
    DOTNET_VERSION=$(dotnet --version 2>/dev/null)
    echo -e ".NET instalado ... ${GREEN}✓ $DOTNET_VERSION${NC}"
    ((CHECKS_PASSED++))
else
    echo -e ".NET ... ${YELLOW}⚠ Não instalado${NC}"
    echo -e "  ${CYAN}Necessário apenas para instalação direta${NC}"
    echo -e "  ${CYAN}Para Docker, ignore este aviso${NC}"
    ((CHECKS_WARNING++))
fi

echo ""

# ========================================
# 6. ARQUIVO .ENV
# ========================================
echo -e "${BLUE}[6/8] Configuração (.env)${NC}"

echo -n "Arquivo .env existe ... "
if [ -f .env ]; then
    echo -e "${GREEN}✓${NC}"
    ((CHECKS_PASSED++))

    # Verificar se tem credenciais SQL
    if grep -q "SQL_USER=" .env && grep -q "SQL_PASSWORD=" .env; then
        SQL_USER_VALUE=$(grep "^SQL_USER=" .env | cut -d= -f2)
        SQL_PASSWORD_VALUE=$(grep "^SQL_PASSWORD=" .env | cut -d= -f2)

        if [ -n "$SQL_USER_VALUE" ] && [ -n "$SQL_PASSWORD_VALUE" ]; then
            echo -e "Credenciais SQL configuradas ... ${GREEN}✓${NC}"
            ((CHECKS_PASSED++))
        else
            echo -e "Credenciais SQL ... ${RED}✗ Vazias${NC}"
            echo -e "  ${YELLOW}Execute: ./setup-wizard.sh${NC}"
            ((CHECKS_FAILED++))
        fi
    else
        echo -e "Credenciais SQL ... ${RED}✗ Não encontradas${NC}"
        ((CHECKS_FAILED++))
    fi
else
    echo -e "${RED}✗ Não encontrado${NC}"
    echo -e "  ${YELLOW}Execute: ./setup-wizard.sh${NC}"
    ((CHECKS_FAILED++))
fi

echo ""

# ========================================
# 7. CONECTIVIDADE SQL SERVER
# ========================================
echo -e "${BLUE}[7/8] Conectividade SQL Server${NC}"

if [ -f test-sql-connection.sh ]; then
    echo "Testando conectividade com servidores SQL..."
    echo ""

    if bash test-sql-connection.sh; then
        : # Success, already counted by script
    else
        EXIT_CODE=$?
        if [ $EXIT_CODE -eq 1 ]; then
            echo -e "${YELLOW}⚠ Conectividade parcial com servidores SQL${NC}"
            ((CHECKS_WARNING++))
        else
            echo -e "${RED}✗ Falha na conectividade com servidores SQL${NC}"
            ((CHECKS_FAILED++))
        fi
    fi
else
    echo -e "${YELLOW}⚠ Script de teste não encontrado${NC}"
    ((CHECKS_WARNING++))
fi

echo ""

# ========================================
# 8. RECURSOS DO SISTEMA
# ========================================
echo -e "${BLUE}[8/8] Recursos do Sistema${NC}"

# Espaço em disco
DISK_AVAILABLE=$(df -BG . | tail -1 | awk '{print $4}' | sed 's/G//')
echo -n "Espaço em disco disponível ... "
if [ "$DISK_AVAILABLE" -ge 2 ]; then
    echo -e "${GREEN}✓ ${DISK_AVAILABLE}GB${NC}"
    ((CHECKS_PASSED++))
else
    echo -e "${YELLOW}⚠ Apenas ${DISK_AVAILABLE}GB (recomendado: 2GB+)${NC}"
    ((CHECKS_WARNING++))
fi

# Memória RAM
MEM_AVAILABLE=$(free -g | awk '/^Mem:/{print $7}')
echo -n "Memória RAM disponível ... "
if [ "$MEM_AVAILABLE" -ge 1 ]; then
    echo -e "${GREEN}✓ ${MEM_AVAILABLE}GB${NC}"
    ((CHECKS_PASSED++))
else
    MEM_AVAILABLE_MB=$(free -m | awk '/^Mem:/{print $7}')
    echo -e "${YELLOW}⚠ ${MEM_AVAILABLE_MB}MB (recomendado: 1GB+)${NC}"
    ((CHECKS_WARNING++))
fi

echo ""

# ========================================
# RESULTADO FINAL
# ========================================
echo -e "${CYAN}=========================================${NC}"
echo -e "${CYAN}  RESULTADO DA VALIDAÇÃO${NC}"
echo -e "${CYAN}=========================================${NC}"
echo ""

echo -e "Verificações passadas: ${GREEN}$CHECKS_PASSED ✓${NC}"
if [ $CHECKS_WARNING -gt 0 ]; then
    echo -e "Avisos: ${YELLOW}$CHECKS_WARNING ⚠${NC}"
fi
if [ $CHECKS_FAILED -gt 0 ]; then
    echo -e "Erros: ${RED}$CHECKS_FAILED ✗${NC}"
fi

echo ""

if [ $CHECKS_FAILED -eq 0 ]; then
    echo -e "${GREEN}=========================================${NC}"
    echo -e "${GREEN}✓ Sistema pronto para deployment!${NC}"
    echo -e "${GREEN}=========================================${NC}"
    echo ""

    if [ $CHECKS_WARNING -gt 0 ]; then
        echo -e "${YELLOW}Há alguns avisos, mas você pode continuar.${NC}"
        echo ""
    fi

    echo -e "${BLUE}Próximo passo:${NC}"
    echo ""
    echo -e "  Deploy via Docker:"
    echo -e "  ${CYAN}./deploy-docker.sh${NC}"
    echo ""
    echo -e "  Ou instalação direta:"
    echo -e "  ${CYAN}sudo ./deploy-ubuntu.sh${NC}"
    echo ""
    exit 0
else
    echo -e "${RED}=========================================${NC}"
    echo -e "${RED}✗ Corrija os erros antes de continuar${NC}"
    echo -e "${RED}=========================================${NC}"
    echo ""
    echo -e "${YELLOW}Dicas:${NC}"
    echo ""
    echo -e "• Configure credenciais: ${CYAN}./setup-wizard.sh${NC}"
    echo -e "• Instale Docker: ${CYAN}./deploy-docker.sh${NC} (instalará automaticamente)"
    echo -e "• Libere porta 8080: ${CYAN}sudo lsof -i :8080${NC}"
    echo -e "• Verifique firewall: ${CYAN}sudo ufw status${NC}"
    echo ""
    exit 1
fi
