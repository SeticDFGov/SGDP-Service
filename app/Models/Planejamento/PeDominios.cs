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
}
