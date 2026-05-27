using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDatesToDateOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"Etapas\" ALTER COLUMN \"DT_INICIO\" TYPE date USING \"DT_INICIO\"::date;");

            migrationBuilder.Sql(
                "ALTER TABLE \"Etapas\" ALTER COLUMN \"DT_FIM\" TYPE date USING \"DT_FIM\"::date;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"Etapas\" ALTER COLUMN \"DT_INICIO\" TYPE timestamp with time zone USING \"DT_INICIO\"::timestamp with time zone;");

            migrationBuilder.Sql(
                "ALTER TABLE \"Etapas\" ALTER COLUMN \"DT_FIM\" TYPE timestamp with time zone USING \"DT_FIM\"::timestamp with time zone;");
        }
    }
}
