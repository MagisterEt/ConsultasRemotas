#!/bin/bash
# Script de teste da API ConsultasRemotas
# Uso: ./test-api.sh [URL_BASE]

set -e

API_URL="${1:-http://localhost:8080}"
BOLD='\033[1m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BOLD}🧪 Teste da API ConsultasRemotas${NC}"
echo -e "URL Base: ${YELLOW}$API_URL${NC}\n"

# Função para testar endpoint
test_endpoint() {
    local method=$1
    local endpoint=$2
    local data=$3
    local description=$4

    echo -e "${BOLD}Teste: ${description}${NC}"
    echo -e "Endpoint: ${YELLOW}${method} ${endpoint}${NC}"

    if [ -z "$data" ]; then
        response=$(curl -s -w "\n%{http_code}" -X ${method} "${API_URL}${endpoint}")
    else
        response=$(curl -s -w "\n%{http_code}" -X ${method} \
            -H "Content-Type: application/json" \
            -d "$data" \
            "${API_URL}${endpoint}")
    fi

    http_code=$(echo "$response" | tail -n1)
    body=$(echo "$response" | sed '$d')

    if [ "$http_code" -ge 200 ] && [ "$http_code" -lt 300 ]; then
        echo -e "${GREEN}✅ Status: ${http_code}${NC}"
        echo -e "Resposta:\n${body}" | jq '.' 2>/dev/null || echo "$body"
    else
        echo -e "${RED}❌ Status: ${http_code}${NC}"
        echo -e "Resposta:\n${body}"
    fi
    echo ""
}

# 1. Health Check
test_endpoint "GET" "/health" "" "Health Check"

# 2. Status da API
test_endpoint "GET" "/api/status" "" "Status da API"

# 3. Listar consultas disponíveis
test_endpoint "GET" "/api/consultas_disponiveis" "" "Listar Consultas Disponíveis"

# 4. Executar consulta simples (exemplo)
read -p "Deseja testar uma consulta SQL? (s/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Ss]$ ]]; then
    echo -e "${YELLOW}📝 Exemplo de consulta:${NC}"

    query_data='{
  "query": "SELECT TOP 10 * FROM INFORMATION_SCHEMA.TABLES",
  "banco": "AASI"
}'

    test_endpoint "POST" "/api/consultar" "$query_data" "Executar Consulta Simples"
fi

# 5. Testar consulta multi-servidor
read -p "Deseja testar consulta multi-servidor? (s/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Ss]$ ]]; then
    multi_query_data='{
  "query": "SELECT TOP 5 * FROM INFORMATION_SCHEMA.TABLES",
  "banco": "AASI"
}'

    test_endpoint "POST" "/api/consultar_multi" "$multi_query_data" "Executar Consulta Multi-Servidor"
fi

echo -e "${BOLD}${GREEN}✅ Testes concluídos!${NC}"
echo -e "\n${YELLOW}📚 Endpoints disponíveis:${NC}"
echo "  GET  /health"
echo "  GET  /api/status"
echo "  GET  /api/consultas_disponiveis"
echo "  POST /api/consultar"
echo "  POST /api/consultar_multi"
echo "  POST /api/exportar/{formato}"
echo "  POST /api/upload_sharepoint"
echo "  GET  /api/logs/{requestId}"
echo "  GET  /api/resultado/{requestId}"
echo "  GET  /api/status/{requestId}"
echo "  POST /api/cancelar/{requestId}"
echo "  POST /api/cancelar_todas"
