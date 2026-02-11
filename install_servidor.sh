#!/bin/bash
#
# Script de instalação do Servidor SQL
# Execute com: bash install_servidor.sh
#

set -e

echo "=========================================="
echo "  INSTALAÇÃO DO SERVIDOR SQL"
echo "=========================================="
echo ""

# Verifica se está rodando como root
if [ "$EUID" -eq 0 ]; then 
    echo "⚠️  Por favor, NÃO execute como root."
    echo "   O script usará sudo quando necessário."
    exit 1
fi

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# 1. Atualizar sistema
echo "1. Atualizando sistema..."
sudo apt update
print_success "Sistema atualizado"

# 2. Instalar Python e pip
echo ""
echo "2. Instalando Python e pip..."
sudo apt install -y python3 python3-pip
print_success "Python instalado"

# 3. Instalar dependências ODBC
echo ""
echo "3. Instalando drivers ODBC..."
sudo apt install -y unixodbc unixodbc-dev

# Adicionar repositório Microsoft
if [ ! -f /etc/apt/sources.list.d/mssql-release.list ]; then
    curl https://packages.microsoft.com/keys/microsoft.asc | sudo apt-key add -
    curl https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/prod.list | sudo tee /etc/apt/sources.list.d/mssql-release.list
    sudo apt update
fi

# Instalar driver ODBC do SQL Server
sudo ACCEPT_EULA=Y apt install -y msodbcsql17
print_success "Drivers ODBC instalados"

# 4. Instalar bibliotecas Python
echo ""
echo "4. Instalando bibliotecas Python..."
pip3 install --user -r requirements.txt
print_success "Bibliotecas Python instaladas"

# 5. Configurar banco de dados
echo ""
echo "5. Configuração do banco de dados"
if [ ! -f db_config.json ] || [ "$(cat db_config.json | grep 'seu_banco')" ]; then
    print_warning "Arquivo db_config.json precisa ser configurado!"
    echo ""
    read -p "Deseja configurar agora? (s/n): " configurar
    
    if [ "$configurar" = "s" ] || [ "$configurar" = "S" ]; then
        read -p "Servidor SQL (exemplo: localhost): " db_server
        read -p "Nome do banco: " db_name
        read -p "Usuário: " db_user
        read -sp "Senha: " db_pass
        echo ""
        
        cat > db_config.json <<EOF
{
    "DRIVER": "{ODBC Driver 17 for SQL Server}",
    "SERVER": "$db_server",
    "DATABASE": "$db_name",
    "UID": "$db_user",
    "PWD": "$db_pass",
    "TrustServerCertificate": "yes"
}
EOF
        chmod 600 db_config.json
        print_success "Configuração do banco salva"
    else
        print_warning "Configure manualmente o arquivo db_config.json antes de iniciar o servidor"
    fi
fi

# 6. Configurar firewall
echo ""
echo "6. Configurando firewall..."
read -p "Deseja configurar o firewall agora? (s/n): " config_firewall

if [ "$config_firewall" = "s" ] || [ "$config_firewall" = "S" ]; then
    if command -v ufw &> /dev/null; then
        echo "Abrindo porta 5555 no UFW..."
        sudo ufw allow 5555/tcp
        print_success "Firewall configurado (UFW)"
    elif command -v firewall-cmd &> /dev/null; then
        echo "Abrindo porta 5555 no firewalld..."
        sudo firewall-cmd --permanent --add-port=5555/tcp
        sudo firewall-cmd --reload
        print_success "Firewall configurado (firewalld)"
    else
        print_warning "Nenhum firewall detectado (UFW ou firewalld)"
    fi
else
    print_warning "Configure manualmente o firewall para permitir conexões na porta 5555"
fi

# 7. Criar serviço systemd (opcional)
echo ""
echo "7. Criar serviço systemd (opcional)"
read -p "Deseja criar um serviço para iniciar automaticamente? (s/n): " criar_servico

if [ "$criar_servico" = "s" ] || [ "$criar_servico" = "S" ]; then
    SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
    
    sudo tee /etc/systemd/system/servidor-sql.service > /dev/null <<EOF
[Unit]
Description=Servidor SQL Remoto
After=network.target

[Service]
Type=simple
User=$USER
WorkingDirectory=$SCRIPT_DIR
ExecStart=/usr/bin/python3 $SCRIPT_DIR/servidor_sql.py
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

    sudo systemctl daemon-reload
    sudo systemctl enable servidor-sql.service
    print_success "Serviço systemd criado"
    
    echo ""
    echo "Comandos úteis:"
    echo "  Iniciar:  sudo systemctl start servidor-sql"
    echo "  Parar:    sudo systemctl stop servidor-sql"
    echo "  Status:   sudo systemctl status servidor-sql"
    echo "  Logs:     sudo journalctl -u servidor-sql -f"
fi

# 8. Testar instalação
echo ""
echo "8. Testando instalação..."
python3 -c "import pyodbc, pandas" 2>/dev/null && print_success "Bibliotecas OK" || print_error "Erro nas bibliotecas"

# Verificar drivers ODBC
if odbcinst -q -d | grep -q "ODBC Driver"; then
    print_success "Drivers ODBC OK"
else
    print_error "Drivers ODBC não encontrados"
fi

# Resumo final
echo ""
echo "=========================================="
echo "  INSTALAÇÃO CONCLUÍDA"
echo "=========================================="
echo ""
echo "📝 Próximos passos:"
echo ""
echo "1. Configure o arquivo db_config.json (se ainda não fez)"
echo "2. Inicie o servidor:"
echo "   python3 servidor_sql.py"
echo ""
echo "   Ou se criou o serviço systemd:"
echo "   sudo systemctl start servidor-sql"
echo ""
echo "3. No cliente, use o IP deste servidor:"
IP_ADDR=$(hostname -I | awk '{print $1}')
echo "   $IP_ADDR"
echo ""
echo "4. Verifique os logs em:"
echo "   tail -f servidor_sql.log"
echo ""
print_success "Sistema pronto para uso!"
