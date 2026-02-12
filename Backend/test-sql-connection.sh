#!/bin/bash
# ========================================
# TESTE DE CONECTIVIDADE SQL SERVER
# ========================================

# Cores
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Carregar variáveis do .env
if [ ! -f .env ]; then
    echo -e "${RED}✗ Arquivo .env não encontrado!${NC}"
    echo -e "${YELLOW}Execute ./setup-wizard.sh primeiro${NC}"
    exit 1
fi

# Carregar .env
export $(grep -v '^#' .env | xargs)

# Verificar se credenciais existem
if [ -z "$SQL_USER" ] || [ -z "$SQL_PASSWORD" ]; then
    echo -e "${RED}✗ Credenciais SQL não configuradas no .env${NC}"
    exit 1
fi

echo -e "${CYAN}=========================================${NC}"
echo -e "${CYAN}  Teste de Conectividade SQL Server${NC}"
echo -e "${CYAN}=========================================${NC}"
echo ""
echo "Usuário: $SQL_USER"
echo ""

# Ler servidores do appsettings.json
APPSETTINGS="ConsultasRemotas.Api/appsettings.json"

if [ ! -f "$APPSETTINGS" ]; then
    echo -e "${RED}✗ Arquivo $APPSETTINGS não encontrado!${NC}"
    exit 1
fi

# Extrair IPs dos servidores (10.3.254.201-215)
SERVERS=(
    "10.3.254.201"
    "10.3.254.202"
    "10.3.254.203"
    "10.3.254.204"
    "10.3.254.205"
    "10.3.254.206"
    "10.3.254.207"
    "10.3.254.208"
    "10.3.254.209"
    "10.3.254.210"
    "10.3.254.211"
    "10.3.254.212"
    "10.3.254.213"
    "10.3.254.214"
    "10.3.254.215"
)

PORT=1433
SUCCESS_COUNT=0
FAIL_COUNT=0

echo -e "${BLUE}Testando conectividade com 15 servidores SQL...${NC}"
echo ""

for i in "${!SERVERS[@]}"; do
    SERVER_NUM=$((i + 1))
    SERVER_IP="${SERVERS[$i]}"

    echo -n "Server${SERVER_NUM} (${SERVER_IP}:${PORT}) ... "

    # Usar timeout e nc (netcat) para testar conexão
    START_TIME=$(date +%s%3N)

    if timeout 3 bash -c "echo > /dev/tcp/${SERVER_IP}/${PORT}" 2>/dev/null; then
        END_TIME=$(date +%s%3N)
        ELAPSED=$((END_TIME - START_TIME))

        echo -e "${GREEN}✓ OK${NC} (${ELAPSED}ms)"
        ((SUCCESS_COUNT++))
    else
        echo -e "${RED}✗ FALHA${NC} (timeout ou conexão recusada)"
        ((FAIL_COUNT++))
    fi
done

echo ""
echo -e "${CYAN}=========================================${NC}"
echo -e "${CYAN}  RESULTADO${NC}"
echo -e "${CYAN}=========================================${NC}"
echo ""

if [ $SUCCESS_COUNT -eq 15 ]; then
    echo -e "${GREEN}✓ Todos os 15 servidores acessíveis!${NC}"
    echo ""
    exit 0
elif [ $SUCCESS_COUNT -gt 0 ]; then
    echo -e "${YELLOW}⚠️  Conectividade parcial${NC}"
    echo -e "  Sucesso: ${GREEN}$SUCCESS_COUNT${NC}/15"
    echo -e "  Falhas:  ${RED}$FAIL_COUNT${NC}/15"
    echo ""
    echo -e "${YELLOW}Algumas consultas multi-servidor podem falhar${NC}"
    echo ""
    exit 1
else
    echo -e "${RED}✗ Nenhum servidor acessível!${NC}"
    echo ""
    echo -e "${YELLOW}Possíveis causas:${NC}"
    echo "  - Servidores SQL offline"
    echo "  - Firewall bloqueando porta 1433"
    echo "  - Problemas de rede/roteamento"
    echo "  - Credenciais incorretas"
    echo ""
    exit 2
fi
