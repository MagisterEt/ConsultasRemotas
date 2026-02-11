#!/bin/bash
# Script para configurar crontab para execução automática
# ATTfolha - Transferências entre Entidades

# Obter diretório do script
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PYTHON_SCRIPT="$SCRIPT_DIR/ATTfolha.py"

echo "=========================================="
echo "Configuração do Crontab"
echo "=========================================="
echo ""
echo "Diretório: $SCRIPT_DIR"
echo "Script Python: $PYTHON_SCRIPT"
echo ""

# Verificar se o script Python existe
if [ ! -f "$PYTHON_SCRIPT" ]; then
    echo "❌ ERRO: Script Python não encontrado!"
    echo "   Esperado em: $PYTHON_SCRIPT"
    exit 1
fi

# Tornar o script executável
chmod +x "$PYTHON_SCRIPT"
echo "✅ Permissões de execução configuradas"

# Verificar python3
if ! command -v python3 &> /dev/null; then
    echo "❌ ERRO: python3 não encontrado!"
    exit 1
fi

PYTHON_PATH=$(which python3)
echo "✅ Python encontrado: $PYTHON_PATH"

echo ""
echo "=========================================="
echo "Opções de Agendamento"
echo "=========================================="
echo ""
echo "1) Todo dia 1º às 06:00 (mensal)"
echo "2) Toda segunda-feira às 08:00 (semanal)"
echo "3) Todos os dias às 07:00 (diário)"
echo "4) Personalizado"
echo ""
read -p "Escolha uma opção [1-4]: " opcao

case $opcao in
    1)
        CRON_SCHEDULE="0 6 1 * *"
        DESC="Todo dia 1º às 06:00"
        ;;
    2)
        CRON_SCHEDULE="0 8 * * 1"
        DESC="Toda segunda-feira às 08:00"
        ;;
    3)
        CRON_SCHEDULE="0 7 * * *"
        DESC="Todos os dias às 07:00"
        ;;
    4)
        echo ""
        echo "Formato: minuto hora dia mês dia_da_semana"
        echo "Exemplo: 0 6 1 * * (dia 1º às 06:00)"
        read -p "Digite o schedule cron: " CRON_SCHEDULE
        DESC="Personalizado: $CRON_SCHEDULE"
        ;;
    *)
        echo "❌ Opção inválida!"
        exit 1
        ;;
esac

# Criar entrada do crontab
CRON_ENTRY="$CRON_SCHEDULE cd $SCRIPT_DIR && $PYTHON_PATH $PYTHON_SCRIPT >> $SCRIPT_DIR/cron_output.log 2>&1"

echo ""
echo "=========================================="
echo "Entrada do Crontab"
echo "=========================================="
echo ""
echo "Agendamento: $DESC"
echo "$CRON_ENTRY"
echo ""

read -p "Deseja adicionar ao crontab? [S/n]: " confirma

if [[ $confirma =~ ^[Ss]$ ]] || [[ -z $confirma ]]; then
    # Backup do crontab atual
    crontab -l > /tmp/crontab_backup_$(date +%Y%m%d_%H%M%S).txt 2>/dev/null
    
    # Adicionar nova entrada
    (crontab -l 2>/dev/null; echo "# ATTfolha - Transferências entre Entidades"; echo "$CRON_ENTRY") | crontab -
    
    echo ""
    echo "✅ Crontab configurado com sucesso!"
    echo ""
    echo "Para visualizar: crontab -l"
    echo "Para editar: crontab -e"
    echo "Para remover: crontab -r"
    echo ""
    echo "Logs serão salvos em:"
    echo "  - $SCRIPT_DIR/ATTfolha.log"
    echo "  - $SCRIPT_DIR/cron_output.log"
else
    echo ""
    echo "❌ Operação cancelada"
    echo ""
    echo "Para adicionar manualmente, execute:"
    echo "crontab -e"
    echo ""
    echo "E adicione a linha:"
    echo "$CRON_ENTRY"
fi
