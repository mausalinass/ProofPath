using Microsoft.AspNetCore.Identity;

namespace ProofPath.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public DateTime? PrivacyAcceptedAt { get; set; }
    public string? PrivacyVersion { get; set; }
}
