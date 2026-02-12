using ConsultasRemotas.Api.Configuration;
using ConsultasRemotas.Api.Models;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;

namespace ConsultasRemotas.Api.Services;

public class ConsultaPreDefinidaService : IConsultaPreDefinidaService
{
    private readonly IQueryExecutorService _queryExecutor;
    private readonly SqlServerSettings _sqlSettings;

    private static readonly CultureInfo PtBr = new("pt-BR");

    private static readonly Dictionary<string, (string host, List<string> entidades)> ServerEntidades = new()
    {
        ["MMN"] = ("10.30.11.2", new() { "3011", "3013", "3021", "3093" }),
        ["USEB"] = ("10.31.11.2", new() { "3111", "3112", "3121", "3122", "3123", "3141", "3151", "3161" }),
        ["ARJ"] = ("10.32.11.2", new() { "3211", "3213", "3221", "3293", "3251" }),
        ["ARS"] = ("10.33.11.2", new() { "3311", "3313", "3321", "3393" }),
        ["AMS"] = ("10.34.11.2", new() { "3411", "3413", "3421", "3493" }),
        ["AMC"] = ("10.35.11.2", new() { "3511", "3513", "3521", "3593", "3541", "3153" }),
        ["AML"] = ("10.36.11.2", new() { "3611", "3613", "3621", "3693" }),
        ["AES"] = ("10.37.11.2", new() { "3711", "3713", "3721", "3793" }),
        ["ARF"] = ("10.38.11.2", new() { "3811", "3813", "3821", "3893" }),
        ["ASES"] = ("10.39.11.2", new() { "3911", "3913", "3921", "3993" }),
        ["MMO"] = ("10.33.211.2", new() { "33211", "33213", "33221", "33293" }),
        ["FADMINAS"] = ("10.31.24.2", new() { "3124", "3129" }),
        ["IPAE"] = ("10.32.24.2", new() { "3224" }),
        ["EDESSA"] = ("10.37.24.2", new() { "3724" }),
        ["3154"] = ("10.31.42.2", new() { "3154" })
    };

    private static readonly Dictionary<string, List<string>> SubcontasPorEntidade = new()
    {
        ["3011"] = new() { "2" }, ["3013"] = new() { "1", "2" }, ["3021"] = new() { "1", "1010" },
        ["3124"] = new() { "1", "2", "1010" }, ["3211"] = new() { "1", "2" }, ["3213"] = new() { "1", "2" },
        ["3221"] = new() { "1", "1010" }, ["3224"] = new() { "1", "2", "1010" }, ["3251"] = new() { "1" },
        ["3311"] = new() { "1", "2" }, ["3313"] = new() { "1", "2" }, ["3321"] = new() { "1", "2", "1010" },
        ["3413"] = new() { "1", "2" }, ["3421"] = new() { "1010" }, ["3511"] = new() { "1", "2" },
        ["3513"] = new() { "1", "2" }, ["3521"] = new() { "1", "1010" }, ["3541"] = new() { "1" },
        ["3611"] = new() { "1", "2" }, ["3613"] = new() { "1", "2" }, ["3621"] = new() { "1", "1010" },
        ["3711"] = new() { "1", "2" }, ["3713"] = new() { "1", "2" }, ["3721"] = new() { "1", "2", "1010" },
        ["3724"] = new() { "1", "1010" }, ["3811"] = new() { "2" }, ["3813"] = new() { "1", "2" },
        ["3821"] = new() { "1010" }, ["3911"] = new() { "1", "2" }, ["3913"] = new() { "1", "2" },
        ["3921"] = new() { "1", "1010" }, ["33211"] = new() { "2" }, ["33213"] = new() { "1" },
        ["33221"] = new() { "1", "2", "1010" }
    };

    private static readonly Dictionary<string, string> ColumnMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IDEntidade"] = "Entidade", ["entity_code"] = "Entidade", ["entidade"] = "Entidade", ["DataLote"] = "Data do Lote",
        ["Type_Document"] = "Tipo de Lote", ["TipoLote"] = "Tipo de Lote", ["NúmeroLote"] = "Número do Lote", ["usuariocriado"] = "Criado Por",
        ["QuemCriou"] = "Criado Por", ["chart_code"] = "Código Conta", ["IDConta"] = "Código Conta", ["Saldo_Legal"] = "Saldo",
        ["saldo_totalAPS"] = "Saldo APS", ["saldo_totalAASI"] = "Saldo AASI", ["Diferenca"] = "Diferença", ["Codigo"] = "Código",
        ["Descricao"] = "Descrição", ["DescricaoAdicional"] = "Descrição Adicional", ["DataBaixa"] = "Data da Baixa", ["DepreciacaoAcumulada"] = "Depreciação Acumulada",
        ["ValorLiquido"] = "Valor Líquido", ["Secao"] = "Seção", ["NF_Numero"] = "Nº NF", ["SubConta"] = "SubConta",
        ["SubContaNome"] = "Nome SubConta", ["IDDepartamento"] = "Cód. Departamento", ["NomeDepartamento"] = "Departamento", ["Totalizador"] = "Total"
    };

    public ConsultaPreDefinidaService(IQueryExecutorService queryExecutor, IOptions<SqlServerSettings> sqlSettings)
    {
        _queryExecutor = queryExecutor;
        _sqlSettings = sqlSettings.Value;
    }

    public List<ConsultaPreDefinidaInfo> ListarConsultas() => new()
    {
        new() { Tipo = "lotes_sem_anexo", Nome = "📎 Lotes Sem Anexo", Descricao = "Lista lotes sem anexo em todos os servidores", TipoExecucao = "multi_servidor" },
        new() { Tipo = "aquisicoes", Nome = "🧾 Aquisições", Descricao = "Aquisições de imobilizado no período", TipoExecucao = "single_servidor", RequerEntidade = true, RequerAno = true, RequerPeriodo = true },
        new() { Tipo = "baixas", Nome = "⬇️ Baixas", Descricao = "Baixas de imobilizado no período", TipoExecucao = "single_servidor", RequerEntidade = true, RequerAno = true, RequerPeriodo = true },
        new() { Tipo = "ficha_loja", Nome = "🏬 Ficha Loja", Descricao = "Balancete consolidado por departamento", TipoExecucao = "multi_servidor", RequerAno = true, RequerPeriodo = true },
        new() { Tipo = "conferencia_13", Nome = "🎄 Conferência 13º", Descricao = "Compara provisão entre APS e AASI", TipoExecucao = "multi_banco", RequerAno = true, RequerPeriodo = true },
        new() { Tipo = "razao_subcontas", Nome = "📒 Razão SubContas", Descricao = "Razão 1139008 por subcontas", TipoExecucao = "multi_servidor", RequerAno = true, RequerPeriodo = true },
        new() { Tipo = "saldo_anterior", Nome = "💳 Saldo Anterior", Descricao = "Saldo até X meses atrás", TipoExecucao = "multi_servidor" },
        new() { Tipo = "futuro_otimizado", Nome = "📅 Futuro Otimizado", Descricao = "Saldo até data limite", TipoExecucao = "multi_servidor" }
    };

    public async Task<ConsultaPreDefinidaResponse> ExecutarAsync(ExecutarConsultaPreDefinidaRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = request.Tipo switch
        {
            "lotes_sem_anexo" => await ExecutarMultiServidorComTemplateAsync(LotesSemAnexoSql, request, cancellationToken),
            "aquisicoes" => await ExecutarSingleServidorAsync(AquisicoesSql, request, cancellationToken),
            "baixas" => await ExecutarSingleServidorAsync(BaixasSql, request, cancellationToken),
            "ficha_loja" => await ExecutarMultiServidorComTemplateAsync(FichaLojaSql, request, cancellationToken),
            "conferencia_13" => await ExecutarConferencia13Async(request, cancellationToken),
            "razao_subcontas" => await ExecutarMultiServidorComTemplateAsync(RazaoSubcontasSql, request, cancellationToken),
            "saldo_anterior" => await ExecutarMultiServidorComTemplateAsync(SaldoAnteriorSql, request, cancellationToken),
            "futuro_otimizado" => await ExecutarMultiServidorComTemplateAsync(FuturoOtimizadoSql, request, cancellationToken),
            _ => throw new ArgumentException($"Tipo de consulta inválido: {request.Tipo}")
        };

        sw.Stop();
        response.TempoSegundos = Math.Round(sw.Elapsed.TotalSeconds, 2);
        response.Colunas = response.Dados.FirstOrDefault()?.Keys.ToList() ?? new();
        response.TotalLinhas = response.Dados.Count;
        return response;
    }

    private async Task<ConsultaPreDefinidaResponse> ExecutarSingleServidorAsync(string template, ExecutarConsultaPreDefinidaRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Entidade) || !request.Ano.HasValue || !request.Periodo.HasValue)
            throw new ArgumentException("entidade, ano e periodo são obrigatórios");

        var servidor = MapearServidorPorEntidade(request.Entidade);
        var query = ApplyParams(template, request, servidor);
        var raw = await _queryExecutor.ExecuteQueryAsync(new QueryRequest { Query = query, Servidor = servidor.Name, Banco = "AASI" }, ct);
        return FromRaw(raw);
    }

    private async Task<ConsultaPreDefinidaResponse> ExecutarMultiServidorComTemplateAsync(string template, ExecutarConsultaPreDefinidaRequest request, CancellationToken ct)
    {
        var dados = new List<Dictionary<string, object>>();
        var avisos = new List<string>();
        var requestId = Guid.NewGuid().ToString();

        foreach (var servidor in _sqlSettings.Servers)
        {
            var query = ApplyParams(template, request, servidor);
            var raw = await _queryExecutor.ExecuteQueryAsync(new QueryRequest { Query = query, Servidor = servidor.Name, Banco = "AASI" }, ct);
            dados.AddRange(raw.Results.Select(FormatarLinha));
            avisos.AddRange(raw.Errors.Select(e => $"{e.Servidor}: {e.Error}"));
            requestId = raw.RequestId;
        }

        return new ConsultaPreDefinidaResponse { RequestId = requestId, Dados = dados, Avisos = avisos };
    }

    private async Task<ConsultaPreDefinidaResponse> ExecutarConferencia13Async(ExecutarConsultaPreDefinidaRequest request, CancellationToken ct)
    {
        if (!request.Ano.HasValue || !request.Periodo.HasValue)
            throw new ArgumentException("ano e periodo são obrigatórios");

        var apsServer = _sqlSettings.Servers.FirstOrDefault(s => s.Host == "10.31.11.2") ?? _sqlSettings.Servers.First();
        var apsQuery = ApplyParams(Conferencia13ApsSql, request, apsServer);
        var apsRaw = await _queryExecutor.ExecuteQueryAsync(new QueryRequest { Query = apsQuery, Servidor = apsServer.Name, Banco = "Mineiracao_APS" }, ct);

        var aasiRaw = await ExecutarMultiServidorComTemplateAsync(Conferencia13AasiSql, request, ct);

        var apsLookup = apsRaw.Results
            .GroupBy(r => $"{r.GetValueOrDefault("Entidade")}::{r.GetValueOrDefault("Conta")}")
            .ToDictionary(g => g.Key, g => Convert.ToDecimal(g.FirstOrDefault()?.GetValueOrDefault("saldo_totalAPS") ?? 0));

        var merged = new List<Dictionary<string, object>>();
        foreach (var row in aasiRaw.Dados)
        {
            var entidade = row.GetValueOrDefault("Entidade")?.ToString() ?? "";
            var conta = row.GetValueOrDefault("Conta")?.ToString() ?? "";
            var key = $"{entidade}::{conta}";
            var saldoAasi = Convert.ToDecimal(row.GetValueOrDefault("saldo_totalAASI") ?? 0);
            var saldoAps = apsLookup.TryGetValue(key, out var v) ? v : 0;
            row["saldo_totalAPS"] = saldoAps;
            row["Diferenca"] = saldoAps - saldoAasi;
            merged.Add(FormatarLinha(row));
        }

        return new ConsultaPreDefinidaResponse
        {
            RequestId = apsRaw.RequestId,
            Dados = merged,
            Avisos = apsRaw.Errors.Select(e => $"APS {e.Error}").Concat(aasiRaw.Avisos).ToList()
        };
    }

    private ConsultaPreDefinidaResponse FromRaw(QueryResponse raw) => new()
    {
        RequestId = raw.RequestId,
        Dados = raw.Results.Select(FormatarLinha).ToList(),
        Avisos = raw.Errors.Select(e => $"{e.Servidor}: {e.Error}").ToList()
    };

    private SqlServerInfo MapearServidorPorEntidade(string entidade)
    {
        var serverCode = ServerEntidades.FirstOrDefault(x => x.Value.entidades.Contains(entidade)).Key;
        if (string.IsNullOrWhiteSpace(serverCode))
            throw new ArgumentException($"Entidade não mapeada: {entidade}");

        return _sqlSettings.Servers.FirstOrDefault(s => s.Name.Equals(serverCode, StringComparison.OrdinalIgnoreCase))
            ?? _sqlSettings.Servers.First(s => s.Host == ServerEntidades[serverCode].host);
    }

    private string ApplyParams(string template, ExecutarConsultaPreDefinidaRequest request, SqlServerInfo servidor)
    {
        var entidades = ServerEntidades.TryGetValue(servidor.Name, out var map)
            ? map.entidades
            : ServerEntidades.FirstOrDefault(s => s.Value.host == servidor.Host).Value.entidades ?? new();

        var entidadesSet = BuildEntidadesSet(entidades);
        var subcontas = BuildSubcontas(entidades);

        return template
            .Replace("{ano}", (request.Ano ?? DateTime.Now.Year).ToString())
            .Replace("{periodo}", (request.Periodo ?? DateTime.Now.Month).ToString())
            .Replace("{entidade}", request.Entidade ?? "")
            .Replace("{entidades}", entidadesSet)
            .Replace("{entidades_set}", entidadesSet)
            .Replace("{subcontas}", subcontas)
            .Replace("{meses_atras}", (request.MesesAtras ?? 1).ToString())
            .Replace("{data_limite}", request.DataLimite ?? DateTime.Today.ToString("yyyy-MM-dd"));
    }

    private static string BuildEntidadesSet(List<string> entidades)
    {
        var padded = entidades.Concat(Enumerable.Repeat("NULL", Math.Max(0, 8 - entidades.Count))).Take(8).ToList();
        return string.Join("\n", padded.Select((e, i) => e == "NULL" ? $"SET @Entidade{i + 1} = NULL" : $"SET @Entidade{i + 1} = '{e}'"));
    }

    private static string BuildSubcontas(List<string> entidades)
    {
        var subs = entidades
            .Where(SubcontasPorEntidade.ContainsKey)
            .SelectMany(e => SubcontasPorEntidade[e])
            .Distinct()
            .ToList();

        if (!subs.Any()) return "'1','2','1010'";
        return string.Join(",", subs.Select(s => $"'{s}'"));
    }

    private static Dictionary<string, object> FormatarLinha(Dictionary<string, object> row)
    {
        var formatted = new Dictionary<string, object>();
        foreach (var (k, v) in row)
        {
            var key = ColumnMap.TryGetValue(k, out var mapped) ? mapped : k;
            formatted[key] = FormatValue(v);
        }
        return formatted;
    }

    private static object FormatValue(object value)
    {
        if (value is DateTime dt) return dt.ToString("dd/MM/yyyy", PtBr);
        if (value is decimal dec) return dec.ToString("N2", PtBr);
        if (value is double d) return d.ToString("N2", PtBr);
        if (value is float f) return f.ToString("N2", PtBr);
        if (value is string s && DateTime.TryParse(s, out var parsedDt)) return parsedDt.ToString("dd/MM/yyyy", PtBr);
        if (value is string sn && decimal.TryParse(sn, out var parsedDec)) return parsedDec.ToString("N2", PtBr);
        return value;
    }

    private const string LotesSemAnexoSql = @"SELECT l.entidade as Entidade, l.Type_Document as TipoLote, l.code as NúmeroLote, l.char_0 as NomeLote, l.DataLote, l.year as Ano, l.period as Período, l.usuariocriado as QuemCriou FROM Vw_Lotes AS l WHERE l.Attachments = 0 AND l.year >= 2025 AND l.cache = 0 AND l.Type_Document NOT LIKE 'PG%' AND l.Type_Document NOT LIKE 'MT%' AND l.Type_Document NOT LIKE 'MR%' AND l.Type_Document NOT LIKE 'ED%'";
    private const string AquisicoesSql = @"DECLARE @ano INT = {ano}, @mes INT = {periodo}, @ent VARCHAR(10) = '{entidade}' SELECT f.code AS Codigo, f.name AS Descricao, f.fa_char_1 AS DescricaoAdicional, f.date_in AS Data, f.section AS Secao, f.fa_char_0 AS NF_Numero, ISNULL(SUM(oi.value), 0) AS Valor FROM v_fixed_asset_iud f INNER JOIN v_entity e ON e.id_entity = f.id_entity LEFT JOIN ( SELECT oi.id_tag, oi.value, p.year, p.period FROM year_document_item oi INNER JOIN year_document od ON od.id_document = oi.id_document INNER JOIN Period p ON p.id_period = od.id_period WHERE oi.value > 0 ) oi ON oi.id_tag = f.id_tag AND oi.year = @ano AND oi.period = @mes WHERE e.entity_code = @ent AND f.date_in IS NOT NULL AND YEAR(f.date_in) = @ano AND MONTH(f.date_in) = @mes AND f.date_out IS NULL GROUP BY f.code, f.name, f.fa_char_1, f.date_in, f.section, f.fa_char_0 HAVING SUM(oi.value) > 0 ORDER BY f.date_in, f.code";
    private const string BaixasSql = @"DECLARE @ano INT = {ano}, @mes INT = {periodo}, @ent VARCHAR(10) = '{entidade}' SELECT vf.code AS Codigo, vf.name AS Descricao, vf.char_1 AS DescricaoAdicional, vf.date_out AS DataBaixa, ABS(vf.value_in) AS Valor, ABS(vf.value_depreciation) AS DepreciacaoAcumulada, ABS(vf.value_in) - ABS(vf.value_depreciation) AS ValorLiquido, vf.char_2 AS Motivo FROM v_fixed_asset vf INNER JOIN v_entity e ON e.id_entity = vf.id_entity WHERE e.entity_code = @ent AND vf.date_out IS NOT NULL AND YEAR(vf.date_out) = @ano AND MONTH(vf.date_out) = @mes ORDER BY vf.date_out DESC, vf.code";
    private const string FichaLojaSql = @"DECLARE @Entidade1 varchar(10), @Entidade2 varchar(10), @Entidade3 varchar(10) DECLARE @Entidade4 varchar(10), @Entidade5 varchar(10), @Entidade6 varchar(10) DECLARE @Entidade7 varchar(10), @Entidade8 varchar(10) {entidades} SELECT v_entity.entity_code AS IDEntidade, v_year_balance.year AS Ano, v_year_balance.period AS Mes, v_year_balance.chart_code AS IDConta, v_year_balance.chart_name AS Conta, v_year_balance.tag_code AS SubConta, v_year_balance.tag_name AS SubContaNome, ROUND(ISNULL(v_year_balance.cr_value_0,0) + ISNULL(v_year_balance.db_value_0,0),2) AS Saldo_Legal, v_year_balance.department_code AS IDDepartamento, v_department.department_name AS NomeDepartamento FROM v_department INNER JOIN (Chart INNER JOIN (v_entity INNER JOIN v_year_balance ON v_entity.id_entity = v_year_balance.id_entity) ON Chart.id_chart = v_year_balance.id_chart) ON (v_department.id_entity = v_year_balance.id_entity) AND (v_department.id_department = v_year_balance.id_department) WHERE v_department.only_accrual='0' AND Chart.only_accrual='0' AND v_year_balance.year = {ano} AND v_year_balance.period BETWEEN '0' AND '{periodo}'";
    private const string Conferencia13ApsSql = @"SELECT p.idEntidade AS Entidade, CASE WHEN p.codVerba IN ('92000','93000') THEN '2141001' WHEN p.codVerba IN ('98600','98960') THEN '2141002' END AS Conta, SUM(p.value) AS saldo_totalAPS FROM pagamentos AS p WHERE p.ano >= {ano} AND p.mes <= {periodo} AND p.codVerba IN ('92000','93000','98600','98960') GROUP BY p.idEntidade, CASE WHEN p.codVerba IN ('92000','93000') THEN '2141001' WHEN p.codVerba IN ('98600','98960') THEN '2141002' END";
    private const string Conferencia13AasiSql = @"DECLARE @Entidade1 varchar(10), @Entidade2 varchar(10), @Entidade3 varchar(10) DECLARE @Entidade4 varchar(10), @Entidade5 varchar(10), @Entidade6 varchar(10) DECLARE @Entidade7 varchar(10), @Entidade8 varchar(10) {entidades} SELECT v_entity.entity_code AS Entidade, v_year_balance.chart_code AS Conta, SUM(ISNULL(v_year_balance.cr_value_0,0) + ISNULL(v_year_balance.db_value_0,0)) AS saldo_totalAASI FROM v_department INNER JOIN (Chart INNER JOIN (v_entity INNER JOIN v_year_balance ON v_entity.id_entity = v_year_balance.id_entity) ON Chart.id_chart = v_year_balance.id_chart) ON (v_department.id_entity = v_year_balance.id_entity) AND (v_department.id_department = v_year_balance.id_department) WHERE v_department.only_accrual='0' AND Chart.only_accrual='0' AND v_year_balance.year = {ano} AND v_year_balance.chart_code IN ('2141001','2141002') AND v_year_balance.department_code <> '0' AND v_year_balance.period <= {periodo} GROUP BY v_entity.entity_code, v_year_balance.year, v_year_balance.chart_code";
    private const string RazaoSubcontasSql = @"DECLARE @Entidade1 varchar(10), @Entidade2 varchar(10), @Entidade3 varchar(10) DECLARE @Entidade4 varchar(10), @Entidade5 varchar(10), @Entidade6 varchar(10) DECLARE @Entidade7 varchar(10), @Entidade8 varchar(10) {entidades_set} SELECT e.entity_code AS Entidade, p.year AS Ano, p.period AS Periodo, d.fund_code AS Fundo, d.department_code AS Departamento, d.chart_code AS Conta, d.tag_code AS SubConta, d.date AS Data, d.reference AS Lote, d.char_0 AS Descricao, d.value AS Valor, c.code AS CodigoConta, c.name AS NomeConta FROM v_open_document AS d INNER JOIN v_entity AS e ON e.id_entity = d.id_entity INNER JOIN Period AS p ON p.id_period = d.id_period INNER JOIN chart_of_accumulator AS coa ON coa.id_chart = d.id_chart INNER JOIN Chart AS c ON c.id_type_chart = coa.id_type_chart AND c.id_chart = coa.parent_id_chart WHERE p.year = {ano} AND p.period = {periodo} AND d.chart_code = '1139008' AND d.type_document_code NOT IN ('EA','AJ','MT') AND c.code = '1139008' AND e.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8) AND d.tag_code IN ({subcontas})";
    private const string SaldoAnteriorSql = @"DECLARE @Entidade1 varchar(10), @Entidade2 varchar(10), @Entidade3 varchar(10) DECLARE @Entidade4 varchar(10), @Entidade5 varchar(10), @Entidade6 varchar(10) DECLARE @Entidade7 varchar(10), @Entidade8 varchar(10) {entidades_set} DECLARE @DataLimite DATE = EOMONTH(GETDATE(), -{meses_atras}) SELECT e.entity_code AS Entidade, d.tag_code AS SubConta, d.department_code AS Departamento, SUM(d.value) AS Totalizador FROM v_open_document AS d INNER JOIN v_entity AS e ON e.id_entity = d.id_entity WHERE d.date <= @DataLimite AND d.chart_code = '1139008' AND d.type_document_code NOT IN ('EA','AJ','MT') AND e.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8) AND d.tag_code IN ({subcontas}) GROUP BY e.entity_code, d.tag_code, d.department_code";
    private const string FuturoOtimizadoSql = @"DECLARE @DataLimite DATE = CAST('{data_limite}' AS DATE) DECLARE @Entidade1 varchar(10), @Entidade2 varchar(10), @Entidade3 varchar(10) DECLARE @Entidade4 varchar(10), @Entidade5 varchar(10), @Entidade6 varchar(10) DECLARE @Entidade7 varchar(10), @Entidade8 varchar(10) {entidades_set} SELECT e.entity_code AS Entidade, d.tag_code AS SubConta, d.department_code AS Departamento, SUM(d.value) AS Totalizador FROM v_open_document AS d INNER JOIN v_entity AS e ON e.id_entity = d.id_entity WHERE d.date <= @DataLimite AND d.chart_code = '1139008' AND d.type_document_code NOT IN ('EA','AJ','MT') AND e.entity_code IN (@Entidade1,@Entidade2,@Entidade3,@Entidade4,@Entidade5,@Entidade6,@Entidade7,@Entidade8) AND d.tag_code IN ({subcontas}) GROUP BY e.entity_code, d.tag_code, d.department_code";
}
