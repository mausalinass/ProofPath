using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProofPath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableAnalysisJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputVersion = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    AvailableAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    Retryable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisJobs_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisJobs_CandidateProfileId_Kind_ResourceId_InputVersion",
                table: "AnalysisJobs",
                columns: new[] { "CandidateProfileId", "Kind", "ResourceId", "InputVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisJobs_State_AvailableAt",
                table: "AnalysisJobs",
                columns: new[] { "State", "AvailableAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisJobs");
        }
    }
}
