using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProofPath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VerifyGitHubInstallationOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GitHubInstallations_InstallationId",
                table: "GitHubInstallations");

            migrationBuilder.DropIndex(
                name: "IX_ConnectedAccounts_Provider_ExternalAccountId",
                table: "ConnectedAccounts");

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnershipVerifiedAt",
                table: "ConnectedAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubInstallations_InstallationId",
                table: "GitHubInstallations",
                column: "InstallationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedAccounts_Provider_ExternalAccountId",
                table: "ConnectedAccounts",
                columns: new[] { "Provider", "ExternalAccountId" },
                unique: true,
                filter: "\"OwnershipVerifiedAt\" IS NOT NULL");

            migrationBuilder.Sql("""
                UPDATE "EvidenceItems" AS evidence SET "Lifecycle" = 'Quarantined'
                FROM "RepositoryAnalyses" AS analysis
                JOIN "Repositories" AS repository ON repository."Id" = analysis."RepositoryId"
                JOIN "ConnectedAccounts" AS account ON account."Id" = repository."ConnectedAccountId"
                WHERE evidence."RepositoryAnalysisId" = analysis."Id"
                  AND account."Provider" = 'GitHub' AND account."OwnershipVerifiedAt" IS NULL;

                UPDATE "Repositories" AS repository
                SET "IncludedForAnalysis" = FALSE, "ScanStatus" = 'AccessLost'
                FROM "ConnectedAccounts" AS account
                WHERE repository."ConnectedAccountId" = account."Id"
                  AND account."Provider" = 'GitHub' AND account."OwnershipVerifiedAt" IS NULL;

                UPDATE "ConnectedAccounts" SET "Status" = 'Revoked'
                WHERE "Provider" = 'GitHub' AND "OwnershipVerifiedAt" IS NULL AND "Status" = 'Active';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GitHubInstallations_InstallationId",
                table: "GitHubInstallations");

            migrationBuilder.DropIndex(
                name: "IX_ConnectedAccounts_Provider_ExternalAccountId",
                table: "ConnectedAccounts");

            migrationBuilder.DropColumn(
                name: "OwnershipVerifiedAt",
                table: "ConnectedAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubInstallations_InstallationId",
                table: "GitHubInstallations",
                column: "InstallationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedAccounts_Provider_ExternalAccountId",
                table: "ConnectedAccounts",
                columns: new[] { "Provider", "ExternalAccountId" },
                unique: true);
        }
    }
}
