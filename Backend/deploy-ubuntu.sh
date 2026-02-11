#!/bin/bash
# ========================================
# SCRIPT DE DEPLOYMENT PARA UBUNTU SERVER
# ========================================

set -e

echo "========================================="
echo "Deploy ConsultasRemotas API - Ubuntu"
echo "========================================="

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Verificar se está rodando como root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}Por favor, execute como root (sudo)${NC}"
    exit 1
fi

# Configurações
APP_NAME="consultas-remotas"
APP_DIR="/opt/consultas-remotas"
SERVICE_USER="consultas"
DOTNET_VERSION="8.0"

# 1. Instalar .NET 8 SDK/Runtime
echo -e "${GREEN}[1/7] Instalando .NET ${DOTNET_VERSION}...${NC}"
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

apt-get update
apt-get install -y dotnet-sdk-8.0 dotnet-runtime-8.0 aspnetcore-runtime-8.0

# 2. Instalar ODBC Driver para SQL Server
echo -e "${GREEN}[2/7] Instalando ODBC Driver para SQL Server...${NC}"
curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add -
curl https://packages.microsoft.com/config/ubuntu/22.04/prod.list > /etc/apt/sources.list.d/mssql-release.list

apt-get update
ACCEPT_EULA=Y apt-get install -y msodbcsql18 unixodbc-dev

# 3. Criar usuário do sistema
echo -e "${GREEN}[3/7] Criando usuário do sistema...${NC}"
if ! id "$SERVICE_USER" &>/dev/null; then
    useradd -r -s /bin/false -d /opt/consultas-remotas $SERVICE_USER
    echo -e "${YELLOW}Usuário $SERVICE_USER criado${NC}"
else
    echo -e "${YELLOW}Usuário $SERVICE_USER já existe${NC}"
fi

# 4. Criar diretórios
echo -e "${GREEN}[4/7] Criando diretórios...${NC}"
mkdir -p $APP_DIR
mkdir -p $APP_DIR/logs
mkdir -p $APP_DIR/wwwroot
mkdir -p /etc/consultas-remotas

# 5. Build da aplicação
echo -e "${GREEN}[5/7] Compilando aplicação...${NC}"
cd ConsultasRemotas.Api
dotnet publish -c Release -o $APP_DIR/publish --runtime linux-x64 --self-contained false

# 6. Configurar permissões
echo -e "${GREEN}[6/7] Configurando permissões...${NC}"
chown -R $SERVICE_USER:$SERVICE_USER $APP_DIR
chmod 755 $APP_DIR
chmod 755 $APP_DIR/publish
chmod 644 $APP_DIR/publish/appsettings.json

# 7. Instalar e iniciar o serviço systemd
echo -e "${GREEN}[7/7] Configurando systemd service...${NC}"

cat > /etc/systemd/system/$APP_NAME.service <<EOF
[Unit]
Description=ConsultasRemotas API - Sistema de Consultas SQL Distribuídas
After=network.target

[Service]
Type=notify
User=$SERVICE_USER
Group=$SERVICE_USER
WorkingDirectory=$APP_DIR/publish
ExecStart=/usr/bin/dotnet $APP_DIR/publish/ConsultasRemotas.Api.dll

# Reiniciar em caso de falha
Restart=always
RestartSec=10

# Variáveis de ambiente
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_gcServer=1
Environment=DOTNET_GCHeapHardLimit=2000000000

# Limites de recursos
LimitNOFILE=65536
LimitNPROC=4096

# Logs
StandardOutput=journal
StandardError=journal
SyslogIdentifier=$APP_NAME

# Security
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=$APP_DIR/logs

[Install]
WantedBy=multi-user.target
EOF

# Recarregar systemd e habilitar o serviço
systemctl daemon-reload
systemctl enable $APP_NAME.service

echo ""
echo -e "${GREEN}=========================================${NC}"
echo -e "${GREEN}Deployment concluído com sucesso!${NC}"
echo -e "${GREEN}=========================================${NC}"
echo ""
echo "Comandos úteis:"
echo -e "  Iniciar:    ${YELLOW}sudo systemctl start $APP_NAME${NC}"
echo -e "  Parar:      ${YELLOW}sudo systemctl stop $APP_NAME${NC}"
echo -e "  Reiniciar:  ${YELLOW}sudo systemctl restart $APP_NAME${NC}"
echo -e "  Status:     ${YELLOW}sudo systemctl status $APP_NAME${NC}"
echo -e "  Logs:       ${YELLOW}sudo journalctl -u $APP_NAME -f${NC}"
echo ""
echo "A aplicação estará disponível em: http://localhost:8080"
echo ""
echo -e "${YELLOW}IMPORTANTE: Configure as variáveis de ambiente em:${NC}"
echo -e "${YELLOW}$APP_DIR/publish/appsettings.json${NC}"
echo ""
