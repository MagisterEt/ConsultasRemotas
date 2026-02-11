"""Configuração de Consultas SQL Pré-Definidas"""
from datetime import datetime
from config import ENTIDADES_POR_SERVIDOR

# Subcontas específicas para consultas de cartão
SUBCONTAS_CARTAO = {
    "3011": ["2"], "3013": ["1","2"], "3021": ["1","1010"],
    "3124": ["1","2","1010"], "3211": ["1","2"], "3213": ["1","2"],
    "3221": ["1","1010"], "3224": ["1","2","1010"], "3251": ["1"],
    "3311": ["1","2"], "3313": ["1","2"], "3321": ["1","2","1010"],
    "3413": ["1","2"], "3421": ["1010"], "3511": ["1","2"],
    "3513": ["1","2"], "3521": ["1","1010"], "3541": ["1"],
    "3611": ["1","2"], "3613": ["1","2"], "3621": ["1","1010"],
    "3711": ["1","2"], "3713": ["1","2"], "3721": ["1","2","1010"],
    "3724": ["1","1010"], "3811": ["2"], "3813": ["1","2"],
    "3821": ["1010"], "3911": ["1","2"], "3913": ["1","2"],
    "3921": ["1","1010"], "33211": ["2"], "33213": ["1"],
    "33221": ["1","2","1010"],
}

# Entidades para consultas de razão/cartão (subset)
ENTIDADES_RAZAO = {
    '10.30.11.2': ['3011', '3013', '3021'],
    '10.31.24.2': ['3124'],
    '10.32.11.2': ['3211', '3213', '3221', '3251'],
    '10.32.24.2': ['3224'],
    '10.33.11.2': ['3311', '3313', '3321'],
    '10.34.11.2': ['3413', '3421'],
    '10.35.11.2': ['3511', '3513', '3521', '3541'],
    '10.36.11.2': ['3611', '3613', '3621'],
    '10.37.11.2': ['3711', '3713', '3721'],
    '10.37.24.2': ['3724'],
    '10.38.11.2': ['3811', '3813', '3821'],
    '10.39.11.2': ['3911', '3913', '3921'],
    '10.33.211.2': ['33211', '33213', '33221']
}

CONSULTAS_PREDEFINIDAS = {
    'lotes_sem_anexo': {
        'nome': '📎 Lotes Sem Anexo',
        'descricao': 'Lista lotes sem anexo em todos os servidores',
        'tipo': 'multi_servidor',
        'requer_entidade': False,
        'requer_periodo': False,
        'requer_ano': False,
        'entidades_por_servidor': ENTIDADES_POR_SERVIDOR,
        'sql_template': """
            SELECT l.entidade as Entidade, l.Type_Document as TipoLote,
                   l.code as NúmeroLote, l.char_0 as NomeLote, l.DataLote,
                   l.year as Ano, l.period as Período, l.usuariocriado as QuemCriou
            FROM Vw_Lotes AS l
            WHERE l.Attachments = 0 AND l.year >= 2025 AND l.cache = 0
              AND l.Type_Document NOT LIKE 'PG%' AND l.Type_Document NOT LIKE 'MT%'
              AND l.Type_Document NOT LIKE 'MR%' AND l.Type_Document NOT LIKE 'ED%'
        """
    },
    
    'aquisicoes': {
        'nome': '📈 Aquisições',
        'descricao': 'Valores positivos de aquisição de imobilizado',
        'tipo': 'single_servidor',
        'requer_entidade': True,
        'requer_periodo': True,
        'requer_ano': True,
        'sql_template': """
            DECLARE @ano INT = {ano}, @mes INT = {periodo}, @ent VARCHAR(10) = '{entidade}'
            SELECT f.code AS Codigo, f.name AS Descricao, f.fa_char_1 AS DescricaoAdicional,
                   f.date_in AS Data, f.section AS Secao, f.fa_char_0 AS NF_Numero,
                   ISNULL(SUM(oi.value), 0) AS Valor
            FROM v_fixed_asset_iud f
            INNER JOIN v_department d ON d.id_department = f.id_department
            INNER JOIN v_entity e ON e.id_entity = f.id_entity
            LEFT JOIN (
                SELECT oi.id_tag, oi.value, p.year, p.period FROM year_document_item oi
                INNER JOIN year_document od ON od.id_document = oi.id_document
                INNER JOIN Period p ON p.id_period = od.id_period WHERE oi.value > 0
                UNION ALL
                SELECT oi.id_tag, oi.value, p.year, p.period FROM old_document_item oi
                INNER JOIN Old_Document od ON od.id_document = oi.id_document
                INNER JOIN Period p ON p.id_period = od.id_period WHERE oi.value > 0
                UNION ALL
                SELECT oi.id_tag, oi.value, p.year, p.period FROM open_document_item oi
                INNER JOIN open_document od ON od.id_document = oi.id_document
                INNER JOIN Period p ON p.id_period = od.id_period WHERE oi.value > 0
            ) oi ON oi.id_tag = f.id_tag AND oi.year = @ano AND oi.period = @mes
            WHERE e.entity_code = @ent AND f.date_in IS NOT NULL
                AND YEAR(f.date_in) = @ano AND MONTH(f.date_in) = @mes AND f.date_out IS NULL
            GROUP BY f.code, f.name, f.fa_char_1, f.date_in, f.section, f.fa_char_0
            HAVING SUM(oi.value) > 0 ORDER BY f.date_in, f.code
        """
    },
    
    'baixas': {
        'nome': '📉 Baixas',
        'descricao': 'Baixas de imobilizado no período',
        'tipo': 'single_servidor',
        'requer_entidade': True,
        'requer_periodo': True,
        'requer_ano': True,
        'sql_template': """
            DECLARE @ano INT = {ano}
            DECLARE @mes INT = {periodo}
            DECLARE @ent VARCHAR(10) = '{entidade}'
            DECLARE @id_entity INT
            DECLARE @id_system INT = 1939
            DECLARE @id_section INT
            DECLARE @id_fa_section INT

            SELECT @id_entity = id_entity FROM v_entity WHERE entity_code = @ent
            SELECT @id_section = id_object FROM object WHERE id_type_object = 'virtual_relationship' AND object = 'Section'
            SELECT @id_fa_section = id_object FROM object WHERE id_type_object = 'virtual_relationship' AND object = 'Fixed_Asset_Section'

            ;WITH accounting AS (
                SELECT 
                    id_tag,
                    data_baixa = MAX(data_baixa),
                    aquisicao = SUM(aquisicao),
                    baixa = SUM(baixa),
                    depreciacao = SUM(depreciacao) * -1
                FROM (
                    SELECT
                        odi.id_tag,
                        data_baixa = CASE WHEN ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=1 THEN od.date ELSE NULL END,
                        Aquisicao = CASE WHEN ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=0 THEN odi.value ELSE 0 END,
                        Baixa = CASE WHEN ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=1 THEN odi.value ELSE 0 END,
                        Depreciacao = CASE WHEN ISNULL(c.bit_3,0)=1 AND ISNULL(c.bit_2,0)=0 THEN odi.value ELSE 0 END
                    FROM open_document od
                    INNER JOIN open_document_item odi ON od.id_document = odi.id_document
                    INNER JOIN chart c ON c.id_chart = odi.id_chart
                    WHERE od.id_entity = @id_entity AND od.id_system = @id_system
                        AND ((ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=0) OR 
                             (ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=1) OR 
                             (ISNULL(c.bit_3,0)=1))
                    
                    UNION ALL
                    
                    SELECT
                        odi.id_tag,
                        data_baixa = CASE WHEN ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=1 THEN od.date ELSE NULL END,
                        Aquisicao = CASE WHEN ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=0 THEN odi.value ELSE 0 END,
                        Baixa = CASE WHEN ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=1 THEN odi.value ELSE 0 END,
                        Depreciacao = CASE WHEN ISNULL(c.bit_3,0)=1 AND ISNULL(c.bit_2,0)=0 THEN odi.value ELSE 0 END
                    FROM year_document od
                    INNER JOIN year_document_item odi ON od.id_document = odi.id_document
                    INNER JOIN chart c ON c.id_chart = odi.id_chart
                    WHERE od.id_entity = @id_entity AND od.id_system = @id_system
                        AND ((ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=0) OR 
                             (ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=1) OR 
                             (ISNULL(c.bit_3,0)=1))
                    
                    UNION ALL
                    
                    SELECT
                        odi.id_tag,
                        data_baixa = CASE WHEN ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=1 THEN od.date ELSE NULL END,
                        Aquisicao = CASE WHEN ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=0 THEN odi.value ELSE 0 END,
                        Baixa = CASE WHEN ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=1 THEN odi.value ELSE 0 END,
                        Depreciacao = CASE WHEN ISNULL(c.bit_3,0)=1 AND ISNULL(c.bit_2,0)=0 THEN odi.value ELSE 0 END
                    FROM old_document od
                    INNER JOIN old_document_item odi ON od.id_document = odi.id_document
                    INNER JOIN chart c ON c.id_chart = odi.id_chart
                    WHERE od.id_entity = @id_entity AND od.id_system = @id_system
                        AND ((ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=0) OR 
                             (ISNULL(c.bit_0,0)=1 AND ISNULL(c.bit_2,0)=1) OR 
                             (ISNULL(c.bit_3,0)=1))
                ) dados
                GROUP BY id_tag
                HAVING (SUM(aquisicao) + SUM(baixa)) = 0
            )
            SELECT 
                vf.code AS Codigo,
                vf.name AS Descricao,
                vf.char_1 AS DescricaoAdicional,
                a.data_baixa AS DataBaixa,
                (SELECT ISNULL(vrs.char_1 + ' ', '') + ISNULL('(' + vrs.char_0 + ')', '')
                 FROM virtual_relationship_row vr
                 INNER JOIN virtual_relationship_row vrs ON vrs.id_virtual_relationship_row = vr.t2_pk
                 WHERE vr.t1_pk = vf.id_fixed_asset
                     AND vrs.id_virtual_relationship = @id_section
                     AND vr.id_virtual_relationship = @id_fa_section) AS Secao,
                ABS(a.aquisicao) AS Valor,
                ABS(a.depreciacao) AS DepreciacaoAcumulada,
                ABS(a.aquisicao) - ABS(a.depreciacao) AS ValorLiquido,
                vf.char_2 AS Motivo
            FROM v_fixed_asset vf
            INNER JOIN accounting a ON vf.id_fixed_asset = a.id_tag
            INNER JOIN chart c ON vf.id_chart = c.id_chart
            WHERE 
                vf.id_entity = @id_entity
                AND a.data_baixa IS NOT NULL
                AND YEAR(a.data_baixa) = @ano
                AND MONTH(a.data_baixa) = @mes
            ORDER BY a.data_baixa DESC, vf.code
        """
    },
    
    'ficha_loja': {
        'nome': '🏪 Ficha Loja (Balancete)',
        'descricao': 'Balancete consolidado de lojas por departamento',
        'tipo': 'multi_servidor',
        'requer_entidade': False,
        'requer_periodo': True,
        'requer_ano': True,
        'entidades_por_servidor': ENTIDADES_POR_SERVIDOR,
        'sql_template': """
            DECLARE @Entidade1 varchar(10), @Entidade2 varchar(10), @Entidade3 varchar(10)
            DECLARE @Entidade4 varchar(10), @Entidade5 varchar(10), @Entidade6 varchar(10)
            DECLARE @Entidade7 varchar(10), @Entidade8 varchar(10)
            {entidades}
            
            -- SALDO INICIAL
            SELECT v_entity.entity_code AS IDEntidade, v_year_balance.year AS Ano, 
                   v_year_balance.period AS Mes, v_year_balance.chart_code AS IDConta, 
                   v_year_balance.chart_name AS Conta, v_year_balance.tag_code AS SubConta,
                   v_year_balance.tag_name AS SubContaNome,
                   ROUND(ISNULL(v_year_balance.cr_value_0,0) + ISNULL(v_year_balance.db_value_0,0),2) AS Saldo_Legal,
                   v_year_balance.department_code AS IDDepartamento, v_department.department_name AS NomeDepartamento
            FROM v_department 
            INNER JOIN (Chart INNER JOIN (v_entity INNER JOIN v_year_balance 
                ON v_entity.id_entity = v_year_balance.id_entity) ON Chart.id_chart = v_year_balance.id_chart) 
            ON (v_department.id_entity = v_year_balance.id_entity) AND (v_department.id_department = v_year_balance.id_department)
            WHERE v_department.only_accrual='0' AND Chart.only_accrual='0' AND v_year_balance.year >= {ano}
                AND v_year_balance.chart_code IN ('1141001','1141005','3151001','3161001','3162010','3162013','3162014','3162015','3163001','3171001')
                AND v_year_balance.department_code <> '0' AND v_year_balance.period = '0'
                AND (v_entity.entity_code LIKE '%13' OR v_entity.entity_code = '3124' OR v_entity.entity_code LIKE '%224')
            
            UNION ALL
            
            -- BALANCETE ATUAL
            SELECT v_entity.entity_code, v_year_balance.year, v_year_balance.period, 
                   v_year_balance.chart_code, v_year_balance.chart_name, v_year_balance.tag_code,
                   v_year_balance.tag_name,
                   ROUND(ISNULL(v_year_balance.db_value_0,0) + ISNULL(v_year_balance.cr_value_0,0),2),
                   v_year_balance.department_code, v_department.department_name
            FROM v_department 
            INNER JOIN (Chart INNER JOIN (v_entity INNER JOIN v_year_balance 
                ON v_entity.id_entity = v_year_balance.id_entity) ON Chart.id_chart = v_year_balance.id_chart) 
            ON (v_department.id_entity = v_year_balance.id_entity) AND (v_department.id_department = v_year_balance.id_department)
            WHERE v_department.only_accrual='0' AND Chart.only_accrual='0' AND v_year_balance.year = {ano}
                AND v_year_balance.chart_code IN ('1141001','1141005','3151001','3151006','3161001','3161006','3162010','3162013','3162014','3162015','3163001','3171001')
                AND v_year_balance.department_code <> '0' AND v_year_balance.period BETWEEN '1' AND '{periodo}'
                AND (v_entity.entity_code LIKE '%13' OR v_entity.entity_code = '3124' OR v_entity.entity_code LIKE '%224')
        """
    },
    
    'conferencia_13': {
        'nome': '💰 Conferência 13º Salário',
        'descricao': 'Conferência de provisão 13º - APS + AASI',
        'tipo': 'multi_banco',
        'requer_entidade': False,
        'requer_periodo': True,
        'requer_ano': True,
        'entidades_por_servidor': ENTIDADES_POR_SERVIDOR,
        'query_aps': """
            SELECT p.idEntidade AS Entidade,
                CASE WHEN p.codVerba IN ('92000','93000') THEN '2141001'
                     WHEN p.codVerba IN ('98600','98960') THEN '2141002' END AS Conta,
                SUM(p.value) AS saldo_totalAPS
            FROM pagamentos AS p
            WHERE p.ano >= {ano} AND p.mes <= {periodo}
                AND p.codVerba IN ('92000','93000','98600','98960')
            GROUP BY p.idEntidade, CASE WHEN p.codVerba IN ('92000','93000') THEN '2141001'
                     WHEN p.codVerba IN ('98600','98960') THEN '2141002' END
            ORDER BY p.idEntidade, Conta
        """,
        'query_aasi': """
            DECLARE @Entidade1 varchar(10), @Entidade2 varchar(10), @Entidade3 varchar(10)
            DECLARE @Entidade4 varchar(10), @Entidade5 varchar(10), @Entidade6 varchar(10)
            DECLARE @Entidade7 varchar(10), @Entidade8 varchar(10)
            {entidades}
            SELECT v_entity.entity_code AS Entidade, v_year_balance.chart_code AS Conta, 
                SUM(ISNULL(v_year_balance.cr_value_0,0) + ISNULL(v_year_balance.db_value_0,0)) AS saldo_totalAASI
            FROM v_department
            INNER JOIN (Chart INNER JOIN (v_entity INNER JOIN v_year_balance 
                ON v_entity.id_entity = v_year_balance.id_entity) ON Chart.id_chart = v_year_balance.id_chart)
            ON (v_department.id_entity = v_year_balance.id_entity) AND (v_department.id_department = v_year_balance.id_department)
            WHERE v_department.only_accrual='0' AND Chart.only_accrual='0'
                AND v_year_balance.year = {ano} AND v_year_balance.chart_code IN ('2141001','2141002')
                AND v_year_balance.department_code <> '0' AND v_year_balance.period <= {periodo}
                AND v_entity.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8)
            GROUP BY v_entity.entity_code, v_year_balance.year, v_year_balance.chart_code
        """
    },
    
    'razao_subcontas': {
        'nome': '📝 Razão SubContas (1139008)',
        'descricao': 'Razão da conta 1139008 filtrado por subcontas',
        'tipo': 'multi_servidor',
        'requer_entidade': False,
        'requer_periodo': True,
        'requer_ano': True,
        'requer_saldo_anterior': True,
        'subcontas_por_entidade': SUBCONTAS_CARTAO,
        'entidades_por_servidor': ENTIDADES_RAZAO,
        'sql_template': """
            DECLARE @Entidade1 varchar(10), @Entidade2 varchar(10), @Entidade3 varchar(10)
            DECLARE @Entidade4 varchar(10), @Entidade5 varchar(10), @Entidade6 varchar(10)
            DECLARE @Entidade7 varchar(10), @Entidade8 varchar(10)
            {entidades_set}
            
            SELECT e.entity_code AS Entidade, p.year AS Ano, p.period AS Periodo,
                   d.fund_code AS Fundo, d.department_code AS Departamento, d.chart_code AS Conta,
                   d.tag_code AS SubConta, d.date AS Data, d.reference AS Lote,
                   d.char_0 AS Descricao, d.value AS Valor, c.code AS CodigoConta, c.name AS NomeConta
            FROM v_open_document AS d
            INNER JOIN v_entity AS e ON e.id_entity = d.id_entity
            INNER JOIN Period AS p ON p.id_period = d.id_period
            INNER JOIN chart_of_accumulator AS coa ON coa.id_chart = d.id_chart
            INNER JOIN Chart AS c ON c.id_type_chart = coa.id_type_chart AND c.id_chart = coa.parent_id_chart
            WHERE p.year = {ano} AND p.period = {periodo} AND d.chart_code = '1139008'
                AND d.type_document_code NOT IN ('EA','AJ','MT') AND c.code = '1139008'
                AND e.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8)
                AND d.tag_code IN ({subcontas})
            
            UNION ALL
            
            SELECT e.entity_code, p.year, p.period, d.fund_code, d.department_code, d.chart_code,
                   d.tag_code, d.date, d.reference, d.char_0, d.value, c.code, c.name
            FROM v_year_document AS d
            INNER JOIN v_entity AS e ON e.id_entity = d.id_entity
            INNER JOIN Period AS p ON p.id_period = d.id_period
            INNER JOIN chart_of_accumulator AS coa ON coa.id_chart = d.id_chart
            INNER JOIN Chart AS c ON c.id_type_chart = coa.id_type_chart AND c.id_chart = coa.parent_id_chart
            WHERE p.year = {ano} AND p.period = {periodo} AND d.chart_code = '1139008'
                AND d.type_document_code NOT IN ('EA','AJ','MT') AND c.code = '1139008'
                AND e.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8)
                AND d.tag_code IN ({subcontas})
        """
    },
    
    'saldo_anterior': {
        'nome': '💳 Saldo Anterior - Cartão',
        'descricao': 'Saldo de cartão até X meses atrás',
        'tipo': 'multi_servidor',
        'requer_meses_atras': True,
        'requer_saldo_anterior': True,
        'subcontas_por_entidade': SUBCONTAS_CARTAO,
        'entidades_por_servidor': ENTIDADES_RAZAO,
        'sql_template': """
            DECLARE @Entidade1 varchar(10), @Entidade2 varchar(10), @Entidade3 varchar(10)
            DECLARE @Entidade4 varchar(10), @Entidade5 varchar(10), @Entidade6 varchar(10)
            DECLARE @Entidade7 varchar(10), @Entidade8 varchar(10)
            {entidades_set}
            DECLARE @DataLimite DATE = EOMONTH(GETDATE(), -{meses_atras})
            
            ;WITH Docs AS (
                SELECT e.entity_code AS Entidade, d.tag_code AS SubConta, d.value AS ValorNum, d.department_code AS Departamento
                FROM v_open_document AS d
                INNER JOIN v_entity AS e ON e.id_entity = d.id_entity
                WHERE d.date <= @DataLimite AND d.chart_code = '1139008'
                    AND d.type_document_code NOT IN ('EA','AJ','MT')
                    AND e.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8)
                    AND d.tag_code IN ({subcontas})
                UNION ALL
                SELECT e.entity_code, d.tag_code, d.value, d.department_code
                FROM v_year_document AS d
                INNER JOIN v_entity AS e ON e.id_entity = d.id_entity
                WHERE d.date <= @DataLimite AND d.chart_code = '1139008'
                    AND d.type_document_code NOT IN ('EA','AJ','MT')
                    AND e.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8)
                    AND d.tag_code IN ({subcontas})
                UNION ALL
                SELECT e.entity_code, d.tag_code, d.value, d.department_code
                FROM v_old_document AS d
                INNER JOIN v_entity AS e ON e.id_entity = d.id_entity
                WHERE d.date <= @DataLimite AND d.chart_code = '1139008'
                    AND d.type_document_code NOT IN ('EA','AJ','MT')
                    AND e.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8)
                    AND d.tag_code IN ({subcontas})
            )
            SELECT Entidade, SubConta, Departamento, SUM(ValorNum) AS Totalizador
            FROM Docs GROUP BY Entidade, SubConta, Departamento
        """
    },
    
    'futuro_otimizado': {
        'nome': '🔮 Futuro - Saldo até Data',
        'descricao': 'Saldo consolidado da conta 1139008 até uma data limite',
        'tipo': 'multi_servidor',
        'requer_data_limite': True,
        'requer_saldo_anterior': True,
        'subcontas_por_entidade': SUBCONTAS_CARTAO,
        'entidades_por_servidor': ENTIDADES_RAZAO,
        'sql_template': """
            DECLARE @DataLimite DATE = CAST('{data_limite}' AS DATE)
            DECLARE @Entidade1 varchar(10), @Entidade2 varchar(10), @Entidade3 varchar(10)
            DECLARE @Entidade4 varchar(10), @Entidade5 varchar(10), @Entidade6 varchar(10)
            DECLARE @Entidade7 varchar(10), @Entidade8 varchar(10)
            {entidades_set}
            
            ;WITH Docs AS (
                SELECT e.entity_code AS Entidade, d.tag_code AS SubConta, d.value AS ValorNum, d.department_code AS Departamento
                FROM v_open_document AS d INNER JOIN v_entity AS e ON e.id_entity = d.id_entity
                WHERE d.date <= @DataLimite AND d.chart_code = '1139008' AND d.type_document_code NOT IN ('EA','AJ','MT')
                    AND e.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8)
                    AND d.tag_code IN ({subcontas})
                UNION ALL
                SELECT e.entity_code, d.tag_code, d.value, d.department_code
                FROM v_year_document AS d INNER JOIN v_entity AS e ON e.id_entity = d.id_entity
                WHERE d.date <= @DataLimite AND d.chart_code = '1139008' AND d.type_document_code NOT IN ('EA','AJ','MT')
                    AND e.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8)
                    AND d.tag_code IN ({subcontas})
                UNION ALL
                SELECT e.entity_code, d.tag_code, d.value, d.department_code
                FROM v_old_document AS d INNER JOIN v_entity AS e ON e.id_entity = d.id_entity
                WHERE d.date <= @DataLimite AND d.chart_code = '1139008' AND d.type_document_code NOT IN ('EA','AJ','MT')
                    AND e.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8)
                    AND d.tag_code IN ({subcontas})
            )
            SELECT Entidade, SubConta, Departamento, SUM(ValorNum) AS Totalizador
            FROM Docs GROUP BY Entidade, SubConta, Departamento
        """
    },
}


def obter_consulta(tipo: str) -> dict:
    """Retorna configuração de uma consulta pelo tipo"""
    return CONSULTAS_PREDEFINIDAS.get(tipo)


def listar_consultas_disponiveis() -> list:
    """Retorna lista de consultas disponíveis"""
    return [
        {'tipo': t, 'nome': c['nome'], 'descricao': c['descricao'], 'tipo_execucao': c['tipo']}
        for t, c in CONSULTAS_PREDEFINIDAS.items()
    ]