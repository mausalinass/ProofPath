using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProofPath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreGitHubConnectionStateServerSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectedPayload",
                table: "GitHubConnectionAttempts",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectedPayload",
                table: "GitHubConnectionAttempts");
        }
    }
}
