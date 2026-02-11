"""Módulo de Formatação de Dados para Exibição"""
import pandas as pd
from datetime import datetime

# Mapeamento de colunas SQL para nomes em português
MAPA_COLUNAS = {
    # Identificadores
    'IDEntidade': 'Entidade',
    'entity_code': 'Entidade',
    'entidade': 'Entidade',
    'Entidade': 'Entidade',
    
    # Datas
    'DataLote': 'Data do Lote',
    'date_in': 'Data Entrada',
    'date_out': 'Data Saída',
    'Data': 'Data',
    'date': 'Data',
    
    # Lotes
    'Type_Document': 'Tipo de Lote',
    'TipoLote': 'Tipo de Lote',
    'NúmeroLote': 'Número do Lote',
    'NomeLote': 'Nome do Lote',
    'code': 'Código',
    'char_0': 'Descrição',
    'reference': 'Lote',
    'Lote': 'Lote',
    
    # Período
    'year': 'Ano',
    'Ano': 'Ano',
    'period': 'Período',
    'Período': 'Período',
    'Periodo': 'Período',
    'Mes': 'Mês',
    
    # Usuário
    'usuariocriado': 'Criado Por',
    'QuemCriou': 'Criado Por',
    
    # Contas
    'IDConta': 'Código Conta',
    'chart_code': 'Código Conta',
    'Conta': 'Conta',
    'chart_name': 'Nome da Conta',
    'NomeConta': 'Nome da Conta',
    'CodigoConta': 'Código Conta',
    
    # Subcontas
    'IDSubConta': 'Código SubConta',
    'tag_code': 'Código SubConta',
    'SubConta': 'SubConta',
    'tag_name': 'Nome SubConta',
    'SubContaNome': 'Nome SubConta',
    
    # Fundos
    'IDFundo': 'Código Fundo',
    'fund_code': 'Código Fundo',
    'Fundo': 'Fundo',
    'fund_name': 'Nome do Fundo',
    
    # Departamentos
    'IDDepartamento': 'Código Depto',
    'department_code': 'Código Depto',
    'Departamento': 'Departamento',
    'NomeDepartamento': 'Nome Departamento',
    'department_name': 'Nome Departamento',
    
    # Valores
    'Saldo_Legal': 'Saldo',
    'Valor': 'Valor',
    'value': 'Valor',
    'ValorNum': 'Valor',
    'Totalizador': 'Total',
    'saldo_totalAPS': 'Saldo APS',
    'saldo_totalAASI': 'Saldo AASI',
    'Diferenca': 'Diferença',
    
    # Imobilizado
    'Codigo': 'Código',
    'Descricao': 'Descrição',
    'DescricaoAdicional': 'Descrição Adicional',
    'Secao': 'Seção',
    'NF_Numero': 'Número NF',
    'fa_char_0': 'Número NF',
    'fa_char_1': 'Descrição Adicional',
    'section': 'Seção',
    'name': 'Nome',
    
    # Baixas de Imobilizado
    'DataBaixa': 'Data da Baixa',
    'DepreciacaoAcumulada': 'Depreciação Acumulada',
    'ValorLiquido': 'Valor Líquido',
    'Motivo': 'Motivo',
    
    # Origem
    'Origem': 'Origem',
}


def renomear_colunas(df: pd.DataFrame) -> pd.DataFrame:
    """Renomeia colunas do DataFrame para português"""
    if df.empty:
        return df
    
    novas_colunas = {}
    for col in df.columns:
        if col in MAPA_COLUNAS:
            novas_colunas[col] = MAPA_COLUNAS[col]
        else:
            # Manter original se não estiver no mapa
            novas_colunas[col] = col
    
    return df.rename(columns=novas_colunas)


def formatar_data_ptbr(valor):
    """Formata data para dd/mm/yyyy"""
    if pd.isna(valor) or valor is None:
        return ''
    
    if isinstance(valor, str):
        # Tentar parsear string
        for fmt in ['%Y-%m-%d %H:%M:%S', '%Y-%m-%d', '%Y-%m-%dT%H:%M:%S']:
            try:
                dt = datetime.strptime(valor.split('.')[0], fmt)
                return dt.strftime('%d/%m/%Y')
            except:
                continue
        return valor
    
    if hasattr(valor, 'strftime'):
        return valor.strftime('%d/%m/%Y')
    
    return str(valor)


def formatar_numero_ptbr(valor, casas=2):
    """Formata número para padrão brasileiro (1.234,56)"""
    if pd.isna(valor) or valor is None:
        return ''
    
    try:
        num = float(valor)
        # Formatar com separador de milhar e decimal brasileiro
        formatado = f"{num:,.{casas}f}"
        # Trocar , por X, depois . por , e X por .
        formatado = formatado.replace(',', 'X').replace('.', ',').replace('X', '.')
        return formatado
    except:
        return str(valor)


def formatar_dataframe(df: pd.DataFrame) -> pd.DataFrame:
    """Formata DataFrame completo: renomeia colunas, formata datas e números"""
    if df.empty:
        return df
    
    df_fmt = df.copy()
    
    # Identificar e formatar colunas
    for col in df_fmt.columns:
        dtype = df_fmt[col].dtype
        col_lower = col.lower()
        
        # Detectar colunas de data pelo nome ou tipo
        is_date_col = (
            pd.api.types.is_datetime64_any_dtype(dtype) or
            'data' in col_lower or 
            'date' in col_lower or
            col_lower in ['datalote', 'date_in', 'date_out']
        )
        
        # Detectar colunas de valor monetário pelo nome
        is_money_col = (
            'valor' in col_lower or 
            'saldo' in col_lower or 
            'total' in col_lower or
            'diferenca' in col_lower or
            col_lower in ['value', 'totalizador']
        )
        
        if is_date_col:
            # Converter cada valor para string formatada
            df_fmt[col] = df_fmt[col].apply(formatar_data_ptbr).astype(str)
        elif is_money_col:
            # Formatar como moeda brasileira - funciona com qualquer tipo numérico
            df_fmt[col] = df_fmt[col].apply(lambda x: formatar_numero_ptbr(x, 2)).astype(str)
        elif pd.api.types.is_float_dtype(dtype):
            # Outros floats também formatados
            df_fmt[col] = df_fmt[col].apply(lambda x: formatar_numero_ptbr(x, 2)).astype(str)
        elif dtype == 'object':
            # Verificar se é coluna numérica disfarçada (Decimal, etc)
            try:
                primeiro_valor = df_fmt[col].dropna().iloc[0] if len(df_fmt[col].dropna()) > 0 else None
                if primeiro_valor is not None and hasattr(primeiro_valor, '__float__'):
                    df_fmt[col] = df_fmt[col].apply(lambda x: formatar_numero_ptbr(x, 2)).astype(str)
            except:
                pass
    
    # Renomear colunas para português
    df_fmt = renomear_colunas(df_fmt)
    
    return df_fmt


def converter_para_json(df: pd.DataFrame, formatar=True) -> tuple:
    """
    Converte DataFrame para lista de dicts prontos para JSON.
    Retorna (dados, colunas)
    """
    if df.empty:
        return [], []
    
    if formatar:
        df_fmt = formatar_dataframe(df)
    else:
        df_fmt = df.copy()
        # Apenas converter tipos problemáticos
        for col in df_fmt.columns:
            dtype = df_fmt[col].dtype
            if pd.api.types.is_datetime64_any_dtype(dtype):
                df_fmt[col] = df_fmt[col].apply(formatar_data_ptbr).astype(str)
    
    # Substituir 'nan' e 'None' por string vazia
    df_fmt = df_fmt.replace(['nan', 'None', 'NaT'], '')
    df_fmt = df_fmt.fillna('')
    
    colunas = df_fmt.columns.tolist()
    dados = df_fmt.to_dict('records')
    
    return dados, colunas