using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace zerver_bot_rewrite.Migrations
{
    /// <inheritdoc />
    public partial class CreateElectionVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElectionVotes",
                columns: table => new
                {
                    VoterId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TargetId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionVotes", x => x.VoterId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElectionVotes");
        }
    }
}
