using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProofPath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FreezeMatchingV1ExperienceCalibration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "MatchingScoringVersions",
                keyColumn: "Id",
                keyValue: "matching-v1",
                column: "ConfigurationJson",
                value: "{\"Version\":\"matching-v1\",\"TechnicalWeight\":0.45,\"EvidenceWeight\":0.25,\"ExperienceWeight\":0.25,\"EducationWeight\":0.05,\"WeakStrength\":0.3,\"ModerateStrength\":0.65,\"StrongStrength\":1,\"StaleFactor\":0.6,\"InactiveFactor\":0,\"RelatedScore\":0.35,\"WeakRelatedCap\":0.7,\"RequiredLevelFactor\":1,\"PreferredLevelFactor\":0.5,\"UnspecifiedLevelFactor\":0.5,\"CriticalImportance\":1.5,\"HighImportance\":1.2,\"MediumImportance\":1,\"LowImportance\":0.7,\"RequiredTechnicalWeight\":0.8,\"PreferredTechnicalWeight\":0.2,\"RelatedEvidenceFactor\":0.25,\"ExplicitExperienceWeight\":0.6,\"SeniorityWeight\":0.25,\"ResponsibilitiesWeight\":0.15,\"DegreeInProgressFactor\":0.65,\"MinimumComponentCoverage\":0.5,\"CoverageWarningThreshold\":0.6,\"CompleteCoverageThreshold\":0.8,\"BehavioralComponentEnabled\":false}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "MatchingScoringVersions",
                keyColumn: "Id",
                keyValue: "matching-v1",
                column: "ConfigurationJson",
                value: "{\"Version\":\"matching-v1\",\"TechnicalWeight\":0.45,\"EvidenceWeight\":0.25,\"ExperienceWeight\":0.25,\"EducationWeight\":0.05,\"WeakStrength\":0.3,\"ModerateStrength\":0.65,\"StrongStrength\":1,\"StaleFactor\":0.6,\"InactiveFactor\":0,\"RelatedScore\":0.35,\"WeakRelatedCap\":0.7,\"RequiredLevelFactor\":1,\"PreferredLevelFactor\":0.5,\"UnspecifiedLevelFactor\":0.5,\"CriticalImportance\":1.5,\"HighImportance\":1.2,\"MediumImportance\":1,\"LowImportance\":0.7,\"RequiredTechnicalWeight\":0.8,\"PreferredTechnicalWeight\":0.2,\"RelatedEvidenceFactor\":0.25,\"DegreeInProgressFactor\":0.65,\"MinimumComponentCoverage\":0.5,\"CoverageWarningThreshold\":0.6,\"CompleteCoverageThreshold\":0.8,\"BehavioralComponentEnabled\":false}");
        }
    }
}
