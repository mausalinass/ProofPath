using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ProofPath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrivateFileDeletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateFileDeletions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Resumes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AnalysisJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resumes_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResumeExtractions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineJson = table.Column<string>(type: "jsonb", nullable: false),
                    DraftJson = table.Column<string>(type: "jsonb", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    SchemaVersion = table.Column<string>(type: "text", nullable: false),
                    ExtractionVersion = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeExtractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResumeExtractions_Resumes_ResumeId",
                        column: x => x.ResumeId,
                        principalTable: "Resumes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillAliases",
                columns: table => new
                {
                    Alias = table.Column<string>(type: "text", nullable: false),
                    SkillId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillAliases", x => x.Alias);
                    table.ForeignKey(
                        name: "FK_SkillAliases_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BehavioralEvidenceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeExtractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThemeKey = table.Column<string>(type: "text", nullable: false),
                    Statement = table.Column<string>(type: "text", nullable: false),
                    Basis = table.Column<string>(type: "text", nullable: false),
                    Strength = table.Column<string>(type: "text", nullable: false),
                    SourceBlockId = table.Column<string>(type: "text", nullable: false),
                    Quote = table.Column<string>(type: "text", nullable: false),
                    UserCorrected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehavioralEvidenceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BehavioralEvidenceItems_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BehavioralEvidenceItems_ResumeExtractions_ResumeExtractionId",
                        column: x => x.ResumeExtractionId,
                        principalTable: "ResumeExtractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeExtractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Organization = table.Column<string>(type: "text", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    StartDateText = table.Column<string>(type: "text", nullable: true),
                    EndDateText = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    SourceBlockId = table.Column<string>(type: "text", nullable: false),
                    Quote = table.Column<string>(type: "text", nullable: false),
                    UserCorrected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Credentials_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Credentials_ResumeExtractions_ResumeExtractionId",
                        column: x => x.ResumeExtractionId,
                        principalTable: "ResumeExtractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Educations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeExtractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Organization = table.Column<string>(type: "text", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    StartDateText = table.Column<string>(type: "text", nullable: true),
                    EndDateText = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    SourceBlockId = table.Column<string>(type: "text", nullable: false),
                    Quote = table.Column<string>(type: "text", nullable: false),
                    UserCorrected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Educations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Educations_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Educations_ResumeExtractions_ResumeExtractionId",
                        column: x => x.ResumeExtractionId,
                        principalTable: "ResumeExtractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeExtractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<string>(type: "text", nullable: true),
                    OriginalTerm = table.Column<string>(type: "text", nullable: false),
                    Context = table.Column<string>(type: "text", nullable: false),
                    Strength = table.Column<string>(type: "text", nullable: false),
                    SourceBlockId = table.Column<string>(type: "text", nullable: false),
                    Quote = table.Column<string>(type: "text", nullable: false),
                    UserCorrected = table.Column<bool>(type: "boolean", nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceItems_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvidenceItems_ResumeExtractions_ResumeExtractionId",
                        column: x => x.ResumeExtractionId,
                        principalTable: "ResumeExtractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvidenceItems_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Experiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeExtractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Organization = table.Column<string>(type: "text", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    StartDateText = table.Column<string>(type: "text", nullable: true),
                    EndDateText = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    SourceBlockId = table.Column<string>(type: "text", nullable: false),
                    Quote = table.Column<string>(type: "text", nullable: false),
                    UserCorrected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Experiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Experiences_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Experiences_ResumeExtractions_ResumeExtractionId",
                        column: x => x.ResumeExtractionId,
                        principalTable: "ResumeExtractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeExtractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Organization = table.Column<string>(type: "text", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    StartDateText = table.Column<string>(type: "text", nullable: true),
                    EndDateText = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    SourceBlockId = table.Column<string>(type: "text", nullable: false),
                    Quote = table.Column<string>(type: "text", nullable: false),
                    UserCorrected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Projects_ResumeExtractions_ResumeExtractionId",
                        column: x => x.ResumeExtractionId,
                        principalTable: "ResumeExtractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Skills",
                columns: new[] { "Id", "DisplayName" },
                values: new object[,]
                {
                    { "aspnet-core", "ASP.NET Core" },
                    { "csharp", "C#" },
                    { "docker", "Docker" },
                    { "dotnet", ".NET" },
                    { "entity-framework-core", "Entity Framework Core" },
                    { "git", "Git" },
                    { "github-actions", "GitHub Actions" },
                    { "javascript", "JavaScript" },
                    { "postgresql", "PostgreSQL" },
                    { "react", "React" },
                    { "sql", "SQL" },
                    { "typescript", "TypeScript" }
                });

            migrationBuilder.InsertData(
                table: "SkillAliases",
                columns: new[] { "Alias", "SkillId" },
                values: new object[,]
                {
                    { ".NET", "dotnet" },
                    { "ASP.NET CORE", "aspnet-core" },
                    { "C#", "csharp" },
                    { "CSHARP", "csharp" },
                    { "DOCKER", "docker" },
                    { "EF CORE", "entity-framework-core" },
                    { "ENTITY FRAMEWORK CORE", "entity-framework-core" },
                    { "GIT", "git" },
                    { "GITHUB ACTIONS", "github-actions" },
                    { "JAVASCRIPT", "javascript" },
                    { "POSTGRES", "postgresql" },
                    { "POSTGRESQL", "postgresql" },
                    { "REACT", "react" },
                    { "SQL", "sql" },
                    { "TYPESCRIPT", "typescript" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BehavioralEvidenceItems_CandidateProfileId",
                table: "BehavioralEvidenceItems",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BehavioralEvidenceItems_ResumeExtractionId",
                table: "BehavioralEvidenceItems",
                column: "ResumeExtractionId");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_CandidateProfileId",
                table: "Credentials",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_ResumeExtractionId",
                table: "Credentials",
                column: "ResumeExtractionId");

            migrationBuilder.CreateIndex(
                name: "IX_Educations_CandidateProfileId",
                table: "Educations",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Educations_ResumeExtractionId",
                table: "Educations",
                column: "ResumeExtractionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceItems_CandidateProfileId",
                table: "EvidenceItems",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceItems_ResumeExtractionId",
                table: "EvidenceItems",
                column: "ResumeExtractionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceItems_SkillId",
                table: "EvidenceItems",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Experiences_CandidateProfileId",
                table: "Experiences",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Experiences_ResumeExtractionId",
                table: "Experiences",
                column: "ResumeExtractionId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateFileDeletions_StorageKey",
                table: "PrivateFileDeletions",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CandidateProfileId",
                table: "Projects",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ResumeExtractionId",
                table: "Projects",
                column: "ResumeExtractionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeExtractions_ResumeId",
                table: "ResumeExtractions",
                column: "ResumeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_CandidateProfileId_Version",
                table: "Resumes",
                columns: new[] { "CandidateProfileId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_StorageKey",
                table: "Resumes",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillAliases_SkillId",
                table: "SkillAliases",
                column: "SkillId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BehavioralEvidenceItems");

            migrationBuilder.DropTable(
                name: "Credentials");

            migrationBuilder.DropTable(
                name: "Educations");

            migrationBuilder.DropTable(
                name: "EvidenceItems");

            migrationBuilder.DropTable(
                name: "Experiences");

            migrationBuilder.DropTable(
                name: "PrivateFileDeletions");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "SkillAliases");

            migrationBuilder.DropTable(
                name: "ResumeExtractions");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "Resumes");
        }
    }
}
