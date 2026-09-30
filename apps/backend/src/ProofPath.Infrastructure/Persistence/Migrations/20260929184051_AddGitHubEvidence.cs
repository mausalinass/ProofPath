using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProofPath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GitHubInstallations_ConnectedAccountId",
                table: "GitHubInstallations");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResumeExtractionId",
                table: "Projects",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "RepositoryId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ResumeExtractionId",
                table: "Experiences",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResumeExtractionId",
                table: "EvidenceItems",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Detector",
                table: "EvidenceItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectorVersion",
                table: "EvidenceItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EndLine",
                table: "EvidenceItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceType",
                table: "EvidenceItems",
                type: "text",
                nullable: false,
                defaultValue: "Presence");

            migrationBuilder.AddColumn<decimal>(
                name: "ExtractionConfidence",
                table: "EvidenceItems",
                type: "numeric(4,3)",
                precision: 4,
                scale: 3,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "Lifecycle",
                table: "EvidenceItems",
                type: "text",
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "EvidenceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RepositoryAnalysisId",
                table: "EvidenceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevisionSha",
                table: "EvidenceItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePath",
                table: "EvidenceItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartLine",
                table: "EvidenceItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ResumeExtractionId",
                table: "Educations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResumeExtractionId",
                table: "Credentials",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "GitHubConnectionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    NonceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubConnectionAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubConnectionAttempts_CandidateProfiles_CandidateProfile~",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectedAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    GitHubId = table.Column<long>(type: "bigint", nullable: false),
                    Owner = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FullName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Private = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultBranch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    HtmlUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    IncludedForAnalysis = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastScanAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRevisionSha = table.Column<string>(type: "text", nullable: true),
                    ScanStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CoverageJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repositories_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Repositories_ConnectedAccounts_ConnectedAccountId",
                        column: x => x.ConnectedAccountId,
                        principalTable: "ConnectedAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExtractionVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AnalysisPolicyVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScanIdentity = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CoverageJson = table.Column<string>(type: "jsonb", nullable: false),
                    WarningsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryAnalyses_AnalysisJobs_AnalysisJobId",
                        column: x => x.AnalysisJobId,
                        principalTable: "AnalysisJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryAnalyses_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_RepositoryId",
                table: "Projects",
                column: "RepositoryId",
                unique: true,
                filter: "\"RepositoryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubInstallations_ConnectedAccountId",
                table: "GitHubInstallations",
                column: "ConnectedAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceItems_ProjectId",
                table: "EvidenceItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceItems_RepositoryAnalysisId_SkillId_SourcePath_Detec~",
                table: "EvidenceItems",
                columns: new[] { "RepositoryAnalysisId", "SkillId", "SourcePath", "Detector" },
                unique: true,
                filter: "\"RepositoryAnalysisId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubConnectionAttempts_CandidateProfileId",
                table: "GitHubConnectionAttempts",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubConnectionAttempts_ExpiresAt",
                table: "GitHubConnectionAttempts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubConnectionAttempts_NonceHash",
                table: "GitHubConnectionAttempts",
                column: "NonceHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_CandidateProfileId_GitHubId",
                table: "Repositories",
                columns: new[] { "CandidateProfileId", "GitHubId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_CandidateProfileId_IncludedForAnalysis",
                table: "Repositories",
                columns: new[] { "CandidateProfileId", "IncludedForAnalysis" });

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_ConnectedAccountId",
                table: "Repositories",
                column: "ConnectedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryAnalyses_AnalysisJobId",
                table: "RepositoryAnalyses",
                column: "AnalysisJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryAnalyses_RepositoryId",
                table: "RepositoryAnalyses",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryAnalyses_ScanIdentity",
                table: "RepositoryAnalyses",
                column: "ScanIdentity",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EvidenceItems_Projects_ProjectId",
                table: "EvidenceItems",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EvidenceItems_RepositoryAnalyses_RepositoryAnalysisId",
                table: "EvidenceItems",
                column: "RepositoryAnalysisId",
                principalTable: "RepositoryAnalyses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Repositories_RepositoryId",
                table: "Projects",
                column: "RepositoryId",
                principalTable: "Repositories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EvidenceItems_Projects_ProjectId",
                table: "EvidenceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_EvidenceItems_RepositoryAnalyses_RepositoryAnalysisId",
                table: "EvidenceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Repositories_RepositoryId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "GitHubConnectionAttempts");

            migrationBuilder.DropTable(
                name: "RepositoryAnalyses");

            migrationBuilder.DropTable(
                name: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Projects_RepositoryId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_GitHubInstallations_ConnectedAccountId",
                table: "GitHubInstallations");

            migrationBuilder.DropIndex(
                name: "IX_EvidenceItems_ProjectId",
                table: "EvidenceItems");

            migrationBuilder.DropIndex(
                name: "IX_EvidenceItems_RepositoryAnalysisId_SkillId_SourcePath_Detec~",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "RepositoryId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Detector",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "DetectorVersion",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "EndLine",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "EvidenceType",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "ExtractionConfidence",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "Lifecycle",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "RepositoryAnalysisId",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "RevisionSha",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "SourcePath",
                table: "EvidenceItems");

            migrationBuilder.DropColumn(
                name: "StartLine",
                table: "EvidenceItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResumeExtractionId",
                table: "Projects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ResumeExtractionId",
                table: "Experiences",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ResumeExtractionId",
                table: "EvidenceItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ResumeExtractionId",
                table: "Educations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ResumeExtractionId",
                table: "Credentials",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubInstallations_ConnectedAccountId",
                table: "GitHubInstallations",
                column: "ConnectedAccountId");
        }
    }
}
