using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class CtrCategoriaServicosTic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_categoria",
                table: "ctr_processo");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_categoria",
                table: "ctr_processo",
                sql: "categoria_objeto IN ('Infraestrutura de Rede','Desenvolvimento / Fábrica de Software','Outsourcing de Impressão','Licenciamento de Software','Inteligência Artificial','Segurança Cibernética','Sistemas de Gestão','Certificação Digital','Captação Audiovisual','Telefonia / VoIP','Serviços de TIC','Sem objeto / Indefinido','Outros')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_categoria",
                table: "ctr_processo");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_categoria",
                table: "ctr_processo",
                sql: "categoria_objeto IN ('Infraestrutura de Rede','Desenvolvimento / Fábrica de Software','Outsourcing de Impressão','Licenciamento de Software','Inteligência Artificial','Segurança Cibernética','Sistemas de Gestão','Certificação Digital','Captação Audiovisual','Telefonia / VoIP','Sem objeto / Indefinido','Outros')");
        }
    }
}
