using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProofPath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnectedAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ExternalLogin = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisconnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectedAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectedAccounts_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GitHubInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectedAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<long>(type: "bigint", nullable: false),
                    TargetAccountId = table.Column<long>(type: "bigint", nullable: false),
                    TargetLogin = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RepositorySelection = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PermissionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubInstallations_ConnectedAccounts_ConnectedAccountId",
                        column: x => x.ConnectedAccountId,
                        principalTable: "ConnectedAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedAccounts_CandidateProfileId_Provider",
                table: "ConnectedAccounts",
                columns: new[] { "CandidateProfileId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedAccounts_Provider_ExternalAccountId",
                table: "ConnectedAccounts",
                columns: new[] { "Provider", "ExternalAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubInstallations_ConnectedAccountId",
                table: "GitHubInstallations",
                column: "ConnectedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubInstallations_InstallationId",
                table: "GitHubInstallations",
                column: "InstallationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitHubInstallations");

            migrationBuilder.DropTable(
                name: "ConnectedAccounts");
        }
    }
}
