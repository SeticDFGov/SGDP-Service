using System.Reflection;
using System.Runtime.ExceptionServices;
using api.Contratacoes;
using api.Pgia;
using Models.Contratacoes;
using Models.Pgia;
using service;
using service.Contratacoes;
using service.Pgia;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Regras puras da classificação de riscos do processo (<see cref="CtrClassificacaoRisco"/>):
/// as 25 células da matriz da CGDF, o nível máximo, a validação do envio (completude
/// XOR, "tudo nenhuma", escala completa, riscos incompletos, limite) e a EQUIVALÊNCIA
/// com a regra do PGIA — que é privada do PgiaSistemaService e por isso é lida por
/// reflexão, sem alterar nenhum arquivo do PGIA.
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

    /// <summary>Grupo nulo = "Nenhuma das alternativas acima"; senão, os incisos marcados.</summary>
    private static CtrClassificacaoRiscoDTO Classificacao(string[]? q15, string[]? q16, string[]? q17,
        params CtrRiscoDeclaradoDTO[] riscos) => new()
    {
        Q15 = q15?.ToList() ?? new List<string>(),
        Q16 = q16?.ToList() ?? new List<string>(),
        Q17 = q17?.ToList() ?? new List<string>(),
        Q15Nenhuma = q15 == null,
        Q16Nenhuma = q16 == null,
        Q17Nenhuma = q17 == null,
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

    // ══ Resultado, enquadramento, pontuação e jsonb ═══════════════════════════

    [Fact]
    public void Avaliar_Art15VenceOsDemaisEEnquadraNoPrimeiroIncisoDoArtigo()
    {
        var avaliada = CtrClassificacaoRisco.Avaliar(Classificacao(new[] { "III", "I" }, new[] { "II" }, new[] { "IV" }));

        Assert.Equal(PgiaDominios.ResultadoRisco.Excessivo, avaliada.Resultado);
        Assert.Equal("art. 15, I", avaliada.Enquadramento);
        Assert.Equal(5 + 5 + 3 + 2, avaliada.Pontuacao);
        Assert.Equal(new[] { "I", "III" }, avaliada.Q15);
    }

    [Fact]
    public void Avaliar_Art16SemArt15_EhAltoRiscoSemRepetirPontos()
    {
        var avaliada = CtrClassificacaoRisco.Avaliar(Classificacao(null, new[] { "IX", "II", "II" }, null));

        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, avaliada.Resultado);
        Assert.Equal("art. 16, II", avaliada.Enquadramento);
        Assert.Equal(3 + 4, avaliada.Pontuacao);
        Assert.Equal(new[] { "II", "IX" }, avaliada.Q16);
        Assert.Empty(avaliada.RiscosDeclarados);
    }

    [Fact]
    public void Avaliar_SoArt17_EhRiscoModerado()
    {
        var avaliada = CtrClassificacaoRisco.Avaliar(Classificacao(null, null, new[] { "III", "II" }));

        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, avaliada.Resultado);
        Assert.Equal("art. 17, II", avaliada.Enquadramento);
        Assert.Equal(1 + 1, avaliada.Pontuacao);
    }

    [Fact]
    public void Avaliar_TudoNenhumaComRiscoDeclarado_EhBaixoRiscoSemEnquadramentoNemPontos()
    {
        var avaliada = CtrClassificacaoRisco.Avaliar(Classificacao(null, null, null,
            Risco("Quase certo", "Catastrófica")));

        Assert.Equal(PgiaDominios.ResultadoRisco.Baixo, avaliada.Resultado);
        Assert.Null(avaliada.Enquadramento);
        // Os "nenhuma" e os riscos declarados não pontuam
        Assert.Equal(0, avaliada.Pontuacao);
        // Mesmo formato do jsonb de pgia_classificacao_risco.respostas_checklist
        Assert.Equal(
            "{\"q15\":[],\"q16\":[],\"q17\":[],\"q15_nenhuma\":true,\"q16_nenhuma\":true,\"q17_nenhuma\":true}",
            avaliada.ChecklistJson);
    }

    // ══ Validação: completude XOR e "tudo nenhuma" ════════════════════════════

    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    public void Avaliar_GrupoComIncisoENenhumaJuntos_EhRecusado(int artigo)
    {
        var dto = Classificacao(new[] { "I" }, new[] { "I" }, new[] { "I" });
        if (artigo == 15) dto.Q15Nenhuma = true;
        if (artigo == 16) dto.Q16Nenhuma = true;
        if (artigo == 17) dto.Q17Nenhuma = true;

        AssertRecusa(ErrorCode.CtrClassificacaoRiscoInvalida, () => CtrClassificacaoRisco.Avaliar(dto),
            $"No grupo do art. {artigo}, marque os incisos aplicáveis ou \"Nenhuma das alternativas acima\", não os dois.");
    }

    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    public void Avaliar_GrupoSemResposta_EhRecusado(int artigo)
    {
        var dto = Classificacao(null, null, null, Risco());
        if (artigo == 15) dto.Q15Nenhuma = false;
        if (artigo == 16) dto.Q16Nenhuma = false;
        if (artigo == 17) dto.Q17Nenhuma = false;

        AssertRecusa(ErrorCode.CtrClassificacaoRiscoInvalida, () => CtrClassificacaoRisco.Avaliar(dto),
            $"Responda o grupo do art. {artigo}");
    }

    [Fact]
    public void Avaliar_IncisoForaDoArtigo_EhDominioInvalido()
    {
        // O art. 17 só tem os incisos I a IV
        AssertRecusa(ErrorCode.CtrDominioInvalido,
            () => CtrClassificacaoRisco.Avaliar(Classificacao(null, null, new[] { "V" })), "art. 17: V");
    }

    [Fact]
    public void Avaliar_TudoNenhumaSemRiscoDeclarado_EhRecusadoComAMensagemDoContrato()
    {
        var ex = Assert.Throws<ApiException>(() => CtrClassificacaoRisco.Avaliar(Classificacao(null, null, null)));

        Assert.Equal((int)ErrorCode.CtrClassificacaoRiscoInvalida, ex.Error.Code);
        Assert.Equal("Como nenhuma das situações dos três grupos se aplica, declare ao menos um risco da contratação.",
            ex.Error.Message);
    }

    [Fact]
    public void Avaliar_ListasNulasNoCorpo_ContamComoGrupoSemIncisos()
    {
        var dto = new CtrClassificacaoRiscoDTO
        {
            Q15 = null!, Q16 = null!, Q17 = null!,
            Q15Nenhuma = true, Q16Nenhuma = true, Q17Nenhuma = true,
            RiscosDeclarados = new List<CtrRiscoDeclaradoDTO> { Risco() }
        };

        Assert.Equal(PgiaDominios.ResultadoRisco.Baixo, CtrClassificacaoRisco.Avaliar(dto).Resultado);

        dto.RiscosDeclarados = null!;
        AssertRecusa(ErrorCode.CtrClassificacaoRiscoInvalida, () => CtrClassificacaoRisco.Avaliar(dto),
            "declare ao menos um risco");
    }

    // ══ Validação: riscos declarados ══════════════════════════════════════════

    [Fact]
    public void Avaliar_EscalaCompletaDaCgdf_EhAceitaInclusiveQuaseCertoXCatastrofica()
    {
        // O PGIA recusa esse par no grupo "Outros" (lá só vale o quadrante baixo)
        Assert.DoesNotContain("Quase certo", PgiaDominios.EscalaCgdf.Probabilidade.Permitidos);
        Assert.DoesNotContain("Catastrófica", PgiaDominios.EscalaCgdf.Consequencia.Permitidos);

        foreach (var probabilidade in PgiaDominios.EscalaCgdf.Probabilidade.Todos)
        foreach (var consequencia in PgiaDominios.EscalaCgdf.Consequencia.Todos)
        {
            var avaliada = CtrClassificacaoRisco.Avaliar(Classificacao(null, null, null,
                Risco(probabilidade, consequencia)));
            var risco = Assert.Single(avaliada.RiscosDeclarados);
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
    public void Avaliar_RiscoDeclaradoIncompleto_EhRecusado(string caso, string trecho)
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

        AssertRecusa(ErrorCode.CtrClassificacaoRiscoInvalida,
            () => CtrClassificacaoRisco.Avaliar(Classificacao(null, new[] { "I" }, null, risco)), trecho);
    }

    [Fact]
    public void Avaliar_ProbabilidadeOuConsequenciaForaDaEscala_EhDominioInvalido()
    {
        AssertRecusa(ErrorCode.CtrDominioInvalido,
            () => CtrClassificacaoRisco.Avaliar(Classificacao(null, null, null, Risco("Frequente", "Menor"))),
            "Probabilidade inválida");
        AssertRecusa(ErrorCode.CtrDominioInvalido,
            () => CtrClassificacaoRisco.Avaliar(Classificacao(null, null, null, Risco("Raro", "Grave"))),
            "Consequência inválida");
    }

    [Fact]
    public void Avaliar_MaisDe20Riscos_EhRecusado()
    {
        var vinte = Enumerable.Range(0, 20).Select(_ => Risco()).ToArray();
        Assert.Equal(20, CtrClassificacaoRisco.Avaliar(Classificacao(null, null, null, vinte)).RiscosDeclarados.Count);

        var vinteEUm = Enumerable.Range(0, 21).Select(_ => Risco()).ToArray();
        AssertRecusa(ErrorCode.CtrClassificacaoRiscoInvalida,
            () => CtrClassificacaoRisco.Avaliar(Classificacao(null, null, null, vinteEUm)), "no máximo 20");
    }

    [Fact]
    public void Avaliar_NormalizaOsTextosDoRiscoDeclarado()
    {
        var risco = Risco();
        risco.DescricaoRisco = "  Atraso  ";
        risco.AcaoMitigacao = "  Multa ";
        risco.ResponsavelNome = " Gestor ";
        // A caixa do e-mail não é informação: sai sempre em minúsculas
        risco.ResponsavelEmail = "  Gestor.Contrato@SEEC.DF.gov.br ";
        risco.Probabilidade = " Provável ";
        risco.Consequencia = "Maior ";

        var avaliado = Assert.Single(CtrClassificacaoRisco.Avaliar(Classificacao(null, null, null, risco)).RiscosDeclarados);

        Assert.Equal(new CtrClassificacaoRisco.RiscoAvaliado("Atraso", "Multa", "Gestor", EmailResponsavel,
            "Provável", "Maior"), avaliado);
    }

    // ══ Equivalência com a regra do PGIA (lida por reflexão, sem alterar o PGIA) ══

    private static readonly MethodInfo RegraDoPgia = typeof(PgiaSistemaService)
        .GetMethod("AvaliarClassificacao", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("A regra do PGIA mudou de nome: reveja a réplica no módulo.");

    private static (string Resultado, string? Enquadramento, int Pontuacao, string ChecklistJson) AvaliarNoPgia(
        CtrClassificacaoRiscoDTO dto)
    {
        var pgia = new PgiaClassificacaoCreateDTO
        {
            Checklist = new PgiaChecklistDTO
            {
                Q15 = dto.Q15.ToList(),
                Q16 = dto.Q16.ToList(),
                Q17 = dto.Q17.ToList(),
                Q15Nenhuma = dto.Q15Nenhuma,
                Q16Nenhuma = dto.Q16Nenhuma,
                Q17Nenhuma = dto.Q17Nenhuma
            },
            // A equivalência é do resultado: no PGIA o grupo "Outros" só aceita o
            // quadrante baixo, então cada risco vira um risco permitido lá
            OutrosRiscos = dto.RiscosDeclarados.Select(_ => new PgiaRiscoOutroDTO
            {
                DescricaoRisco = "Risco",
                AcaoMitigacao = "Mitigação",
                ResponsavelNome = "Responsável",
                ResponsavelEmail = EmailResponsavel,
                Probabilidade = PgiaDominios.EscalaCgdf.Probabilidade.Raro,
                Consequencia = PgiaDominios.EscalaCgdf.Consequencia.Menor
            }).ToList(),
            Motivo = PgiaDominios.MotivoClassificacao.Todos[0],
            DataClassificacao = new DateOnly(2026, 9, 15),
            Justificativa = "Equivalência com a Supervisão Contínua das Contratações"
        };

        object avaliada;
        try
        {
            avaliada = RegraDoPgia.Invoke(null, new object?[] { pgia, "sgdi@local.teste" })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        var tipo = avaliada.GetType();
        object? Ler(string nome) => tipo.GetProperty(nome)!.GetValue(avaliada);

        return ((string)Ler("Resultado")!, (string?)Ler("Enquadramento"), (int)Ler("Pontuacao")!,
            (string)Ler("ChecklistJson")!);
    }

    /// <summary>"I,III" = incisos marcados; null = "nenhuma" (com um risco quando os três são nenhuma).</summary>
    private static CtrClassificacaoRiscoDTO DeTexto(string? q15, string? q16, string? q17)
    {
        string[]? Incisos(string? texto) => texto?.Split(',', StringSplitOptions.RemoveEmptyEntries);

        return q15 == null && q16 == null && q17 == null
            ? Classificacao(null, null, null, Risco())
            : Classificacao(Incisos(q15), Incisos(q16), Incisos(q17));
    }

    public static IEnumerable<object?[]> ChecklistsRepresentativos() => new[]
    {
        new object?[] { "III,I", "II", "IV" },
        new object?[] { "I,II,III,IV,V,VI", "I,II,III,IV,V,VI,VII,VIII,IX", "I,II,III,IV" },
        new object?[] { "VI", null, null },
        new object?[] { null, "IX,II,II", null },
        new object?[] { null, "VIII", "I,IV" },
        new object?[] { null, "III", "II" },
        new object?[] { null, null, "III,II" },
        new object?[] { null, null, "IV" },
        new object?[] { null, null, null }
    };

    [Theory]
    [MemberData(nameof(ChecklistsRepresentativos))]
    public void Avaliar_EquivaleARegraDoPgia(string? q15, string? q16, string? q17)
    {
        var dto = DeTexto(q15, q16, q17);

        var modulo = CtrClassificacaoRisco.Avaliar(dto);
        var pgia = AvaliarNoPgia(dto);

        Assert.Equal(pgia.Resultado, modulo.Resultado);
        Assert.Equal(pgia.Enquadramento, modulo.Enquadramento);
        Assert.Equal(pgia.Pontuacao, modulo.Pontuacao);
        // Mesmo jsonb, byte a byte
        Assert.Equal(pgia.ChecklistJson, modulo.ChecklistJson);
        // E o cálculo público do módulo dá o mesmo que a avaliação completa
        Assert.Equal((modulo.Resultado, modulo.Enquadramento),
            CtrClassificacaoRisco.CalcularResultado(dto.Q15, dto.Q16, dto.Q17));
    }

    [Theory]
    [InlineData("inciso-e-nenhuma")]
    [InlineData("grupo-sem-resposta")]
    [InlineData("tudo-nenhuma-sem-risco")]
    [InlineData("inciso-inexistente")]
    public void Avaliar_RecusaOsMesmosChecklistsQueOPgia(string caso)
    {
        var dto = caso switch
        {
            "inciso-e-nenhuma" => Classificacao(null, new[] { "II" }, null),
            "grupo-sem-resposta" => Classificacao(new[] { "I" }, null, null),
            "tudo-nenhuma-sem-risco" => Classificacao(null, null, null),
            _ => Classificacao(new[] { "VII" }, null, null)
        };
        if (caso == "inciso-e-nenhuma") dto.Q16Nenhuma = true;
        if (caso == "grupo-sem-resposta") dto.Q17Nenhuma = false;

        Assert.Throws<ApiException>(() => CtrClassificacaoRisco.Avaliar(dto));
        Assert.Throws<ApiException>(() => AvaliarNoPgia(dto));
    }
}
