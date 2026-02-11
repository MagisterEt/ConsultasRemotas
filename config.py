"""Configurações centralizadas do sistema"""
import os

# Tentar carregar .env se existir
try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass

# === AZURE / GRAPH ===
AZURE_CLIENT_ID = os.getenv("AZURE_CLIENT_ID", "451cccf3-6015-40c7-a170-4f3569580519")
AZURE_CLIENT_SECRET = os.getenv("AZURE_CLIENT_SECRET", "")
AZURE_TENANT_ID = os.getenv("AZURE_TENANT_ID", "01ee7ae0-42ba-41a5-bb7b-8d4af8db7f07")

# === SHAREPOINT ===
SHAREPOINT_SITE_ID = os.getenv("SHAREPOINT_SITE_ID", "mailadventistas.sharepoint.com,100852d9-d173-4ee5-8db2-52ca8fcbd2ca,b389c9d9-6db0-4b39-a78f-442e137797dc")
SHAREPOINT_DRIVE_ID = os.getenv("SHAREPOINT_DRIVE_ID", "b!2VIIEHPR5U6NslLKj8vSytnJibOwbTlLp49ELhN3l9wvLr3QX4FHQrR0VdJwCbw6")
SHAREPOINT_PASTA_DESTINO = "Consultas_Remotas"

# === SQL SERVER ===
SQL_USER = os.getenv("SQL_USER", "net.bi")
SQL_PASSWORD = os.getenv("SQL_PASSWORD", "")
SQL_USER_APS = os.getenv("SQL_USER_APS", "USeB_000_PBI")
SQL_PASSWORD_APS = os.getenv("SQL_PASSWORD_APS", "")

# === SERVIDOR WEB ===
WEB_PORT = int(os.getenv("WEB_PORT", 8080))
WEB_HOST = os.getenv("WEB_HOST", "0.0.0.0")
SQL_SERVER_PORT = int(os.getenv("SQL_SERVER_PORT", 5555))
SQL_SERVER_HOST = os.getenv("SQL_SERVER_HOST", "localhost")
TIMEOUT_CONEXAO = int(os.getenv("TIMEOUT_CONEXAO", 60))
DEBUG_MODE = os.getenv("DEBUG_MODE", "false").lower() == "true"
SECRET_KEY = os.getenv("SECRET_KEY", "mude-esta-chave-em-producao")

# === SERVIDORES SQL ===
SERVIDORES = [
    '10.30.11.2', '10.31.11.2', '10.32.11.2', '10.33.11.2', '10.34.11.2',
    '10.35.11.2', '10.36.11.2', '10.37.11.2', '10.38.11.2', '10.39.11.2',
    '10.33.211.2', '10.31.24.2', '10.32.24.2', '10.37.24.2', '10.31.42.2'
]

# === MAPEAMENTO ENTIDADES (FONTE ÚNICA) ===
ENTIDADES_POR_SERVIDOR = {
    '10.30.11.2': ['3011', '3013', '3021', '3093'],
    '10.31.11.2': ['3111', '3112', '3121', '3122', '3123', '3141', '3151', '3161'],
    '10.32.11.2': ['3211', '3213', '3221', '3293', '3251'],
    '10.33.11.2': ['3311', '3313', '3321', '3393'],
    '10.34.11.2': ['3411', '3413', '3421', '3493'],
    '10.35.11.2': ['3511', '3513', '3521', '3593', '3541', '3153'],
    '10.36.11.2': ['3611', '3613', '3621', '3693'],
    '10.37.11.2': ['3711', '3713', '3721', '3793'],
    '10.38.11.2': ['3811', '3813', '3821', '3893'],
    '10.39.11.2': ['3911', '3913', '3921', '3993'],
    '10.33.211.2': ['33211', '33213', '33221', '33293'],
    '10.31.24.2': ['3124', '3129'],
    '10.32.24.2': ['3224'],
    '10.37.24.2': ['3724'],
    '10.31.42.2': ['3154'],
}

# Mapeamento inverso: entidade -> servidor
SERVIDOR_POR_ENTIDADE = {
    ent: srv for srv, ents in ENTIDADES_POR_SERVIDOR.items() for ent in ents
}


def get_connection_string(servidor: str, database: str = "AASI") -> str:
    """Gera string de conexão para servidor SQL"""
    if database in ("APS", "Mineiracao_APS"):
        user, pwd = SQL_USER_APS, SQL_PASSWORD_APS
    else:
        user, pwd = SQL_USER, SQL_PASSWORD
    
    return (f"DRIVER={{ODBC Driver 18 for SQL Server}};"
            f"SERVER={servidor};DATABASE={database};"
            f"UID={user};PWD={pwd};TrustServerCertificate=yes")


def get_all_connection_strings(database: str = "AASI") -> dict:
    """Retorna dict de conexões para todos os servidores"""
    return {srv: get_connection_string(srv, database) for srv in SERVIDORES}