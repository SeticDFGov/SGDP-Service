using System.Text.Json;
using api.Contratacoes;
using Models.Contratacoes;
using Models.Pgia;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Regras puras dos riscos da contratação (<see cref="CtrClassificacaoRisco"/>): as 25
/// células da matriz da CGDF, o nível máximo, a validação do envio (escala completa,
/// riscos incompletos, limite, lista vazia), o reenvio idêntico e a leitura. Desde
/// 2026-09-21 não há mais o questionário dos arts. 15 a 17 do PGIA no módulo.
/// </summary>
public class CtrClassificacaoRiscoTest
{
    private const string EmailResponsavel = "gestor.contrato@seec.df.gov.br";

    private static CtrRiscoDeclaradoDTO Risco(string probabilidade = "Raro", string consequencia = "Menor") => new()
    {
        DescricaoRisco = "Atraso na entrega dos equipamentos",
        AcaoMitigacao = "Cronograma com marcos e cláusula de multa",
        ResponsavelNome = "Gestor do contrato",
        ResponsavelEmail = EmailResponsavel,
        Probabilidade = probabilidade,
        Consequencia = consequencia
    };

    private static CtrClassificacaoRiscoDTO Classificacao(params CtrRiscoDeclaradoDTO[] riscos) => new()
    {
        RiscosDeclarados = riscos.ToList()
    };

    private static void AssertRecusa(ErrorCode codigo, Action acao, string? trecho = null)
    {
        var ex = Assert.Throws<ApiException>(acao);
        Assert.Equal((int)codigo, ex.Error.Code);
        if (trecho != null) Assert.Contains(trecho, ex.Error.Message);
    }

    // ══ Matriz da CGDF ════════════════════════════════════════════════════════

    // Tabela do contrato (desenho_supervisao_riscos.md, §3.4), célula a célula — é a
    // mesma disposição de MATRIZ_RISCO_CGDF no front
    [Theory]
    [InlineData("Improvável", "Catastrófica", "Médio")]
    [InlineData("Raro", "Catastrófica", "Alto")]
    [InlineData("Possível", "Catastrófica", "Extremo")]
    [InlineData("Provável", "Catastrófica", "Extremo")]
    [InlineData("Quase certo", "Catastrófica", "Extremo")]
    [InlineData("Improvável", "Maior", "Médio")]
    [InlineData("Raro", "Maior", "Médio")]
    [InlineData("Possível", "Maior", "Alto")]
    [InlineData("Provável", "Maior", "Extremo")]
    [InlineData("Quase certo", "Maior", "Extremo")]
    [InlineData("Improvável", "Moderada", "Baixo")]
    [InlineData("Raro", "Moderada", "Médio")]
    [InlineData("Possível", "Moderada", "Médio")]
    [InlineData("Provável", "Moderada", "Alto")]
    [InlineData("Quase certo", "Moderada", "Extremo")]
    [InlineData("Improvável", "Menor", "Baixo")]
    [InlineData("Raro", "Menor", "Baixo")]
    [InlineData("Possível", "Menor", "Médio")]
    [InlineData("Provável", "Menor", "Alto")]
    [InlineData("Quase certo", "Menor", "Alto")]
    [InlineData("Improvável", "Desprezível", "Baixo")]
    [InlineData("Raro", "Desprezível", "Baixo")]
    [InlineData("Possível", "Desprezível", "Baixo")]
    [InlineData("Provável", "Desprezível", "Médio")]
    [InlineData("Quase certo", "Desprezível", "Alto")]
    public void CalcularNivel_As25CelulasDaMatrizDaCgdf(string probabilidade, string consequencia, string nivel)
    {
        Assert.Equal(nivel, CtrClassificacaoRisco.CalcularNivel(probabilidade, consequencia));
    }

    [Fact]
    public void Matriz_As25CelulasSaoAtivasEOsParesConcordamComOCalculo()
    {
        var todos = CtrDominios.NivelRisco.Todos
            .SelectMany(nivel => CtrClassificacaoRisco.ParesDoNivel(nivel).Select(par => (nivel, par)))
            .ToList();

        // Nenhuma célula fora (nem esmaecida): 5 × 5, sem repetição
        Assert.Equal(25, todos.Count);
        Assert.Equal(25, todos.Select(x => x.par).Distinct().Count());

        Assert.Equal(6, CtrClassificacaoRisco.ParesDoNivel(CtrDominios.NivelRisco.Baixo).Count);
        Assert.Equal(7, CtrClassificacaoRisco.ParesDoNivel(CtrDominios.NivelRisco.Medio).Count);
        Assert.Equal(6, CtrClassificacaoRisco.ParesDoNivel(CtrDominios.NivelRisco.Alto).Count);
        Assert.Equal(6, CtrClassificacaoRisco.ParesDoNivel(CtrDominios.NivelRisco.Extremo).Count);

        // Os pares de cada nível (base do filtro EF) e o cálculo são a mesma fonte
        foreach (var (nivel, par) in todos)
            Assert.Equal(nivel, CtrClassificacaoRisco.CalcularNivel(par.Probabilidade, par.Consequencia));
    }

    [Fact]
    public void CalcularNivel_ForaDaEscala_DevolveNulo()
    {
        Assert.Null(CtrClassificacaoRisco.CalcularNivel("Talvez", "Menor"));
        Assert.Null(CtrClassificacaoRisco.CalcularNivel("Raro", "Grave"));
        Assert.Null(CtrClassificacaoRisco.CalcularNivel(null, null));
        Assert.Empty(CtrClassificacaoRisco.ParesDoNivel(CtrDominios.NivelRisco.SemRiscosDeclarados));
    }

    [Fact]
    public void NivelRisco_EstaEmOrdemCrescente()
    {
        Assert.Equal(new[] { "Baixo", "Médio", "Alto", "Extremo" }, CtrDominios.NivelRisco.Todos);
    }

    [Fact]
    public void CalcularNivelMaximo_EscolheOMaiorENuloSemRiscos()
    {
        Assert.Null(CtrClassificacaoRisco.CalcularNivelMaximo(Array.Empty<(string, string)>()));

        Assert.Equal("Extremo", CtrClassificacaoRisco.CalcularNivelMaximo(new[]
        {
            ("Improvável", "Desprezível"), ("Quase certo", "Catastrófica"), ("Raro", "Maior")
        }));
        Assert.Equal("Médio", CtrClassificacaoRisco.CalcularNivelMaximo(new[]
        {
            ("Raro", "Maior"), ("Improvável", "Menor")
        }));
        Assert.Equal("Baixo", CtrClassificacaoRisco.CalcularNivelMaximo(new[] { ("Improvável", "Desprezível") }));

        // Valor fora da escala não conta (o banco nem o aceita)
        Assert.Equal("Alto", CtrClassificacaoRisco.CalcularNivelMaximo(new[]
        {
            ("Talvez", "Menor"), ("Provável", "Menor")
        }));
    }

    // ══ Validação do envio: só a lista de riscos da contratação ═══════════════

    [Fact]
    public void Avaliar_ListaVaziaOuNula_EhValidaESemRiscos()
    {
        // O processo pode ficar sem riscos: é assim que a tela tira o último
        Assert.Empty(CtrClassificacaoRisco.Avaliar(Classificacao()));
        Assert.Empty(CtrClassificacaoRisco.Avaliar(new CtrClassificacaoRiscoDTO { RiscosDeclarados = null! }));
    }

    [Fact]
    public void Dto_NaoTemMaisOQuestionarioDoPgia()
    {
        // Só a lista: nada dos grupos dos arts. 15 a 17 (Q15, Q16Nenhuma...)
        Assert.Equal(new[] { nameof(CtrClassificacaoRiscoDTO.RiscosDeclarados) },
            typeof(CtrClassificacaoRiscoDTO).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain(typeof(CtrClassificacaoRiscoResponse).GetProperties(),
            p => p.Name.StartsWith("Q1") || p.Name is "RiscoClassificado" or "EnquadramentoRisco" or "PontuacaoRisco");
        Assert.DoesNotContain(typeof(CtrProcessoResponse).GetProperties(), p => p.Name == "RiscoClassificado");
        Assert.DoesNotContain(typeof(CtrProcessoFiltro).GetProperties(), p => p.Name == "RiscoClassificado");
    }

    [Fact]
    public void Dto_CorpoDeClienteAntigoComOQuestionario_EhLidoSoPelosRiscos()
    {
        // Um front antigo ainda manda os grupos: o desserializador ignora o que não existe
        const string json = """
            {"Q15":["I"],"Q16":[],"Q17":[],"Q15Nenhuma":false,"Q16Nenhuma":true,"Q17Nenhuma":true,
             "RiscosDeclarados":[{"DescricaoRisco":"Atraso","AcaoMitigacao":"Multa","ResponsavelNome":"Gestor",
             "ResponsavelEmail":"gestor@seec.df.gov.br","Probabilidade":"Raro","Consequencia":"Menor"}]}
            """;

        var dto = JsonSerializer.Deserialize<CtrClassificacaoRiscoDTO>(json)!;

        var risco = Assert.Single(CtrClassificacaoRisco.Avaliar(dto));
        Assert.Equal("Atraso", risco.DescricaoRisco);
    }

    [Fact]
    public void Avaliar_EscalaCompletaDaCgdf_EhAceitaInclusiveQuaseCertoXCatastrofica()
    {
        // O PGIA recusa esse par no grupo "Outros" (lá só vale o quadrante baixo)
        Assert.DoesNotContain("Quase certo", PgiaDominios.EscalaCgdf.Probabilidade.Permitidos);
        Assert.DoesNotContain("Catastrófica", PgiaDominios.EscalaCgdf.Consequencia.Permitidos);

        foreach (var probabilidade in PgiaDominios.EscalaCgdf.Probabilidade.Todos)
        foreach (var consequencia in PgiaDominios.EscalaCgdf.Consequencia.Todos)
        {
            var risco = Assert.Single(CtrClassificacaoRisco.Avaliar(Classificacao(Risco(probabilidade, consequencia))));
            Assert.Equal(probabilidade, risco.Probabilidade);
            Assert.Equal(consequencia, risco.Consequencia);
        }

        Assert.Equal(CtrDominios.NivelRisco.Extremo, CtrClassificacaoRisco.CalcularNivel("Quase certo", "Catastrófica"));
    }

    [Theory]
    [InlineData("descricao", "a descrição do risco e a ação de mitigação são obrigatórias")]
    [InlineData("acao", "a descrição do risco e a ação de mitigação são obrigatórias")]
    [InlineData("nome", "nome do responsável")]
    [InlineData("email-sem-dominio", "E-mail do responsável pelo risco declarado inválido")]
    [InlineData("email-com-apelido", "E-mail do responsável pelo risco declarado inválido")]
    [InlineData("probabilidade-vazia", "Informe a probabilidade e a consequência")]
    [InlineData("consequencia-vazia", "Informe a probabilidade e a consequência")]
    public void Avaliar_RiscoIncompleto_EhRecusado(string caso, string trecho)
    {
        var risco = Risco();
        switch (caso)
        {
            case "descricao": risco.DescricaoRisco = "   "; break;
            case "acao": risco.AcaoMitigacao = ""; break;
            case "nome": risco.ResponsavelNome = " "; break;
            case "email-sem-dominio": risco.ResponsavelEmail = "gestor@seec"; break;
            case "email-com-apelido": risco.ResponsavelEmail = "Gestor <gestor@seec.df.gov.br>"; break;
            case "probabilidade-vazia": risco.Probabilidade = ""; break;
            case "consequencia-vazia": risco.Consequencia = "  "; break;
        }

        // Um risco ruim no meio de riscos bons recusa o envio inteiro
        AssertRecusa(ErrorCode.CtrClassificacaoRiscoInvalida,
            () => CtrClassificacaoRisco.Avaliar(Classificacao(Risco(), risco, Risco())), trecho);
    }

    [Fact]
    public void Avaliar_ProbabilidadeOuConsequenciaForaDaEscala_EhDominioInvalido()
    {
        AssertRecusa(ErrorCode.CtrDominioInvalido,
            () => CtrClassificacaoRisco.Avaliar(Classificacao(Risco("Frequente", "Menor"))), "Probabilidade inválida");
        AssertRecusa(ErrorCode.CtrDominioInvalido,
            () => CtrClassificacaoRisco.Avaliar(Classificacao(Risco("Raro", "Grave"))), "Consequência inválida");
    }

    [Fact]
    public void Avaliar_MaisDe20Riscos_EhRecusado()
    {
        var vinte = Enumerable.Range(0, 20).Select(_ => Risco()).ToArray();
        Assert.Equal(20, CtrClassificacaoRisco.Avaliar(Classificacao(vinte)).Count);

        var vinteEUm = Enumerable.Range(0, 21).Select(_ => Risco()).ToArray();
        AssertRecusa(ErrorCode.CtrClassificacaoRiscoInvalida,
            () => CtrClassificacaoRisco.Avaliar(Classificacao(vinteEUm)), "no máximo 20");
    }

    [Fact]
    public void Avaliar_NormalizaOsTextosDoRisco()
    {
        var risco = Risco();
        risco.DescricaoRisco = "  Atraso  ";
        risco.AcaoMitigacao = "  Multa ";
        risco.ResponsavelNome = " Gestor ";
        // A caixa do e-mail não é informação: sai sempre em minúsculas
        risco.ResponsavelEmail = "  Gestor.Contrato@SEEC.DF.gov.br ";
        risco.Probabilidade = " Provável ";
        risco.Consequencia = "Maior ";

        var avaliado = Assert.Single(CtrClassificacaoRisco.Avaliar(Classificacao(risco)));

        Assert.Equal(new CtrClassificacaoRisco.RiscoAvaliado("Atraso", "Multa", "Gestor", EmailResponsavel,
            "Provável", "Maior"), avaliado);
    }

    // ══ Reenvio idêntico ══════════════════════════════════════════════════════

    private static CtrRiscoDeclarado Gravado(string descricao, string email = EmailResponsavel,
        string probabilidade = "Raro", string consequencia = "Menor") => new()
    {
        DescricaoRisco = descricao,
        AcaoMitigacao = "Cronograma com marcos e cláusula de multa",
        ResponsavelNome = "Gestor do contrato",
        ResponsavelEmail = email,
        Probabilidade = probabilidade,
        Consequencia = consequencia
    };

    [Fact]
    public void MesmosRiscos_ComparaComoMulticonjuntoNormalizado()
    {
        var gravados = new[] { Gravado("Risco A"), Gravado("Risco B", probabilidade: "Quase certo") };

        // Outra ordem, espaços e outra caixa no e-mail: é o mesmo conteúdo
        var b = Risco("Quase certo");
        b.DescricaoRisco = "  Risco B ";
        var a = Risco();
        a.DescricaoRisco = "Risco A";
        a.ResponsavelEmail = " GESTOR.Contrato@seec.df.gov.br";
        Assert.True(CtrClassificacaoRisco.MesmosRiscos(gravados, CtrClassificacaoRisco.Avaliar(Classificacao(b, a))));

        // Multiplicidade conta: dois iguais gravados não são um só enviado
        var repetidos = new[] { Gravado("Risco A"), Gravado("Risco A") };
        Assert.False(CtrClassificacaoRisco.MesmosRiscos(repetidos, CtrClassificacaoRisco.Avaliar(Classificacao(a))));

        // Uma consequência diferente já é alteração
        var outra = Risco("Raro", "Maior");
        outra.DescricaoRisco = "Risco A";
        Assert.False(CtrClassificacaoRisco.MesmosRiscos(new[] { Gravado("Risco A") },
            CtrClassificacaoRisco.Avaliar(Classificacao(outra))));

        // Nada gravado e nada enviado: nada muda
        Assert.True(CtrClassificacaoRisco.MesmosRiscos(Array.Empty<CtrRiscoDeclarado>(),
            Array.Empty<CtrClassificacaoRisco.RiscoAvaliado>()));
    }

    // ══ Leitura ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MapearClassificacao_SemRiscos_EhNula()
    {
        Assert.Null(CtrClassificacaoRisco.MapearClassificacao(Array.Empty<CtrRiscoDeclarado>()));
    }

    [Fact]
    public void MapearClassificacao_QuemEQuandoSaemDoRiscoMaisRecente()
    {
        var antigo = Gravado("Antigo");
        antigo.Id = 7;
        antigo.CriadoEm = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        antigo.CriadoPor = "primeira@local.teste";
        var novo = Gravado("Novo", probabilidade: "Provável", consequencia: "Maior");
        novo.Id = 3;
        novo.CriadoEm = new DateTime(2026, 9, 20, 15, 30, 0, DateTimeKind.Utc);
        novo.CriadoPor = "segunda@local.teste";

        var lida = CtrClassificacaoRisco.MapearClassificacao(new[] { antigo, novo })!;

        Assert.Equal(novo.CriadoEm, lida.ClassificadoEm);
        Assert.Equal("segunda@local.teste", lida.ClassificadoPor);
        // Na ordem de Id, com o nível derivado da célula
        Assert.Equal(new[] { "Novo", "Antigo" }, lida.RiscosDeclarados.Select(r => r.DescricaoRisco));
        Assert.Equal(new[] { "Extremo", "Baixo" }, lida.RiscosDeclarados.Select(r => r.Nivel));
    }
}
