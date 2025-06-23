using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class deleteColUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UNIDADE",
                table: "Projetos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UNIDADE",
                table: "Projetos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }
    }
}
