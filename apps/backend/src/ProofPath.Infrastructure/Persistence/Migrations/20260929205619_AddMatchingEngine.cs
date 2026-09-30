using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProofPath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchingEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchingScoringVersions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "jsonb", nullable: false),
                    Frozen = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchingScoringVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoringVersionId = table.Column<string>(type: "character varying(64)", nullable: false),
                    CandidateSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    RequirementSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    OverallScore = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    OverallClassification = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OverallStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OverallConfidence = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    EvaluationCoverage = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchResults_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchResults_MatchingScoringVersions_ScoringVersionId",
                        column: x => x.ScoringVersionId,
                        principalTable: "MatchingScoringVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchResults_RequirementSets_RequirementSetId",
                        column: x => x.RequirementSetId,
                        principalTable: "RequirementSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequirementMatchRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultSnapshotJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementMatchRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementMatchRecords_MatchResults_MatchResultId",
                        column: x => x.MatchResultId,
                        principalTable: "MatchResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequirementMatchEvidenceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementMatchRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    Contribution = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementMatchEvidenceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementMatchEvidenceRecords_RequirementMatchRecords_Req~",
                        column: x => x.RequirementMatchRecordId,
                        principalTable: "RequirementMatchRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MatchingScoringVersions",
                columns: new[] { "Id", "ConfigurationJson", "CreatedAt", "Frozen" },
                values: new object[] { "matching-v1", "{\"Version\":\"matching-v1\",\"TechnicalWeight\":0.45,\"EvidenceWeight\":0.25,\"ExperienceWeight\":0.25,\"EducationWeight\":0.05,\"WeakStrength\":0.3,\"ModerateStrength\":0.65,\"StrongStrength\":1,\"StaleFactor\":0.6,\"InactiveFactor\":0,\"RelatedScore\":0.35,\"WeakRelatedCap\":0.7,\"RequiredLevelFactor\":1,\"PreferredLevelFactor\":0.5,\"UnspecifiedLevelFactor\":0.5,\"CriticalImportance\":1.5,\"HighImportance\":1.2,\"MediumImportance\":1,\"LowImportance\":0.7,\"MinimumComponentCoverage\":0.5,\"CoverageWarningThreshold\":0.6,\"CompleteCoverageThreshold\":0.8,\"BehavioralComponentEnabled\":false}", new DateTime(2026, 8, 31, 0, 0, 0, 0, DateTimeKind.Utc), true });

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_JobId_CreatedAt",
                table: "MatchResults",
                columns: new[] { "JobId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_RequirementSetId",
                table: "MatchResults",
                column: "RequirementSetId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_ScoringVersionId",
                table: "MatchResults",
                column: "ScoringVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementMatchEvidenceRecords_RequirementMatchRecordId_Ev~",
                table: "RequirementMatchEvidenceRecords",
                columns: new[] { "RequirementMatchRecordId", "EvidenceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequirementMatchRecords_MatchResultId_RequirementId",
                table: "RequirementMatchRecords",
                columns: new[] { "MatchResultId", "RequirementId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequirementMatchEvidenceRecords");

            migrationBuilder.DropTable(
                name: "RequirementMatchRecords");

            migrationBuilder.DropTable(
                name: "MatchResults");

            migrationBuilder.DropTable(
                name: "MatchingScoringVersions");
        }
    }
}
