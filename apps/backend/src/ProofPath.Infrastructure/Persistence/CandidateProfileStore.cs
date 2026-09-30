using Microsoft.EntityFrameworkCore;
using ProofPath.Application.Candidates;
using ProofPath.Domain.Entities;

namespace ProofPath.Infrastructure.Persistence;

public sealed class CandidateProfileStore(ProofPathDbContext database) : ICandidateProfileStore
{
    public Task<CandidateProfile?> FindByUserAsync(string userId, CancellationToken cancellationToken) =>
        database.CandidateProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public async Task<CandidateProfile> UpsertAsync(string userId, ProfileInput input, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        // The unique UserId index makes simultaneous first saves idempotent at the database boundary.
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CandidateProfiles"
                ("Id", "UserId", "FirstName", "LastName", "Headline", "Location",
                 "WorkAuthorization", "EducationSummary", "CreatedAt", "UpdatedAt")
            VALUES ({id}, {userId}, {input.FirstName}, {input.LastName}, {input.Headline}, {input.Location},
                    {input.WorkAuthorization}, {input.EducationSummary}, {now}, {now})
            ON CONFLICT ("UserId") DO UPDATE SET
                "FirstName" = EXCLUDED."FirstName", "LastName" = EXCLUDED."LastName",
                "Headline" = EXCLUDED."Headline", "Location" = EXCLUDED."Location",
                "WorkAuthorization" = EXCLUDED."WorkAuthorization", "EducationSummary" = EXCLUDED."EducationSummary",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """, cancellationToken);
        return (await FindByUserAsync(userId, cancellationToken))!;
    }
}
