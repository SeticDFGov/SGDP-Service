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

        // Os campos da seção dos princípios do catálogo do DF (os 11 do art. 4º são registros do sistema)
        public const string CampoTextoPrincipio = "texto";
        public const string CampoFundamentoPrincipio = "fundamento";
        public const string CampoCriterioPrincipio = "criterio_priorizacao";

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
    /// E4 abre em elaboração; a E7 faz as transições: enviar (em aprovação), a deliberação do
    /// CGTIC (aprovado ou devolvido), publicar, o acompanhamento, encerrar e a revisão (a
    /// versão anterior fica substituída quando a nova é aprovada).
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

        // Fora do PDTIC atual do órgão: nada mais muda nelas
        public static readonly string[] Encerradas = { Encerrado, Substituido };

        // A elaboração está aberta (etapas 1 a 3, o documento e os fluxos): a equipe do órgão edita
        public static readonly string[] Editaveis = { EmElaboracao, Devolvido };

        // A versão em elaboração do órgão (até a publicação): no máximo uma por órgão (ux_pe_pdtic_em_elaboracao)
        public static readonly string[] DaElaboracao = { EmElaboracao, EmAprovacao, Devolvido, Aprovado };

        // A versão vigente do órgão (publicada): no máximo uma por órgão (ux_pe_pdtic_vigente)
        public static readonly string[] Vigentes = { Publicado, EmAcompanhamento };

        /// <summary>
        /// Texto para a tela e para as planilhas. Desde a F1 (achado B15), "Devolvido pelo CGTIC",
        /// o mesmo rótulo da tela (a planilha consolidada dizia "Devolvido para ajuste").
        /// </summary>
        public static string Rotulo(string situacao) => situacao switch
        {
            EmElaboracao => "Em elaboração",
            EmAprovacao => "Em aprovação",
            Devolvido => "Devolvido pelo CGTIC",
            Aprovado => "Aprovado",
            Publicado => "Publicado",
            EmAcompanhamento => "Em acompanhamento",
            Encerrado => "Encerrado",
            Substituido => "Substituído",
            _ => situacao
        };

        /// <summary>O rótulo no meio da frase: só a primeira letra em minúscula (a sigla "CGTIC" fica).</summary>
        public static string RotuloMinusculo(string situacao)
        {
            var rotulo = Rotulo(situacao);
            return rotulo.Length == 0 ? rotulo : char.ToLowerInvariant(rotulo[0]) + rotulo[1..];
        }
    }

    /// <summary>
    /// Situação de cada passo da trilha do PDTIC (GET pdtic/{id}/situacao), calculada no
    /// servidor. Desde a E7: "aguardando" (o passo ainda não pode ser feito: as etapas 4 a 7
    /// antes da publicação, a publicação antes da aprovação, a deliberação enquanto o CGTIC
    /// decide), "atrasado" (o ciclo de monitoramento passou do prazo sem fechar, rodada B) e
    /// "externo" (feito fora do sistema, no PDTIC registrado externamente), com o Motivo em
    /// texto. "continuo" fica para o tipo fluxo e, antes de o carregador trazer a versão 6 do
    /// modelo inicial, para o monitoramento. Desde a F3, "opcional": o passo opcional para o
    /// órgão e ainda sem conteúdo no PDTIC, que não é cobrado (onde antes ficaria pendente ou
    /// atrasado), nos dois modos dos níveis.
    /// </summary>
    public static class SituacaoPasso
    {
        public const string Feito = "feito";
        public const string Pendente = "pendente";
        // Há comentário aberto no passo (ou o CGTIC devolveu, no passo da deliberação)
        public const string Atencao = "atencao";
        public const string NaoSeAplica = "nao_se_aplica";
        public const string Continuo = "continuo";
        public const string Aguardando = "aguardando";
        public const string Atrasado = "atrasado";
        public const string Externo = "externo";
        // F3: passo opcional sem conteúdo (sem registro em nenhuma seção dele; no documento, sem PDF gerado)
        public const string Opcional = "opcional";

        public static readonly string[] Todas = { Feito, Pendente, Atencao, NaoSeAplica, Continuo, Aguardando, Atrasado, Externo, Opcional };
    }

    /// <summary>
    /// Como os órgãos usam os níveis de maturidade (pe_configuracao.modo_niveis, desde a F3):
    /// "livre" (cada órgão começa pelo mínimo do decreto, o nível base, vê os outros passos do
    /// guia como opcionais, escolhe a forma de cada passo e o nível sai no fim, pelo que o PDTIC
    /// tem) ou "definido" (o administrador escolhe o nível de cada órgão, como até a F2). Sem a
    /// configuração, ou antes de o carregador trazer a versão 8 do modelo inicial, vale o definido.
    /// </summary>
    public static class ModoNiveis
    {
        public const string Livre = "livre";
        public const string Definido = "definido";

        public static readonly string[] Todos = { Livre, Definido };
    }

    /// <summary>
    /// As etapas da trilha do PDTIC pela chave (itens do sistema: a chave não muda e o
    /// administrador não cria etapa). As três primeiras são a elaboração (editáveis em
    /// elaboração ou devolvido); as quatro últimas, o acompanhamento (editáveis depois da
    /// publicação). Etapa com chave desconhecida conta como elaboração.
    /// </summary>
    public static class EtapaPdtic
    {
        public const string Preparacao = "preparacao";
        public const string Diagnostico = "diagnostico";
        public const string Planejamento = "planejamento";
        public const string PlanoAcompanhamento = "plano-acompanhamento";
        public const string Monitoramento = "monitoramento";
        public const string AvaliacaoIntermediaria = "avaliacao-intermediaria";
        public const string Fechamento = "fechamento";

        public static readonly string[] Elaboracao = { Preparacao, Diagnostico, Planejamento };

        public static readonly string[] Acompanhamento = { PlanoAcompanhamento, Monitoramento, AvaliacaoIntermediaria, Fechamento };

        public static bool EhDoAcompanhamento(string? chave) => chave != null && Acompanhamento.Contains(chave);
    }

    /// <summary>
    /// Decisão das seções de aprovação (campo "decisao" dos passos do tipo aprovação e da
    /// aprovação do SGTIC): aprovado ou devolvido; na avaliação do comitê (6.3), seguir ou
    /// revisar (as duas contam como decisão tomada).
    /// </summary>
    public static class Decisao
    {
        public const string Aprovado = "aprovado";
        public const string Devolvido = "devolvido";
        public const string Seguir = "seguir";
        public const string Revisar = "revisar";

        // O passo de aprovação fica feito com uma destas
        public static readonly string[] Tomadas = { Aprovado, Seguir, Revisar };
    }

    /// <summary>
    /// Quem aprovou o PDTIC registrado fora do sistema (POST pdtic/registrar-externo): o CGTIC
    /// (vira uma deliberação aprovada, com o ato) ou outra instância (vai para a seção da
    /// aprovação do SGTIC, com a instância "outra").
    /// </summary>
    public static class InstanciaAprovacaoExterna
    {
        public const string Cgtic = "cgtic";
        public const string Outra = "outra";

        public static readonly string[] Todas = { Cgtic, Outra };
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

        // Princípios e diretrizes do PDTIC (passo 1.8): os princípios do art. 4º entram como
        // sugestão quando o PDTIC é aberto (F2), com a origem "art4"
        public const string SecaoPrincipiosDiretrizes = "principios_diretrizes";
        public const string CampoPrincipio = "principio";
        public const string CampoOrigem = "origem";
        public const string CampoFonte = "fonte";
        public const string CampoCriterioPriorizacao = "criterio_priorizacao";
        public const string OrigemArt4 = "art4";

        // Aprovação e publicação (E7): as seções formulário que a E2 semeou e os campos delas
        public const string SecaoAprovacaoSgtic = "aprovacao_sgtic";
        public const string SecaoPublicacao = "publicacao";
        public const string SecaoAprovacaoPlanoAcompanhamento = "aprovacao_plano_acompanhamento";
        public const string SecaoAvaliacaoComite = "avaliacao_comite";
        public const string SecaoAprovacaoAutoridade = "aprovacao_resultados_autoridade";
        public const string CampoDecisao = "decisao";
        public const string CampoData = "data";
        public const string CampoInstancia = "instancia";
        public const string CampoAtoTipo = "ato_tipo";
        public const string CampoAtoNumero = "ato_numero";
        public const string CampoSei = "sei";
        public const string CampoObservacao = "observacao";
        public const string CampoEndereco = "endereco";
        public const string InstanciaOutra = "outra";
        public const string AtoTipoOutro = "outro";

        // Passos que o PDTIC registrado fora do sistema preenche para acompanhar (3.3 e 3.9)
        public const string PassoMetasAcoes = "planejamento.metas-acoes";
        public const string PassoRiscos = "planejamento.riscos";
        // A avaliação do comitê (6.3): a decisão "revisar" libera a revisão
        public const string PassoAvaliacaoComite = "avaliacao-intermediaria.avaliacao-comite";
        // A metodologia de elaboração (1.5): mostra os fluxos do órgão; gravar ou restaurar um fluxo
        // é mudança no conteúdo dela (F3, a validação pela equipe)
        public const string PassoMetodologia = "preparacao.metodologia";
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
    /// Formato de um campo de texto curto (config "formato", desde a F1): o número de um
    /// processo SEI (00000-00000000/0000-00) ou um endereço de internet completo (http ou https).
    /// </summary>
    public static class FormatoTexto
    {
        public const string Sei = "sei";
        public const string Url = "url";

        public static readonly string[] Todos = { Sei, Url };
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
        // Fluxo do guia (modelo, E6)
        public const string FluxoModelo = "fluxo_modelo";
        // Uma configuração geral do módulo (F3: o modo dos níveis; entidade_id = 0, a chave vai no antes e no depois)
        public const string Configuracao = "configuracao";

        public static readonly string[] Todas =
            { Nivel, Etapa, Passo, Secao, Campo, Opcao, OrgaoNivel, OrgaoAjuste, DocCapitulo, DocBloco, FluxoModelo, Configuracao };
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

    /// <summary>
    /// Tipo do modelo de documento (pe_doc_modelo.tipo) e do documento de cada linha da cópia do
    /// órgão e das versões (doc_tipo): o PDTIC (E5) e, desde a E7 (rodada B), o relatório de
    /// acompanhamento de um ciclo (RA, Anexo XIV do guia) e o relatório de resultados (RR, Anexo XV).
    /// </summary>
    public static class TipoDocumento
    {
        public const string Pdtic = "pdtic";
        public const string Ra = "ra";
        public const string Rr = "rr";

        public static readonly string[] Todos = { Pdtic, Ra, Rr };

        /// <summary>O título do documento, como sai na capa e na prévia.</summary>
        public static string Titulo(string tipo) => tipo switch
        {
            Pdtic => "Plano Diretor de Tecnologia da Informação e Comunicação",
            Ra => "Relatório de Acompanhamento do PDTIC",
            Rr => "Relatório de Resultados do PDTIC",
            _ => tipo
        };

        /// <summary>O nome curto do documento, no rodapé e nas mensagens.</summary>
        public static string NomeCurto(string tipo) => tipo switch
        {
            Ra => "Relatório de acompanhamento",
            Rr => "Relatório de resultados",
            _ => "PDTIC"
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
        // Os blocos de dados do acompanhamento (E7, rodada B), para os relatórios RA e RR
        public const string AcoesPorSituacao = "acoes_por_situacao";
        public const string MetasPorResultado = "metas_por_resultado";
        public const string RiscosOcorridos = "riscos_ocorridos";
        public const string Medicoes = "medicoes";

        public static readonly string[] Todos =
        {
            Texto, TabelaSecao, ListaTema, MatrizSwot, Fluxo, QuebraPagina, AcoesPorSituacao, MetasPorResultado, RiscosOcorridos, Medicoes
        };

        /// <summary>Os blocos do acompanhamento: saem em grupos de tabelas (Grupos) e não têm configuração além da página deitada.</summary>
        public static readonly string[] DoAcompanhamento = { AcoesPorSituacao, MetasPorResultado, RiscosOcorridos, Medicoes };
    }

    /// <summary>
    /// Situação de uma versão gerada do documento (pe_doc_versao.situacao). A E5 gera minutas;
    /// a E7 gera a enviada ao CGTIC no envio e marca a mesma versão como aprovada (decisão do
    /// CGTIC) e depois como publicada (o PDF não muda). O PDTIC registrado fora do sistema
    /// guarda o PDF enviado como a versão 1, já publicada.
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

    // ── Fluxos (E6) ────────────────────────────────────────────────────────────

    /// <summary>
    /// Tipo de um elemento de fluxo (a definição em jsonb de pe_fluxo_modelo e pe_fluxo): início
    /// e fim, ligação com outro fluxo (evento de enlace do BPMN), tarefa, subprocesso, decisão
    /// (gateway exclusivo, com "X") e paralelo (gateway paralelo, com "+"). Tarefa e subprocesso
    /// recebem o número automático (prefixo do fluxo e a ordem no desenho).
    /// </summary>
    public static class TipoElementoFluxo
    {
        public const string Inicio = "inicio";
        public const string Fim = "fim";
        public const string Ligacao = "ligacao";
        public const string Tarefa = "tarefa";
        public const string Subprocesso = "subprocesso";
        public const string Decisao = "decisao";
        public const string Paralelo = "paralelo";

        public static readonly string[] Todos = { Inicio, Fim, Ligacao, Tarefa, Subprocesso, Decisao, Paralelo };

        /// <summary>Recebe número automático e artefatos.</summary>
        public static bool EhAtividade(string? tipo) => tipo is Tarefa or Subprocesso;

        /// <summary>Losango (decisão ou paralelo).</summary>
        public static bool EhPorta(string? tipo) => tipo is Decisao or Paralelo;

        /// <summary>Círculo (início, fim ou ligação com outro fluxo).</summary>
        public static bool EhEvento(string? tipo) => tipo is Inicio or Fim or Ligacao;
    }

    /// <summary>
    /// Os fluxos do guia que o carregador semeia (figuras 4 a 22) e os que viram o cronograma
    /// sugerido do plano de trabalho (preparação, diagnóstico e planejamento, na ordem).
    /// </summary>
    public static class FluxoGuia
    {
        public const string Macroprocesso = "macroprocesso";
        public const string Elaboracao = "elaboracao";
        public const string Preparacao = "preparacao";
        public const string Diagnostico = "diagnostico";
        public const string Planejamento = "planejamento";
        public const string Acompanhamento = "acompanhamento";
        public const string PlanejamentoAcompanhamento = "planejamento_acompanhamento";
        public const string Monitoramento = "monitoramento";
        public const string AvaliacaoIntermediaria = "avaliacao_intermediaria";
        public const string AvaliacaoFinal = "avaliacao_final";

        // As tarefas destes fluxos viram as linhas sugeridas do cronograma (passo do plano de trabalho)
        public static readonly string[] DoCronograma = { Preparacao, Diagnostico, Planejamento };

        // A seção do cronograma do plano de trabalho e os campos que a sugestão preenche
        public const string SecaoCronograma = "cronograma_elaboracao";
        public const string CampoAtividade = "atividade";
        public const string CampoResponsavel = "responsavel";
        public const string CampoPredecessoras = "predecessoras";
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

    // ── Acompanhamento (E7, rodada B) ──────────────────────────────────────────

    /// <summary>
    /// Tipo do ciclo (pe_ciclo.tipo) e das seções por ciclo (pe_secao.por_ciclo): o ciclo de
    /// monitoramento (criado sozinho pela periodicidade, depois da publicação) e a avaliação
    /// intermediária (aberta pela equipe do órgão quando o comitê pede).
    /// </summary>
    public static class TipoCiclo
    {
        public const string Monitoramento = "monitoramento";
        public const string Avaliacao = "avaliacao";

        public static readonly string[] Todos = { Monitoramento, Avaliacao };
    }

    /// <summary>Situação guardada do ciclo (pe_ciclo.situacao; token de concorrência).</summary>
    public static class SituacaoCiclo
    {
        public const string Aberto = "aberto";
        public const string Fechado = "fechado";

        public static readonly string[] Todas = { Aberto, Fechado };
    }

    /// <summary>
    /// A situação que a tela mostra, calculada: futuro (ainda não começou; não aceita dados),
    /// aberto, atrasado (o prazo de fechamento venceu sem fechar) e fechado.
    /// </summary>
    public static class SituacaoCicloExibida
    {
        public const string Futuro = "futuro";
        public const string Aberto = "aberto";
        public const string Atrasado = "atrasado";
        public const string Fechado = "fechado";

        public static readonly string[] Todas = { Futuro, Aberto, Atrasado, Fechado };
    }

    /// <summary>
    /// Periodicidade do monitoramento (opções do campo periodicidade da seção
    /// periodicidade_monitoramento, passo 4.3, e o padrão em pe_configuracao): quantos meses tem
    /// cada ciclo. Os ciclos seguem o calendário (1º trimestre = janeiro a março).
    /// </summary>
    public static class Periodicidade
    {
        public const string Mensal = "mensal";
        public const string Bimestral = "bimestral";
        public const string Trimestral = "trimestral";
        public const string Semestral = "semestral";
        public const string Anual = "anual";

        public const string Padrao = Trimestral;

        /// <summary>Meses de cada ciclo, ou nulo para um valor que o módulo não conhece.</summary>
        public static int? Meses(string? valor) => valor switch
        {
            Mensal => 1,
            Bimestral => 2,
            Trimestral => 3,
            Semestral => 6,
            Anual => 12,
            _ => null
        };
    }

    /// <summary>
    /// Situação física de uma ação no painel do PDTIC (AC-PDTIC, Anexo XIII) e nos grupos do
    /// relatório de acompanhamento: pela situação registrada no ciclo e pelas datas previstas.
    /// </summary>
    public static class SituacaoFisica
    {
        public const string EmDia = "em_dia";
        public const string Atrasada = "atrasada";
        public const string Concluida = "concluida";
        public const string Cancelada = "cancelada";
        public const string SemRegistro = "sem_registro";

        public static readonly string[] Todas = { EmDia, Atrasada, Concluida, Cancelada, SemRegistro };
    }

    /// <summary>
    /// Chaves das seções, dos campos e dos passos do acompanhamento que o código usa (itens do
    /// sistema, semeados na E2: não mudam de chave nem de tipo). As seções por ciclo são marcadas
    /// pelo carregador (versão 6): o monitoramento em 5.1 e 5.2 e a avaliação em 6.1 a 6.3.
    /// </summary>
    public static class ChaveAcompanhamento
    {
        // Passos da etapa 5 (monitoramento): a grade do ciclo (5.1) e o fechamento (5.2)
        public const string PassoCicloMonitoramento = "monitoramento.ciclo-monitoramento";
        public const string PassoRelatorioAcompanhamento = "monitoramento.relatorio-acompanhamento";

        // Plano de acompanhamento (etapa 4)
        public const string SecaoPeriodicidade = "periodicidade_monitoramento";
        public const string CampoPeriodicidade = "periodicidade";
        public const string SecaoIndicadoresMonitoramento = "indicadores_monitoramento";
        public const string CampoIndicador = "indicador";
        public const string CampoValoresReferencia = "valores_referencia";
        public const string SecaoProjetos = "projetos";
        public const string CampoAcao = "acao";
        public const string CampoPesoDaAcaoNaMeta = "peso_da_acao_na_meta";

        // Monitoramento (por ciclo de monitoramento)
        public const string SecaoMonitoramentoAcoes = "monitoramento_acoes";
        public const string CampoSituacao = "situacao";
        public const string CampoExecucaoFisica = "execucao_fisica";
        public const string CampoExecucaoOrcamentaria = "execucao_orcamentaria";
        public const string CampoObservacao = "observacao";
        public const string SecaoMedicoes = "medicoes_indicadores";
        public const string CampoValorApurado = "valor_apurado";
        public const string CampoData = "data";
        public const string SecaoRiscosOcorridos = "riscos_ocorridos";
        public const string CampoRisco = "risco";
        public const string CampoAcoesRealizadas = "acoes_realizadas";
        public const string CampoResponsavel = "responsavel";
        public const string CampoResultado = "resultado";
        public const string SecaoRelatorioCiclo = "relatorio_ciclo";

        // Avaliação intermediária (por ciclo de avaliação) e avaliação final
        public const string SecaoResultadosIntermediarios = "resultados_intermediarios";
        public const string CampoMeta = "meta";
        public const string CampoValorAlcancado = "valor_alcancado";
        public const string SecaoAnaliseIntermediaria = "analise_intermediaria";
        public const string SecaoResultadosMetas = "resultados_metas";
        public const string CampoMotivo = "motivo";

        // Plano: metas, ações e riscos (etapa 3)
        public const string SecaoMetas = "metas";
        public const string CampoDescricao = "descricao";
        public const string CampoValorDaMeta = "valor";
        public const string CampoPrazo = "prazo";
        public const string CampoNecessidades = "necessidades";
        public const string CampoMetas = "metas";
        public const string CampoInicio = "inicio";
        public const string CampoConclusao = "conclusao";
        public const string CampoInvestimento = "investimento";
        public const string CampoCusteio = "custeio";
        public const string CampoNivelRisco = "nivel";
        public const string CampoNivelRiscoSimples = "nivel_simples";

        // Valores das listas do sistema (situação da ação e do risco, resultado da meta)
        public const string AcaoNaoIniciada = "nao_iniciada";
        public const string AcaoEmAndamento = "em_andamento";
        public const string AcaoConcluida = "concluida";
        public const string AcaoCancelada = "cancelada";
        public const string RiscoAberto = "aberto";
        public const string RiscoFechado = "fechado";
        public const string RiscoExcluido = "excluido";
        public const string SemOcorrencia = "sem_ocorrencia";
        public const string MetaAlcancada = "alcancada";
        public const string MetaNaoAlcancada = "nao_alcancada";
        public const string MetaCancelada = "cancelada";
        public const string MetaEmAndamento = "em_andamento";
        public const string NivelAlto = "alto";
        public const string NivelMedio = "medio";
        public const string NivelBaixo = "baixo";

        public static readonly string[] SituacoesDoRisco = { RiscoAberto, RiscoFechado, RiscoExcluido, SemOcorrencia };
        public static readonly string[] NiveisDoRisco = { NivelAlto, NivelMedio, NivelBaixo };
    }

    // ── Painéis da SGDI, conformidade e inadimplência (E8) ─────────────────────

    /// <summary>
    /// Situação de um registro de inadimplência (pe_inadimplencia.situacao), no caminho do art.
    /// 11 do Decreto nº 48.899/2026: a SGDI notifica o órgão (5 dias úteis para regularizar ou
    /// justificar); a justificativa aceita encerra; sem regularização nem justificativa aceita no
    /// prazo, a SGDI registra a inadimplência com o motivo; o saneamento tira a marca do painel e
    /// guarda a data. A vigente (painel, conformidade e página do órgão) é a notificada ou a
    /// inadimplente.
    /// </summary>
    public static class SituacaoInadimplencia
    {
        public const string Notificado = "notificado";
        public const string Justificado = "justificado";
        public const string Inadimplente = "inadimplente";
        public const string Saneado = "saneado";

        public static readonly string[] Todas = { Notificado, Justificado, Inadimplente, Saneado };

        // A marca que o painel, a conformidade e a página do órgão mostram
        public static readonly string[] Vigentes = { Notificado, Inadimplente };

        public static string Rotulo(string situacao) => situacao switch
        {
            Notificado => "Notificado",
            Justificado => "Justificado",
            Inadimplente => "Inadimplente",
            Saneado => "Saneado",
            _ => situacao
        };
    }

    /// <summary>O motivo da inadimplência registrada (art. 11, III, do Decreto nº 48.899/2026).</summary>
    public static class MotivoInadimplencia
    {
        public const string DescumprimentoPrazo = "descumprimento_prazo";
        public const string OmissaoReiterada = "omissao_reiterada";
        public const string RecusaInjustificada = "recusa_injustificada";

        public static readonly string[] Todos = { DescumprimentoPrazo, OmissaoReiterada, RecusaInjustificada };

        public static string Rotulo(string motivo) => motivo switch
        {
            DescumprimentoPrazo => "Descumprimento de prazo",
            OmissaoReiterada => "Omissão reiterada",
            RecusaInjustificada => "Recusa injustificada de comunicação",
            _ => motivo
        };
    }

    /// <summary>
    /// Os itens de conformidade da SGDI (só itens de TIC, decisão 4 do plano), na ordem da tela e
    /// da planilha, com o rótulo e a base legal.
    /// </summary>
    public static class ItemConformidade
    {
        public const string AprovadoCgtic = "aprovado_cgtic";
        public const string ComunicadoSgdi = "comunicado_sgdi";
        public const string Vigente = "vigente";
        public const string NoveConteudos = "nove_conteudos";
        public const string RevisaoEmDia = "revisao_em_dia";
        public const string AcompanhamentoEmDia = "acompanhamento_em_dia";

        public sealed record Item(string Chave, string Rotulo, string Base, string AtendeQuando);

        public static readonly IReadOnlyList<Item> Todos = new[]
        {
            new Item(AprovadoCgtic, "PDTIC aprovado pelo CGTIC", "art. 5º do Decreto nº 48.900/2026",
                "Há deliberação do CGTIC que aprova a versão do PDTIC."),
            new Item(ComunicadoSgdi, "PDTIC comunicado à SGDI", "art. 7º, V, do Decreto nº 48.899/2026",
                "A versão foi enviada pelo sistema ou registrada fora dele com a aprovação."),
            new Item(Vigente, "PDTIC vigente", "art. 12 do Decreto nº 48.900/2026",
                "O PDTIC está publicado ou em acompanhamento e hoje está dentro da vigência."),
            new Item(NoveConteudos, "Os nove conteúdos preenchidos", "art. 12, § 2º, do Decreto nº 48.900/2026",
                "Os passos dos nove conteúdos mínimos estão feitos."),
            new Item(RevisaoEmDia, "Revisão em dia", "art. 12 do Decreto nº 48.900/2026",
                "A última aprovação do CGTIC (no PDTIC registrado fora do sistema, a publicação) está dentro da periodicidade de revisão do passo da abrangência (anual por padrão)."),
            new Item(AcompanhamentoEmDia, "Acompanhamento em dia", "Guia de PDTIC do SISP, capítulo 7",
                "Nenhum ciclo de monitoramento passou do prazo de fechamento. Antes da publicação, o item não se aplica.")
        };
    }

    /// <summary>
    /// Grupo de conformidade pelo percentual de itens atendidos entre os que se aplicam: alta a
    /// partir de 70%, média de 25% a 69% e baixa abaixo de 25% (o órgão sem PDTIC fica em baixa).
    /// </summary>
    public static class GrupoConformidade
    {
        public const string Alta = "alta";
        public const string Media = "media";
        public const string Baixa = "baixa";

        public static readonly string[] Todos = { Alta, Media, Baixa };

        public static string De(int percentual) => percentual >= 70 ? Alta : percentual >= 25 ? Media : Baixa;

        public static string Rotulo(string grupo) => grupo switch
        {
            Alta => "Alta",
            Media => "Média",
            Baixa => "Baixa",
            _ => grupo
        };
    }

    /// <summary>
    /// Os alertas que a conformidade filtra (GET conformidade?Alerta=; F2): os três do painel que
    /// levam à conformidade, pelas marcas da linha que o painel conta, e a notificação ou
    /// inadimplência em aberto (a linha tem Inadimplencia). Os valores são os da tela (?alerta=).
    /// </summary>
    public static class AlertaConformidade
    {
        public const string Vigencia = "vigencia";
        public const string Revisao = "revisao";
        public const string Ciclo = "ciclo";
        public const string Inadimplencia = "inadimplencia";

        public static readonly string[] Todos = { Vigencia, Revisao, Ciclo, Inadimplencia };

        public static string Rotulo(string alerta) => alerta switch
        {
            Vigencia => "PDTIC publicado com a vigência vencida",
            Revisao => "Revisão do PDTIC vencida",
            Ciclo => "Ciclo de monitoramento atrasado",
            Inadimplencia => "Notificação ou inadimplência em aberto",
            _ => alerta
        };
    }

    /// <summary>
    /// A situação de cada órgão no painel da SGDI: a do PDTIC de referência (a versão vigente;
    /// sem ela, a versão da elaboração; sem as duas, a mais recente, encerrada) ou sem PDTIC.
    /// </summary>
    public static class SituacaoPainel
    {
        public const string SemPdtic = "sem_pdtic";

        public static readonly string[] Todas =
        {
            SemPdtic, SituacaoPdtic.EmElaboracao, SituacaoPdtic.EmAprovacao, SituacaoPdtic.Devolvido, SituacaoPdtic.Aprovado,
            SituacaoPdtic.Publicado, SituacaoPdtic.EmAcompanhamento, SituacaoPdtic.Encerrado
        };

        public static string Rotulo(string chave) => chave == SemPdtic ? "Sem PDTIC" : SituacaoPdtic.Rotulo(chave);
    }
}
