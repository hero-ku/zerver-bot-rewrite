using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace zerver_bot_rewrite.Migrations
{
    /// <inheritdoc />
    public partial class CreateTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    InteractionId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SenderId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RecipientId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.InteractionId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transactions");
        }
    }
}
