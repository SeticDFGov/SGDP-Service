namespace Models.Planejamento;

/// <summary>
/// Domínios fechados do módulo Governança Estratégica. Cada lista gera o CHECK da
/// coluna em PeModelConfiguration (fonte única, como PgiaDominios e CtrDominios). Os
/// papéis do módulo ficam em app.Auth.PapeisPlanejamento, ao lado dos outros papéis.
/// </summary>
public static class PeDominios
{
    /// <summary>De onde veio uma mudança de papel (pe_papel_usuario_historico.origem).</summary>
    public static class OrigemPapel
    {
        // Tela "Pessoas e acessos" do módulo (administrador do módulo ou admin geral)
        public const string Pessoas = "pessoas";
        // Aprovação de um pedido de acesso
        public const string Pedido = "pedido";
        // Tela de gestão de acessos da Administração
        public const string GestaoAcessos = "gestao_acessos";
        // Login do modo local (só testes, Development)
        public const string ModoLocal = "modo_local";

        public static readonly string[] Todas = { Pessoas, Pedido, GestaoAcessos, ModoLocal };
    }

    /// <summary>
    /// Situação de um passo, seção ou campo num nível de maturidade, num ajuste de órgão
    /// ou na situação geral das seções fora do PDTIC. Desligado guarda e esconde: nada se
    /// apaga (decisão 13 do plano).
    /// </summary>
    public static class Situacao
    {
        public const string Obrigatorio = "obrigatorio";
        public const string Opcional = "opcional";
        public const string Desligado = "desligado";

        public static readonly string[] Todas = { Obrigatorio, Opcional, Desligado };
    }

    /// <summary>
    /// A quem a seção serve: o PDTIC de cada órgão (depende do nível de maturidade), o
    /// PETIC-DF ou o catálogo do DF (princípios, diretrizes do ciclo). As duas últimas não
    /// dependem de nível: usam a situação geral.
    /// </summary>
    public static class Escopo
    {
        public const string Pdtic = "pdtic";
        public const string Petic = "petic";
        public const string Df = "df";

        public static readonly string[] Todos = { Pdtic, Petic, Df };
    }

    /// <summary>Formulário (um registro) ou tabela (vários registros).</summary>
    public static class TipoSecao
    {
        public const string Formulario = "formulario";
        public const string Tabela = "tabela";

        public static readonly string[] Todos = { Formulario, Tabela };
    }

    /// <summary>Item que um ajuste de órgão muda (pe_orgao_ajuste.alvo_tipo).</summary>
    public static class AlvoAjuste
    {
        public const string Passo = "passo";
        public const string Secao = "secao";
        public const string Campo = "campo";

        public static readonly string[] Todos = { Passo, Secao, Campo };
    }

    /// <summary>
    /// Tipo do passo: diz qual tela o front usa. Só "dados" mostra as seções do passo; os
    /// outros chegam nas próximas entregas (o front mostra "em construção" até lá).
    /// </summary>
    public static class TipoPasso
    {
        // Seções do passo (formulários e tabelas)
        public const string Dados = "dados";
        // Prévia do documento do PDTIC (E5)
        public const string Documento = "documento";
        // Editor de fluxo (E6)
        public const string Fluxo = "fluxo";
        // Registro de aprovação (E7)
        public const string Aprovacao = "aprovacao";
        // Envio ao CGTIC, que vale como a comunicação à SGDI (E7)
        public const string Envio = "envio";
        // Acompanhar a deliberação do CGTIC (E7)
        public const string Deliberacao = "deliberacao";
        // Publicação (E7)
        public const string Publicacao = "publicacao";
        // Ações por tema dos incisos V, VI e IX do art. 12, § 2º (E4)
        public const string ConferenciaTemas = "conferencia_temas";
        // Ciclo de monitoramento (E7)
        public const string Monitoramento = "monitoramento";

        public static readonly string[] Todos =
            { Dados, Documento, Fluxo, Aprovacao, Envio, Deliberacao, Publicacao, ConferenciaTemas, Monitoramento };
    }

    /// <summary>Tipos de campo (lista fechada do plano, seção 6.1).</summary>
    public static class TipoCampo
    {
        public const string TextoCurto = "texto_curto";
        public const string TextoLongo = "texto_longo";
        public const string TextoRico = "texto_rico";
        public const string Numero = "numero";
        public const string Moeda = "moeda";
        public const string Percentual = "percentual";
        public const string Data = "data";
        public const string SimNao = "sim_nao";
        public const string Lista = "lista";
        public const string ListaMultipla = "lista_multipla";
        public const string LigacaoSecao = "ligacao_secao";
        public const string LigacaoCatalogo = "ligacao_catalogo";
        public const string Arquivo = "arquivo";
        public const string Calculado = "calculado";

        public static readonly string[] Todos =
        {
            TextoCurto, TextoLongo, TextoRico, Numero, Moeda, Percentual, Data, SimNao,
            Lista, ListaMultipla, LigacaoSecao, LigacaoCatalogo, Arquivo, Calculado
        };

        /// <summary>Tipos que têm opções em pe_opcao.</summary>
        public static bool TemOpcoes(string tipo) => tipo is Lista or ListaMultipla;
    }

    /// <summary>Cálculos permitidos no campo calculado (nada de fórmula livre).</summary>
    public static class Calculo
    {
        public const string Produto = "produto";
        public const string SomaPonderada = "soma_ponderada";
        public const string NivelRisco = "nivel_risco";
        public const string Subtracao = "subtracao";

        public static readonly string[] Todos = { Produto, SomaPonderada, NivelRisco, Subtracao };
    }

    /// <summary>Catálogos que um campo de ligação com catálogo pode usar.</summary>
    public static class Catalogo
    {
        // Objetivos estratégicos do PETIC-DF vigente (E3)
        public const string PeticObjetivo = "petic_objetivo";
        // Eixos de governo do PETIC-DF vigente (E3)
        public const string PeticEixo = "petic_eixo";
        // Princípios do art. 4º e os acrescentados pelo administrador (E3)
        public const string Principio = "principio";
        // Sistemas de IA do inventário do PGIA (só leitura)
        public const string PgiaSistema = "pgia_sistema";

        public static readonly string[] Todos = { PeticObjetivo, PeticEixo, Principio, PgiaSistema };

        /// <summary>
        /// Catálogos feitos de registros (E3): a chave da seção de onde saem os itens. Os dois
        /// do PETIC-DF leem a versão vigente; o de princípios lê o catálogo do DF. O
        /// pgia_sistema não é feito de registros: lê o inventário do PGIA do órgão do PDTIC
        /// (E4), e a ligação fica no jsonb do registro (lista de ids), não em pe_vinculo.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> SecaoDoCatalogo = new Dictionary<string, string>
        {
            [PeticObjetivo] = "petic_objetivo",
            [PeticEixo] = "petic_eixo",
            [Principio] = "principio"
        };

        /// <summary>Catálogo que sai do PETIC-DF vigente (sem vigente, a ligação fica opcional).</summary>
        public static bool DoPetic(string? catalogo) => catalogo is PeticObjetivo or PeticEixo;

        /// <summary>Catálogo dos sistemas de IA do PGIA (só no PDTIC; ids guardados no jsonb).</summary>
        public static bool DoPgia(string? catalogo) => catalogo == PgiaSistema;
    }

    // ── Referenciais e registros (E3) ──────────────────────────────────────────

    /// <summary>
    /// Situação de uma versão do PETIC-DF (pe_petic.situacao). Uma versão em rascunho (ou em
    /// deliberação) por vez e uma aprovada (a vigente); aprovar uma nova marca a anterior
    /// como substituída.
    /// </summary>
    public static class SituacaoPetic
    {
        public const string Rascunho = "rascunho";
        public const string EmDeliberacao = "em_deliberacao";
        public const string Aprovado = "aprovado";
        public const string Substituido = "substituido";

        public static readonly string[] Todas = { Rascunho, EmDeliberacao, Aprovado, Substituido };

        // No máximo uma versão em cada uma destas (índice único parcial)
        public static readonly string[] Unicas = { Rascunho, EmDeliberacao, Aprovado };
    }

    /// <summary>O que o CGTIC delibera (pe_deliberacao.objeto_tipo). O PDTIC chega na E7.</summary>
    public static class ObjetoDeliberacao
    {
        public const string Petic = "petic";
        public const string Pdtic = "pdtic";

        public static readonly string[] Todos = { Petic, Pdtic };
    }

    /// <summary>Situação de uma deliberação do CGTIC (pe_deliberacao.situacao).</summary>
    public static class SituacaoDeliberacao
    {
        public const string Aguardando = "aguardando";
        public const string Aprovado = "aprovado";
        public const string Devolvido = "devolvido";

        public static readonly string[] Todas = { Aguardando, Aprovado, Devolvido };

        // O que a Secretaria do CGTIC registra
        public static readonly string[] Decisoes = { Aprovado, Devolvido };
    }

    /// <summary>
    /// Dono dos registros: o catálogo do DF (escopo df, sem dono), uma versão do PETIC-DF
    /// (escopo petic) ou o PDTIC de um órgão (escopo pdtic, desde a E4).
    /// </summary>
    public static class DonoRegistro
    {
        public const string Df = "df";
        public const string Petic = "petic";
        public const string Pdtic = "pdtic";

        public static readonly string[] Todos = { Df, Petic, Pdtic };
    }

    // ── PDTIC dos órgãos (E4) ──────────────────────────────────────────────────

    /// <summary>
    /// Situação do PDTIC de um órgão (pe_pdtic.situacao), no ciclo da decisão 18 do plano. A
    /// E4 abre em elaboração; as transições seguintes (envio, deliberação, publicação,
    /// acompanhamento, encerramento, revisão) chegam na E7.
    /// </summary>
    public static class SituacaoPdtic
    {
        public const string EmElaboracao = "em_elaboracao";
        public const string EmAprovacao = "em_aprovacao";
        public const string Devolvido = "devolvido";
        public const string Aprovado = "aprovado";
        public const string Publicado = "publicado";
        public const string EmAcompanhamento = "em_acompanhamento";
        public const string Encerrado = "encerrado";
        public const string Substituido = "substituido";

        public static readonly string[] Todas =
            { EmElaboracao, EmAprovacao, Devolvido, Aprovado, Publicado, EmAcompanhamento, Encerrado, Substituido };

        // Fora do "atual": o órgão pode abrir outro PDTIC (índice único parcial ux_pe_pdtic_atual)
        public static readonly string[] Encerradas = { Encerrado, Substituido };

        // A equipe do órgão edita os registros só nestas
        public static readonly string[] Editaveis = { EmElaboracao, Devolvido };

        /// <summary>Texto para a tela e para as planilhas.</summary>
        public static string Rotulo(string situacao) => situacao switch
        {
            EmElaboracao => "Em elaboração",
            EmAprovacao => "Em aprovação",
            Devolvido => "Devolvido para ajuste",
            Aprovado => "Aprovado",
            Publicado => "Publicado",
            EmAcompanhamento => "Em acompanhamento",
            Encerrado => "Encerrado",
            Substituido => "Substituído",
            _ => situacao
        };
    }

    /// <summary>
    /// Situação de cada passo da trilha do PDTIC (GET pdtic/{id}/situacao), calculada no
    /// servidor. "continuo" vale para os tipos de passo das próximas entregas (documento,
    /// fluxo, aprovação, envio, deliberação, publicação e monitoramento).
    /// </summary>
    public static class SituacaoPasso
    {
        public const string Feito = "feito";
        public const string Pendente = "pendente";
        // Há comentário aberto no passo
        public const string Atencao = "atencao";
        public const string NaoSeAplica = "nao_se_aplica";
        public const string Continuo = "continuo";

        public static readonly string[] Todas = { Feito, Pendente, Atencao, NaoSeAplica, Continuo };
    }

    /// <summary>
    /// Chaves de seções e campos do modelo inicial que o código do PDTIC usa: a vigência
    /// (copiada para pe_pdtic) e os avisos do guia (fraqueza sem necessidade, ameaça sem
    /// risco, equipe só de TIC). São itens do sistema: não mudam de chave nem de tipo.
    /// </summary>
    public static class ChavePdtic
    {
        public const string SecaoAbrangencia = "abrangencia";
        public const string CampoVigenciaInicio = "vigencia_inicio";
        public const string CampoVigenciaFim = "vigencia_fim";

        public const string SecaoEquipe = "equipe_elaboracao";
        public const string CampoTipoArea = "tipo_area";
        public const string AreaTic = "tic";
        public const string AreaFinalistica = "finalistica";

        public const string SecaoFraquezas = "swot_fraquezas";
        public const string SecaoNecessidades = "necessidades";
        public const string CampoFraqueza = "fraqueza";

        public const string SecaoAmeacas = "swot_ameacas";
        public const string SecaoRiscos = "riscos";
        public const string CampoAmeaca = "ameaca";
    }

    /// <summary>
    /// Os três temas do decreto que o passo de conferência (tipo conferencia_temas) confere:
    /// segurança da informação e continuidade (inciso V), transformação digital e
    /// interoperabilidade (VI) e governança de dados (IX) do art. 12, § 2º. O valor é o da
    /// opção travada do campo acoes.tema; a justificativa de tema sem ação fica no campo da
    /// seção temas_sem_acao (formulário opcional do passo 3.4).
    /// </summary>
    public static class TemaDecreto
    {
        public const string SecaoAcoes = "acoes";
        public const string CampoTema = "tema";
        public const string CampoDescricao = "descricao";
        public const string CampoSituacao = "situacao";
        public const string SecaoJustificativas = "temas_sem_acao";

        public sealed record Tema(string Valor, string Inciso, string CampoJustificativa);

        public static readonly IReadOnlyList<Tema> Todos = new[]
        {
            new Tema("seguranca", "V", "justificativa_seguranca"),
            new Tema("transformacao_digital", "VI", "justificativa_transformacao"),
            new Tema("governanca_dados", "IX", "justificativa_dados")
        };
    }

    /// <summary>
    /// Dono de um arquivo (pe_arquivo.dono_tipo): o registro cujo campo aponta para ele; desde a
    /// E5, o PDTIC (as imagens dos textos que o órgão editou no documento e os PDFs gerados) e o
    /// modelo do documento (as imagens dos textos padrão, que todo papel do módulo lê). Nulo =
    /// recém-enviado, ainda sem dono (só quem enviou vê).
    /// </summary>
    public static class DonoArquivo
    {
        public const string Registro = "registro";
        // Imagens dos textos do órgão no documento e as versões geradas em PDF (dono_id = id do PDTIC)
        public const string Pdtic = "pdtic";
        // Imagens dos textos padrão do modelo do documento (dono_id = id do modelo)
        public const string DocModelo = "doc_modelo";

        public static readonly string[] Todos = { Registro, Pdtic, DocModelo };
    }

    /// <summary>Largura da coluna do campo nas tabelas.</summary>
    public static class Largura
    {
        public const string Estreita = "estreita";
        public const string Media = "media";
        public const string Larga = "larga";

        public static readonly string[] Todas = { Estreita, Media, Larga };
    }

    /// <summary>
    /// Cor de uma opção de lista (situação, prioridade, nível de risco). Nome do tom,
    /// não o código: o front traduz para as classes do tema.
    /// </summary>
    public static class Cor
    {
        public static readonly string[] Todas = { "verde", "amarelo", "laranja", "vermelho", "azul", "roxo", "cinza" };
    }

    /// <summary>Tipos de arquivo aceitos por um campo de arquivo.</summary>
    public static class TipoArquivo
    {
        public static readonly string[] Todos = { "pdf", "png", "jpg" };

        // Limite geral dos anexos do módulo (plano, seção 14)
        public const int MaximoMb = 25;
    }

    /// <summary>
    /// Incisos do art. 12, § 2º, do Decreto nº 48.900/2026 (os nove conteúdos mínimos do
    /// PDTIC). Um passo pode cobrir mais de um: "V,VI,IX".
    /// </summary>
    public static class Inciso
    {
        public static readonly string[] Todos = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };

        public static bool EhValido(string? valor) =>
            !string.IsNullOrEmpty(valor) && valor.Split(',').All(i => Todos.Contains(i));
    }

    /// <summary>Que item o histórico do modelo descreve (pe_modelo_historico.entidade).</summary>
    public static class EntidadeHistorico
    {
        public const string Nivel = "nivel";
        public const string Etapa = "etapa";
        public const string Passo = "passo";
        public const string Secao = "secao";
        public const string Campo = "campo";
        public const string Opcao = "opcao";
        // Nível de um órgão (entidade_id = id do órgão)
        public const string OrgaoNivel = "orgao_nivel";
        // Ajustes de um órgão (entidade_id = id do órgão)
        public const string OrgaoAjuste = "orgao_ajuste";
        // Modelo do documento (E5): capítulo e bloco
        public const string DocCapitulo = "doc_capitulo";
        public const string DocBloco = "doc_bloco";

        public static readonly string[] Todas =
            { Nivel, Etapa, Passo, Secao, Campo, Opcao, OrgaoNivel, OrgaoAjuste, DocCapitulo, DocBloco };
    }

    /// <summary>O que aconteceu com o item (pe_modelo_historico.acao).</summary>
    public static class AcaoHistorico
    {
        public const string Criacao = "criacao";
        public const string Alteracao = "alteracao";
        // Situação em cada nível (ou a situação geral)
        public const string Situacao = "situacao";
        public const string Ordem = "ordem";
        // Exclusão lógica: o item some, os dados ficam guardados
        public const string Exclusao = "exclusao";
        // Remoção de verdade (opção criada pelo administrador e nunca usada; ajuste de órgão)
        public const string Remocao = "remocao";

        public static readonly string[] Todas = { Criacao, Alteracao, Situacao, Ordem, Exclusao, Remocao };
    }

    // ── Documento do PDTIC (E5) ────────────────────────────────────────────────

    /// <summary>Tipo do modelo de documento (pe_doc_modelo.tipo). A E7 acrescenta ra e rr.</summary>
    public static class TipoDocumento
    {
        public const string Pdtic = "pdtic";

        public static readonly string[] Todos = { Pdtic };

        /// <summary>O título do documento, como sai na capa e na prévia.</summary>
        public static string Titulo(string tipo) => tipo switch
        {
            Pdtic => "Plano Diretor de Tecnologia da Informação e Comunicação",
            _ => tipo
        };
    }

    /// <summary>
    /// Tipo de um bloco do capítulo (pe_doc_bloco.tipo): o texto rico (editável pelo órgão), a
    /// tabela de uma seção, a lista das ações de um tema, a matriz SWOT, o fluxo (desenhado a
    /// partir da E6) e a quebra de página.
    /// </summary>
    public static class TipoBloco
    {
        public const string Texto = "texto";
        public const string TabelaSecao = "tabela_secao";
        public const string ListaTema = "lista_tema";
        public const string MatrizSwot = "matriz_swot";
        public const string Fluxo = "fluxo";
        public const string QuebraPagina = "quebra_pagina";

        public static readonly string[] Todos = { Texto, TabelaSecao, ListaTema, MatrizSwot, Fluxo, QuebraPagina };
    }

    /// <summary>
    /// Situação de uma versão gerada do documento (pe_doc_versao.situacao). A E5 gera minutas;
    /// a E7 congela a enviada ao CGTIC e a marca como aprovada e publicada.
    /// </summary>
    public static class SituacaoVersaoDoc
    {
        public const string Minuta = "minuta";
        public const string Enviada = "enviada";
        public const string Aprovada = "aprovada";
        public const string Publicada = "publicada";

        public static readonly string[] Todas = { Minuta, Enviada, Aprovada, Publicada };

        public static string Rotulo(string situacao) => situacao switch
        {
            Minuta => "Minuta",
            Enviada => "Enviada ao CGTIC",
            Aprovada => "Aprovada",
            Publicada => "Publicada",
            _ => situacao
        };
    }

    /// <summary>
    /// Capítulos do modelo com desenho próprio no PDF: a capa, a folha de rosto, o histórico de
    /// versões e o sumário (os elementos pré-textuais do Anexo X do guia) e os anexos, que
    /// começam numa página nova. São capítulos do sistema: a chave não muda.
    /// </summary>
    public static class CapituloEspecial
    {
        public const string Capa = "capa";
        public const string FolhaRosto = "folha_rosto";
        public const string Historico = "historico_versoes";
        public const string Sumario = "sumario";
        public const string Anexos = "anexos";

        public static readonly string[] PreTextuais = { Capa, FolhaRosto, Historico, Sumario };

        public static bool EhPreTextual(string chave) => PreTextuais.Contains(chave);
    }

    /// <summary>
    /// O dicionário de nomes do órgão (seção nomes do passo 1.2), fonte dos marcadores
    /// {nomes.*} do documento e do logotipo da capa (campo de arquivo, desde a E5).
    /// </summary>
    public static class DicionarioNomes
    {
        public const string Secao = "nomes";
        public const string Comite = "comite";
        public const string Equipe = "equipe_elaboracao";
        public const string EquipeAcompanhamento = "equipe_acompanhamento";
        public const string AutoridadeCargo = "autoridade_cargo";
        public const string AutoridadeNome = "autoridade_nome";
        public const string UnidadeTic = "unidade_tic";
        public const string SiglaOrgao = "sigla_orgao";
        public const string Logotipo = "logotipo";
    }

    /// <summary>As quatro seções da análise SWOT (passo 2.5) e o campo do texto de cada item.</summary>
    public static class SecoesSwot
    {
        public const string Forcas = "swot_forcas";
        public const string Fraquezas = "swot_fraquezas";
        public const string Oportunidades = "swot_oportunidades";
        public const string Ameacas = "swot_ameacas";
        public const string CampoTexto = "descricao";

        public static readonly string[] Todas = { Forcas, Fraquezas, Oportunidades, Ameacas };
    }
}
