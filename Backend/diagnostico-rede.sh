#!/bin/bash
# Script de diagnóstico de conectividade
# Executar no servidor Ubuntu

echo "=========================================="
echo "🔍 DIAGNÓSTICO DE CONECTIVIDADE"
echo "=========================================="
echo ""

# 1. Verificar se container está rodando
echo "1️⃣ Verificando container..."
if docker ps | grep -q consultas-remotas-api; then
    echo "✅ Container está RODANDO"
    docker ps | grep consultas-remotas-api
else
    echo "❌ Container NÃO está rodando"
    echo "Execute: sudo docker compose up -d"
    exit 1
fi
echo ""

# 2. Testar API localmente no servidor
echo "2️⃣ Testando API localmente (no servidor)..."
if curl -s http://localhost:8080/health > /dev/null; then
    echo "✅ API está RESPONDENDO localmente"
    curl -s http://localhost:8080/api/status | jq '.' 2>/dev/null || curl -s http://localhost:8080/api/status
else
    echo "❌ API NÃO está respondendo"
    echo "Verifique os logs: sudo docker logs consultas-remotas-api"
    exit 1
fi
echo ""

# 3. Descobrir IP do servidor
echo "3️⃣ Endereços IP do servidor:"
echo "-----------------------------"
hostname -I | tr ' ' '\n' | grep -v '^$' | nl
echo ""

# 4. Verificar porta 8080
echo "4️⃣ Verificando porta 8080..."
if netstat -tuln | grep -q ':8080'; then
    echo "✅ Porta 8080 está ABERTA"
    netstat -tuln | grep ':8080'
else
    echo "⚠️  Porta 8080 pode não estar visível externamente"
fi
echo ""

# 5. Verificar firewall (ufw)
echo "5️⃣ Verificando firewall (UFW)..."
if command -v ufw &> /dev/null; then
    ufw_status=$(sudo ufw status 2>/dev/null | grep "Status:")
    echo "$ufw_status"

    if echo "$ufw_status" | grep -q "active"; then
        if sudo ufw status | grep -q '8080'; then
            echo "✅ Porta 8080 está LIBERADA no firewall"
            sudo ufw status | grep 8080
        else
            echo "⚠️  Porta 8080 NÃO está liberada no firewall"
            echo ""
            echo "Para liberar, execute:"
            echo "  sudo ufw allow 8080/tcp"
            echo "  sudo ufw reload"
        fi
    else
        echo "ℹ️  Firewall está inativo"
    fi
else
    echo "ℹ️  UFW não está instalado"
fi
echo ""

# 6. URLs de acesso
echo "=========================================="
echo "🌐 COMO ACESSAR O SISTEMA"
echo "=========================================="
echo ""
echo "No SERVIDOR (SSH):"
echo "  curl http://localhost:8080/api/status"
echo ""
echo "Do SEU COMPUTADOR (escolha um IP abaixo):"
for ip in $(hostname -I); do
    echo "  http://$ip:8080"
done
echo ""

# 7. Instruções finais
echo "=========================================="
echo "📋 PRÓXIMOS PASSOS"
echo "=========================================="
echo ""
echo "1. Se a porta 8080 está bloqueada no firewall:"
echo "   sudo ufw allow 8080/tcp"
echo "   sudo ufw reload"
echo ""
echo "2. Teste do seu computador:"
echo "   Abra o navegador em: http://IP_DO_SERVIDOR:8080"
echo ""
echo "3. Se ainda não funcionar, configure SSH Tunnel:"
echo "   ssh -L 8080:localhost:8080 usuario@servidor"
echo "   Depois acesse: http://localhost:8080"
echo ""
