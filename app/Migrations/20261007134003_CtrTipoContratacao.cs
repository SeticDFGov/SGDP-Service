using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class CtrTipoContratacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo_contratacao",
                table: "ctr_processo",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_tipo_contratacao",
                table: "ctr_processo",
                sql: "tipo_contratacao IS NULL OR tipo_contratacao IN ('Nova contratação','Alteração de contratação vigente')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_tipo_contratacao",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "tipo_contratacao",
                table: "ctr_processo");
        }
    }
}
