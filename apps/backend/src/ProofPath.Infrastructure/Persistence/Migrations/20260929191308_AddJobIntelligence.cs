using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ProofPath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Company = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Description = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DescriptionVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AnalysisJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobRequirementExtractions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    DescriptionVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    MachineJson = table.Column<string>(type: "jsonb", nullable: false),
                    DraftJson = table.Column<string>(type: "jsonb", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    SchemaVersion = table.Column<string>(type: "text", nullable: false),
                    ExtractionVersion = table.Column<string>(type: "text", nullable: false),
                    NormalizationPolicyVersion = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Outdated = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequirementExtractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobRequirementExtractions_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequirementSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobRequirementExtractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementSets_JobRequirementExtractions_JobRequirementExt~",
                        column: x => x.JobRequirementExtractionId,
                        principalTable: "JobRequirementExtractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequirementSets_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Importance = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OriginalWording = table.Column<string>(type: "text", nullable: false),
                    SkillTerm = table.Column<string>(type: "text", nullable: true),
                    SkillId = table.Column<string>(type: "text", nullable: true),
                    NormalizationStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    BehavioralThemeKey = table.Column<string>(type: "text", nullable: true),
                    QualifiersJson = table.Column<string>(type: "jsonb", nullable: false),
                    GroupKey = table.Column<string>(type: "text", nullable: true),
                    GroupType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceBlockId = table.Column<string>(type: "text", nullable: false),
                    Quote = table.Column<string>(type: "text", nullable: false),
                    IsEvaluable = table.Column<bool>(type: "boolean", nullable: false),
                    IsScoreEligible = table.Column<bool>(type: "boolean", nullable: false),
                    UserCorrected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobRequirements_RequirementSets_RequirementSetId",
                        column: x => x.RequirementSetId,
                        principalTable: "RequirementSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobRequirements_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "SkillAliases",
                columns: new[] { "Alias", "SkillId" },
                values: new object[,]
                {
                    { ".NET WEB API", "aspnet-core" },
                    { "ASP.NET WEB API", "aspnet-core" }
                });

            migrationBuilder.InsertData(
                table: "Skills",
                columns: new[] { "Id", "DisplayName" },
                values: new object[,]
                {
                    { "authentication-authorization", "Authentication & Authorization" },
                    { "automated-testing", "Automated Testing" },
                    { "aws", "AWS" },
                    { "ci-cd", "CI/CD" },
                    { "dependency-injection", "Dependency Injection" },
                    { "express", "Express" },
                    { "java", "Java" },
                    { "nodejs", "Node.js" },
                    { "python", "Python" },
                    { "rest-api", "REST APIs" }
                });

            migrationBuilder.InsertData(
                table: "SkillAliases",
                columns: new[] { "Alias", "SkillId" },
                values: new object[,]
                {
                    { "AUTH", "authentication-authorization" },
                    { "AUTHENTICATION & AUTHORIZATION", "authentication-authorization" },
                    { "AUTOMATED TESTING", "automated-testing" },
                    { "AWS", "aws" },
                    { "CI/CD", "ci-cd" },
                    { "DEPENDENCY INJECTION", "dependency-injection" },
                    { "EXPRESS", "express" },
                    { "JAVA", "java" },
                    { "NODE", "nodejs" },
                    { "NODE.JS", "nodejs" },
                    { "PYTHON", "python" },
                    { "REST", "rest-api" },
                    { "REST APIS", "rest-api" },
                    { "TESTING", "automated-testing" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobRequirementExtractions_JobId_DescriptionVersion",
                table: "JobRequirementExtractions",
                columns: new[] { "JobId", "DescriptionVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRequirements_RequirementSetId_Key",
                table: "JobRequirements",
                columns: new[] { "RequirementSetId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRequirements_SkillId",
                table: "JobRequirements",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CandidateProfileId_UpdatedAt",
                table: "Jobs",
                columns: new[] { "CandidateProfileId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementSets_JobId_Active",
                table: "RequirementSets",
                columns: new[] { "JobId", "Active" },
                unique: true,
                filter: "\"Active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementSets_JobId_Version",
                table: "RequirementSets",
                columns: new[] { "JobId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequirementSets_JobRequirementExtractionId",
                table: "RequirementSets",
                column: "JobRequirementExtractionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobRequirements");

            migrationBuilder.DropTable(
                name: "RequirementSets");

            migrationBuilder.DropTable(
                name: "JobRequirementExtractions");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: ".NET WEB API");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "ASP.NET WEB API");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "AUTH");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "AUTHENTICATION & AUTHORIZATION");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "AUTOMATED TESTING");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "AWS");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "CI/CD");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "DEPENDENCY INJECTION");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "EXPRESS");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "JAVA");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "NODE");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "NODE.JS");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "PYTHON");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "REST");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "REST APIS");

            migrationBuilder.DeleteData(
                table: "SkillAliases",
                keyColumn: "Alias",
                keyValue: "TESTING");

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: "authentication-authorization");

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: "automated-testing");

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: "aws");

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: "ci-cd");

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: "dependency-injection");

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: "express");

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: "java");

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: "nodejs");

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: "python");

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: "rest-api");
        }
    }
}
