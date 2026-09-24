using System.IO.Compression;
using System.Text;
using System.Text.Json;
using api.Planejamento;
using app.Models;
using Controllers.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service.Planejamento;

namespace test.planejamento;

/// <summary>
/// Base dos testes da E5 (documento do PDTIC): a base do PDTIC (E4), com o modelo inicial na
/// versão 3 (o modelo do documento já carregado), os serviços do documento e do modelo do
/// documento e atalhos para ler o documento, achar capítulos e blocos, preencher o dicionário
/// de nomes e montar um PDTIC completo no Avançado (o do PDF de exemplo).
/// </summary>
public abstract class PeDocumentoTestBase : PePdticTestBase
{
    protected readonly PeDocumentoService Documentos;
    protected readonly PeDocModeloService ModeloDoc;

    protected PeDocumentoTestBase()
    {
        Documentos = new PeDocumentoService(Context, Registros, Permissoes);
        ModeloDoc = new PeDocModeloService(Context);
    }

    protected async Task<PeDocumentoResponse> DocumentoAsync(long pdticId, User? user = null) =>
        await Documentos.ObterAsync(pdticId, await ContextoDe(user ?? UserOrgaoSes));

    protected static PeDocCapituloResponse Cap(PeDocumentoResponse documento, string chave) =>
        documento.Capitulos.Single(c => c.Chave == chave);

    protected static PeDocCapituloResponse? CapOuNulo(PeDocumentoResponse documento, string chave) =>
        documento.Capitulos.SingleOrDefault(c => c.Chave == chave);

    /// <summary>O primeiro bloco de texto do capítulo.</summary>
    protected static PeDocBlocoResponse Texto(PeDocumentoResponse documento, string chave) =>
        Cap(documento, chave).Blocos.First(b => b.Tipo == PeDominios.TipoBloco.Texto);

    protected static PeDocBlocoResponse TabelaDe(PeDocumentoResponse documento, string capitulo, string secao) =>
        Cap(documento, capitulo).Blocos.Single(b => b.Tabela?.SecaoChave == secao);

    protected PeDocCapitulo CapituloDoModelo(string chave) => Context.PeDocCapitulos.AsNoTracking().Single(c => c.Chave == chave);

    protected PeDocBloco BlocoDoModelo(string capitulo, string tipo = PeDominios.TipoBloco.Texto)
    {
        var id = CapituloDoModelo(capitulo).Id;
        return Context.PeDocBlocos.AsNoTracking().Where(b => b.CapituloId == id && b.Tipo == tipo).OrderBy(b => b.Ordem).First();
    }

    /// <summary>O texto de um JSON do TipTap (os nós de texto, juntos).</summary>
    protected static string TextoDe(JsonElement? documento) =>
        documento is JsonElement e ? PeValores.TextoDoRico(System.Text.Json.Nodes.JsonNode.Parse(e.GetRawText())) : string.Empty;

    protected static JsonElement Json(object valor) => JsonSerializer.SerializeToElement(valor);

    // ── Dados do PDTIC ────────────────────────────────────────────────────────

    protected async Task PreencherNomesAsync(long pdticId, object? extra = null)
    {
        var dados = new Dictionary<string, object?>
        {
            ["comite"] = "Subcomitê Gestor de TIC da Saúde",
            ["equipe_elaboracao"] = "Equipe de Elaboração do PDTIC",
            ["equipe_acompanhamento"] = "Equipe de Acompanhamento do PDTIC",
            ["autoridade_cargo"] = "Secretária de Estado de Saúde",
            ["autoridade_nome"] = "Maria da Silva",
            ["unidade_tic"] = "Subsecretaria de Tecnologia da Informação"
        };
        if (extra != null)
            foreach (var p in JsonSerializer.SerializeToElement(extra).EnumerateObject())
                dados[p.Name] = p.Value;
        await IncluirNoPdticAsync(pdticId, "nomes", dados);
    }

    protected async Task PreencherAbrangenciaAsync(long pdticId, object? extra = null)
    {
        var dados = new Dictionary<string, object?>
        {
            ["tipo_abrangencia"] = "todo_com_vinculadas",
            ["unidades"] = "Sede, superintendências regionais e hospitais da rede",
            ["vigencia_inicio"] = "2026-01-01",
            ["vigencia_fim"] = "2029-12-31",
            ["periodicidade_revisao"] = "anual"
        };
        if (extra != null)
            foreach (var p in JsonSerializer.SerializeToElement(extra).EnumerateObject())
                dados[p.Name] = p.Value;
        await IncluirNoPdticAsync(pdticId, "abrangencia", dados);
    }

    // ── Imagens de verdade (o conversor abre e desenha) ───────────────────────

    /// <summary>Um PNG válido (RGB, 8 bits) com um degradê, montado à mão.</summary>
    protected static byte[] PngDeVerdade(int largura = 120, int altura = 60) => PeImagensDeTeste.Png(largura, altura);

    /// <summary>Um JPEG válido (o marcador de imagem do QuestPDF).</summary>
    protected static byte[] JpegDeVerdade(int largura = 300, int altura = 120) => QuestPDF.Helpers.Placeholders.Image(largura, altura);

    // ── PDTIC completo (Avançado), o do PDF de exemplo ────────────────────────

    /// <summary>Texto rico com todos os nós do conversor: títulos, marcas, link, listas, tabela mesclada e imagem.</summary>
    protected static object DiagnosticoRico(long imagemId) => Doc(
        new { type = "heading", attrs = new { level = 3 }, content = new object[] { new { type = "text", text = "Estrutura da TIC" } } },
        new
        {
            type = "paragraph",
            content = new object[]
            {
                new { type = "text", text = "A Subsecretaria de Tecnologia da Informação responde ao gabinete e atende " },
                new { type = "text", text = "todas as unidades", marks = new object[] { new { type = "bold" } } },
                new { type = "text", text = " da secretaria, com " },
                new { type = "text", text = "sistemas finalísticos", marks = new object[] { new { type = "italic" } } },
                new { type = "text", text = " e " },
                new { type = "text", text = "infraestrutura própria", marks = new object[] { new { type = "underline" } } },
                new { type = "text", text = ". O organograma completo está no " },
                new
                {
                    type = "text", text = "portal da secretaria",
                    marks = new object[] { new { type = "link", attrs = new { href = "https://www.saude.df.gov.br/organograma" } } }
                },
                new { type = "text", text = "." },
                new { type = "hardBreak" },
                new { type = "text", text = "Atualizado em setembro de 2026." }
            }
        },
        new
        {
            type = "bulletList",
            content = new object[]
            {
                Item("Coordenação de Sistemas: 18 pessoas."),
                Item("Coordenação de Infraestrutura: 22 pessoas."),
                new
                {
                    type = "listItem",
                    content = new object[]
                    {
                        Paragrafo("Coordenação de Atendimento:"),
                        new { type = "orderedList", attrs = new { start = 1, type = "a" }, content = new object[] { Item("Central de serviços."), Item("Suporte presencial.") } }
                    }
                }
            }
        },
        new { type = "heading", attrs = new { level = 4 }, content = new object[] { new { type = "text", text = "Pessoas por área" } } },
        new
        {
            type = "table",
            content = new object[]
            {
                new { type = "tableRow", content = new object[] { Celula("Área", true), Celula("Efetivos", true), Celula("Terceirizados", true) } },
                new { type = "tableRow", content = new object[] { Celula("Sistemas"), Celula("12"), Celula("6") } },
                new { type = "tableRow", content = new object[] { Celula("Infraestrutura"), Celula("9"), Celula("13") } },
                new { type = "tableRow", content = new object[] { Celula("Total: 40 pessoas", false, colspan: 3) } }
            }
        },
        new { type = "orderedList", content = new object[] { Item("Primeiro passo do diagnóstico."), Item("Segundo passo do diagnóstico.") } },
        new { type = "image", attrs = new { src = $"api/planejamento/arquivos/{imagemId}", alt = "Organograma da TIC", width = 360 } });

    protected static object Paragrafo(string texto) => new { type = "paragraph", content = new object[] { new { type = "text", text = texto } } };

    protected static object Item(string texto) => new { type = "listItem", content = new object[] { Paragrafo(texto) } };

    protected static object Celula(string texto, bool cabecalho = false, int colspan = 1, int rowspan = 1) =>
        colspan == 1 && rowspan == 1
            ? new { type = cabecalho ? "tableHeader" : "tableCell", content = new object[] { Paragrafo(texto) } }
            : (object)new { type = cabecalho ? "tableHeader" : "tableCell", attrs = new { colspan, rowspan }, content = new object[] { Paragrafo(texto) } };

    /// <summary>
    /// Um PDTIC da SES no Avançado com dados em quase todas as seções que o documento mostra,
    /// o logotipo, o diagnóstico em texto rico (com imagem) e um sistema de IA no PGIA.
    /// </summary>
    protected async Task<PePdticResponse> PdticCompletoAsync()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var pdtic = await AbrirSesAsync();
        var id = pdtic.Id;
        var logo = await EnviarArquivoAsync(UserOrgaoSes, "logo-ses.png", PngDeVerdade(480, 160));
        var organograma = await EnviarArquivoAsync(UserOrgaoSes, "organograma.png", PngDeVerdade(480, 240));

        await PreencherAbrangenciaAsync(id);
        await PreencherNomesAsync(id, new { logotipo = new { ArquivoId = logo.Id } });

        await IncluirNoPdticAsync(id, "equipe_elaboracao", new { nome = "Ana Souza", papel = "coordenacao", cargo = "Coordenadora de Governança", area = "Subsecretaria de TIC", tipo_area = "tic" });
        await IncluirNoPdticAsync(id, "equipe_elaboracao", new { nome = "Bruno Lima", papel = "membro", cargo = "Assessor", area = "Atenção Primária", tipo_area = "finalistica" });
        await IncluirNoPdticAsync(id, "equipe_elaboracao", new { nome = "Carla Dias", papel = "membro", area = "Vigilância em Saúde", tipo_area = "finalistica" });
        await IncluirNoPdticAsync(id, "equipe_designacao", new { ato_numero = "123/2026", ato_tipo = "portaria", ato_data = "2026-02-10", sei = "00060-00000001/2026-11" });
        await IncluirNoPdticAsync(id, "metodologia", new { metodologia_adotada = "guia_sisp", descricao = "Oficinas com as áreas, questionário e análise das demandas dos últimos dois anos.", tecnicas = new[] { "comite", "questionarios" } });
        await IncluirNoPdticAsync(id, "documentos_referencia", new { identificacao = "Plano Plurianual 2024 a 2027", tipo = "ppa" });
        await IncluirNoPdticAsync(id, "documentos_referencia", new { identificacao = "Estratégia de Governança Digital do DF", tipo = "egd", link = "https://www.df.gov.br/egd" });
        await IncluirNoPdticAsync(id, "principios_diretrizes", new { principio = "Priorizar soluções corporativas do GDF.", origem = "art4" });
        await IncluirNoPdticAsync(id, "principios_diretrizes", new { principio = "Toda nova solução precisa de plano de sustentação.", origem = "orgao" });

        var estrategia = await IncluirNoPdticAsync(id, "estrategias", new { descricao = "Ampliar o atendimento digital ao cidadão.", fonte = "pei" });
        await IncluirNoPdticAsync(id, "instrumentos_ausentes", new { instrumento = "Plano de continuidade", substituto = "Procedimentos de contingência da rede" });

        await IncluirNoPdticAsync(id, "diagnostico_ambiente", new Dictionary<string, object> { ["diagnostico"] = DiagnosticoRico(organograma.Id) });
        await IncluirNoPdticAsync(id, "quadro_pessoal_tic", new { efetivos = 21, comissionados = 5, terceirizados = 19, temporarios = 2 });
        await IncluirNoPdticAsync(id, "resultados_pdtic_anterior", new { resultados = "O PDTIC 2022 a 2025 entregou 70% das metas.", teve_anterior = true, licoes = "Planejar com as áreas finalísticas desde o início." });
        await IncluirNoPdticAsync(id, "referencial_estrategico", new { missao = "Prover soluções de TIC para a saúde pública do DF.", visao = "Ser referência em saúde digital até 2029.", valores = "Ética, transparência e foco no cidadão." });
        await IncluirNoPdticAsync(id, "objetivos_tic", new { objetivo = "Integrar os sistemas de regulação." });
        await IncluirNoPdticAsync(id, "swot_forcas", new { descricao = "Equipe técnica experiente." });
        await IncluirNoPdticAsync(id, "swot_forcas", new { descricao = "Rede própria nas unidades." });
        var fraqueza = await IncluirNoPdticAsync(id, "swot_fraquezas", new { descricao = "Sistemas legados sem suporte." });
        await IncluirNoPdticAsync(id, "swot_oportunidades", new { descricao = "Soluções corporativas do CeTIC-DF." });
        var ameaca = await IncluirNoPdticAsync(id, "swot_ameacas", new { descricao = "Ataques de ransomware ao setor de saúde." });
        await IncluirNoPdticAsync(id, "capacidade_execucao", new { capacidade_total = 12000, esforco_alocado = 9000, unidade = "horas", como_estimou = "Horas das equipes no último ano." });
        await IncluirNoPdticAsync(id, "plano_levantamento", new { areas = "Todas as subsecretarias.", abordagem = "Oficinas e questionário on-line.", instrumentos = new[] { "questionario", "reuniao" } });
        await IncluirNoPdticAsync(id, "necessidades_informacao", new { descricao = "Painel de filas por unidade.", areas = "Regulação" },
            new { estrategia = new[] { estrategia.Id } });
        await IncluirNoPdticAsync(id, "ativos", new { nome = "Prontuário Eletrônico", tipo = "sistema", situacao = "em_operacao", hospedagem = "cetic", usa_gdfnet = true, criticidade = "alta", responsavel = "Coordenação de Sistemas" });
        await IncluirNoPdticAsync(id, "ativos", new { nome = "Rede das unidades", tipo = "rede", situacao = "em_operacao", hospedagem = "outro", usa_gdfnet = true, criticidade = "alta", contrato = "Contrato 45/2024", contrato_vigencia = "2027-06-30" });
        await IncluirNoPdticAsync(id, "ativos", new { nome = "Sistema de Regulação antigo", tipo = "sistema", situacao = "em_desativacao", hospedagem = "datacenter_proprio", usa_gdfnet = false, criticidade = "media" });

        object Necessidade(string descricao, string tipo, int g, int u, int t, bool priorizada) => new
        {
            descricao, tipo, origem = "swot", areas = "Atenção Primária", gravidade = g, urgencia = u, tendencia = t, priorizada, valor_estimado = 250000.5m
        };
        var n1 = await IncluirNoPdticAsync(id, "necessidades", Necessidade("Substituir o sistema de regulação legado.", "servico", 5, 5, 4, true),
            new { fraqueza = new[] { fraqueza.Id } });
        var n2 = await IncluirNoPdticAsync(id, "necessidades", Necessidade("Ampliar a cobertura de backup dos servidores.", "infraestrutura", 5, 4, 5, true));
        var n3 = await IncluirNoPdticAsync(id, "necessidades", Necessidade("Integrar os dados de vacinação com o Ministério da Saúde.", "servico", 4, 3, 3, true));
        await IncluirNoPdticAsync(id, "necessidades", Necessidade("Trocar os monitores das recepções.", "infraestrutura", 2, 2, 2, false));
        await IncluirNoPdticAsync(id, "aquisicoes_ia", new { objeto = "Triagem de exames de imagem com IA.", finalidade = "Apoiar a priorização de laudos.", classificacao_prevista = "alto", previsao = "2027-06-30" },
            new { necessidade = new[] { n1.Id } });
        await IncluirNoPdticAsync(id, "criterios_priorizacao", new { confirmados = true, data_confirmacao = "2026-03-15" });
        await IncluirNoPdticAsync(id, "priorizacao", new { resumo = "O comitê aplicou a matriz GUT e confirmou as notas em reunião." });

        var m1 = await IncluirNoPdticAsync(id, "metas", new { descricao = "Implantar o novo sistema de regulação.", indicador = "Unidades usando o sistema", formula = "Unidades com o sistema / total de unidades", valor = "100%", prazo = "2027-12-31", situacao = "em_andamento" },
            new { necessidades = new[] { n1.Id } });
        var m2 = await IncluirNoPdticAsync(id, "metas", new { descricao = "Ter backup diário de todos os servidores críticos.", indicador = "Servidores com backup", formula = "Servidores com backup / servidores críticos", valor = "100%", prazo = "2026-12-31" },
            new { necessidades = new[] { n2.Id, n3.Id } });
        object Acao(string descricao, string[] tema, string situacao) => new
        {
            descricao, tema, responsavel = "Coordenação de Sistemas", unidade_demandante = "Regulação", unidade_executora = "Subsecretaria de TIC",
            inicio = "2026-03-01", conclusao = "2027-06-30", pessoas_quantidade = 4, pessoas_competencias = "Gestão de projetos e integração de sistemas",
            investimento = 1200000m, custeio = 300000m, situacao
        };
        var a1 = await IncluirNoPdticAsync(id, "acoes", Acao("Contratar e implantar a solução de regulação.", new[] { "transformacao_digital", "sistemas" }, "em_andamento"),
            new { metas = new[] { m1.Id } });
        var a2 = await IncluirNoPdticAsync(id, "acoes", Acao("Implantar a política de backup e o teste de restauração.", new[] { "seguranca" }, "nao_iniciada"),
            new { metas = new[] { m2.Id } });
        await IncluirNoPdticAsync(id, "acoes", Acao("Publicar a API de dados de vacinação.", new[] { "transformacao_digital" }, "nao_iniciada"),
            new { metas = new[] { m2.Id } });
        await IncluirNoPdticAsync(id, "temas_sem_acao", new { justificativa_dados = "A governança de dados será tratada no PDTIC seguinte, após a PGD/DF ser regulamentada." });
        await IncluirNoPdticAsync(id, "contratacoes", new { objeto = "Solução de regulação em nuvem.", tipo = "solucao", valor_estimado = 1500000m, ano_previsto = 2026, trimestre_previsto = "t3", no_pca = true, item_pca = "PCA 2026, item 42", situacao = "em_planejamento" },
            new { necessidades = new[] { n1.Id }, acoes = new[] { a1.Id } });
        await IncluirNoPdticAsync(id, "contratacoes", new { objeto = "Licenças de backup.", tipo = "renovacao", valor_estimado = 200000m, ano_previsto = 2027, trimestre_previsto = "t1", no_pca = false },
            new { acoes = new[] { a2.Id } });
        await IncluirNoPdticAsync(id, "plano_pessoas", new { quadro_minimo = "30 pessoas nas coordenações.", quadro_ideal = "45 pessoas, com 10 analistas de dados.", servicos_contratar = "Sustentação de sistemas." });
        await IncluirNoPdticAsync(id, "capacitacoes", new { tema = "Segurança da informação", publico = "Equipe de infraestrutura", quantidade = 12, previsao = "2026-08-31" });
        await IncluirNoPdticAsync(id, "orcamento_acoes", new { ano = 2026, investimento = 800000m, custeio = 150000m }, new { acao = new[] { a1.Id } });
        await IncluirNoPdticAsync(id, "orcamento_acoes", new { ano = 2027, investimento = 400000m, custeio = 150000m }, new { acao = new[] { a1.Id } });
        await IncluirNoPdticAsync(id, "orcamento_loa", new { valor_loa = 9500000m, observacao = "Valor da LOA 2026 para TIC." });
        await IncluirNoPdticAsync(id, "fatores_criticos", new { descricao = "Apoio da alta administração." });
        await IncluirNoPdticAsync(id, "fatores_criticos", new { descricao = "Orçamento garantido na LOA." });
        await IncluirNoPdticAsync(id, "riscos", new
        {
            descricao = "Atraso na contratação da solução de regulação.", probabilidade = "media", impacto = "alto", estrategia = "mitigar",
            acao_preventiva = "Começar o estudo técnico preliminar no primeiro trimestre.", gatilho = "Termo de referência não aprovado até junho.",
            resposta = "Prorrogar o contrato do sistema atual.", contingencia = "Manter o sistema legado com suporte extra.",
            responsavel = "Coordenação de Sistemas", proxima_revisao = "2026-06-30"
        }, new { metas = new[] { m1.Id }, acoes = new[] { a1.Id } });
        await IncluirNoPdticAsync(id, "riscos", new
        {
            descricao = "Ataque de ransomware.", probabilidade = "media", impacto = "alto", estrategia = "mitigar",
            acao_preventiva = "Backup isolado e testes de restauração.", gatilho = "Alerta do centro de resposta a incidentes.",
            resposta = "Acionar o plano de resposta a incidentes.", responsavel = "Coordenação de Infraestrutura", proxima_revisao = "2026-09-30"
        }, new { ameaca = new[] { ameaca.Id } });
        await IncluirNoPdticAsync(id, "responsavel_acompanhamento", new { modalidade = "equipe_propria", descricao = "Três servidores da Subsecretaria de TIC.", ato_tipo = "portaria", ato_numero = "130/2026", ato_data = "2026-04-01" });
        await IncluirNoPdticAsync(id, "periodicidade_monitoramento", new { periodicidade = "trimestral" });
        await IncluirNoPdticAsync(id, "plano_trabalho", new { objetivo = "Elaborar o PDTIC 2026 a 2029.", justificativa = "O PDTIC anterior venceu.", premissas_restricoes = "Participação das áreas finalísticas." });
        await IncluirNoPdticAsync(id, "partes_interessadas", new { nome = "Gabinete da Secretária", orgao = "SES" });
        await IncluirNoPdticAsync(id, "cronograma_elaboracao", new { atividade = "Levantar as necessidades", responsavel = "Equipe de elaboração", inicio = "2026-02-15", termino = "2026-04-30" });
        SistemaPgia(OrgaoSes, "Triagem de exames de imagem");
        SistemaPgia(OrgaoSes, "Chatbot de agendamento", "Baixo Risco", "Implantado (em uso)", null);

        return await Pdtics.ObterAsync(id, await Orgao());
    }

    // ── Controller ────────────────────────────────────────────────────────────

    protected PeDocumentoController ControladorDocumento(User user) => new(Documentos, ModeloDoc, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user) } }
    };

    // ── PDF ───────────────────────────────────────────────────────────────────

    protected static string TextoDoPdf(byte[] pdf) => Encoding.Latin1.GetString(pdf);
}

/// <summary>Imagens de verdade para os testes do documento (o conversor abre e desenha).</summary>
internal static class PeImagensDeTeste
{
    /// <summary>Um PNG válido (RGB, 8 bits) com um degradê, montado à mão.</summary>
    public static byte[] Png(int largura = 120, int altura = 60)
    {
        using var saida = new MemoryStream();
        saida.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        void Pedaco(string tipo, byte[] dados)
        {
            var nome = Encoding.ASCII.GetBytes(tipo);
            Inteiro(saida, dados.Length);
            saida.Write(nome);
            saida.Write(dados);
            Inteiro(saida, (int)Crc(nome.Concat(dados).ToArray()));
        }

        using (var ihdr = new MemoryStream())
        {
            Inteiro(ihdr, largura);
            Inteiro(ihdr, altura);
            ihdr.Write(new byte[] { 8, 2, 0, 0, 0 });
            Pedaco("IHDR", ihdr.ToArray());
        }
        var linhas = new byte[altura * (1 + largura * 3)];
        for (var y = 0; y < altura; y++)
        {
            var inicio = y * (1 + largura * 3);
            linhas[inicio] = 0;
            for (var x = 0; x < largura; x++)
            {
                linhas[inicio + 1 + x * 3] = (byte)(27 + x * 200 / Math.Max(1, largura));
                linhas[inicio + 2 + x * 3] = (byte)(54 + y * 150 / Math.Max(1, altura));
                linhas[inicio + 3 + x * 3] = 93;
            }
        }
        using (var zlib = new MemoryStream())
        {
            using (var z = new ZLibStream(zlib, CompressionLevel.Optimal, leaveOpen: true)) z.Write(linhas);
            Pedaco("IDAT", zlib.ToArray());
        }
        Pedaco("IEND", Array.Empty<byte>());
        return saida.ToArray();
    }

    private static void Inteiro(Stream s, int valor) =>
        s.Write(new[] { (byte)(valor >> 24), (byte)(valor >> 16), (byte)(valor >> 8), (byte)valor });

    private static uint Crc(byte[] dados)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in dados)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }
        return crc ^ 0xFFFFFFFFu;
    }
}
