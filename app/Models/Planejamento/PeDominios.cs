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
        /// pgia_sistema não é feito de registros (chega com o PDTIC, na E4).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> SecaoDoCatalogo = new Dictionary<string, string>
        {
            [PeticObjetivo] = "petic_objetivo",
            [PeticEixo] = "petic_eixo",
            [Principio] = "principio"
        };

        /// <summary>Catálogo que sai do PETIC-DF vigente (sem vigente, a ligação fica opcional).</summary>
        public static bool DoPetic(string? catalogo) => catalogo is PeticObjetivo or PeticEixo;
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
    /// Dono dos registros: o catálogo do DF (escopo df, sem dono) ou uma versão do PETIC-DF
    /// (escopo petic). A E4 acrescenta o PDTIC de cada órgão.
    /// </summary>
    public static class DonoRegistro
    {
        public const string Df = "df";
        public const string Petic = "petic";

        public static readonly string[] Todos = { Df, Petic };
    }

    /// <summary>
    /// Dono de um arquivo (pe_arquivo.dono_tipo): o registro cujo campo aponta para ele.
    /// Nulo = recém-enviado, ainda sem dono (só quem enviou vê).
    /// </summary>
    public static class DonoArquivo
    {
        public const string Registro = "registro";

        public static readonly string[] Todos = { Registro };
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

        public static readonly string[] Todas = { Nivel, Etapa, Passo, Secao, Campo, Opcao, OrgaoNivel, OrgaoAjuste };
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
}
