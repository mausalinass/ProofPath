namespace ProofPath.Domain.Entities;

public class CandidateProfile
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Headline { get; set; }
    public string? Location { get; set; }
    public string? WorkAuthorization { get; set; }
    public string? EducationSummary { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
