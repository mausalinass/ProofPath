using ProofPath.Domain.Entities;

namespace ProofPath.Application.Candidates;

public interface ICandidateProfileStore
{
    Task<CandidateProfile?> FindByUserAsync(string userId, CancellationToken cancellationToken);
    Task<CandidateProfile> UpsertAsync(string userId, ProfileInput input, CancellationToken cancellationToken);
}

public sealed record ProfileInput(string? FirstName, string? LastName, string? Headline,
    string? Location, string? WorkAuthorization, string? EducationSummary)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        Check("firstName", FirstName, 100);
        Check("lastName", LastName, 100);
        Check("headline", Headline, 200);
        Check("location", Location, 150);
        Check("workAuthorization", WorkAuthorization, 200);
        Check("educationSummary", EducationSummary, 2000);
        return errors;

        void Check(string field, string? value, int limit)
        {
            if (value?.Length > limit)
                errors[field] = [$"Use at most {limit} characters."];
        }
    }
}

public sealed record ProfileView(Guid Id, string? FirstName, string? LastName, string? Headline,
    string? Location, string? WorkAuthorization, string? EducationSummary,
    DateTime CreatedAt, DateTime UpdatedAt)
{
    public static ProfileView From(CandidateProfile profile) => new(profile.Id, profile.FirstName,
        profile.LastName, profile.Headline, profile.Location, profile.WorkAuthorization,
        profile.EducationSummary, profile.CreatedAt, profile.UpdatedAt);
}

public sealed class CandidateProfileService(ICandidateProfileStore store)
{
    public async Task<ProfileView?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await store.FindByUserAsync(userId, cancellationToken);
        return profile is null ? null : ProfileView.From(profile);
    }

    public async Task<ProfileView> SaveAsync(string userId, ProfileInput input, CancellationToken cancellationToken)
    {
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var normalized = new ProfileInput(Clean(input.FirstName), Clean(input.LastName), Clean(input.Headline),
            Clean(input.Location), Clean(input.WorkAuthorization), Clean(input.EducationSummary));
        return ProfileView.From(await store.UpsertAsync(userId, normalized, cancellationToken));
    }
}
