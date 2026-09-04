using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PgiaAnexosDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "orgao_id",
                table: "pgia_documento",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "content_type",
                table: "pgia_documento",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "tamanho_bytes",
                table: "pgia_documento",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pgia_documento_arquivo",
                columns: table => new
                {
                    documento_id = table.Column<long>(type: "bigint", nullable: false),
                    conteudo = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_documento_arquivo", x => x.documento_id);
                    table.ForeignKey(
                        name: "fk_pgia_documento_arquivo_documento",
                        column: x => x.documento_id,
                        principalTable: "pgia_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pgia_documento_arquivo");

            migrationBuilder.DropColumn(
                name: "content_type",
                table: "pgia_documento");

            migrationBuilder.DropColumn(
                name: "tamanho_bytes",
                table: "pgia_documento");

            migrationBuilder.AlterColumn<long>(
                name: "orgao_id",
                table: "pgia_documento",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
