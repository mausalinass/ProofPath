using System.Text.RegularExpressions;
using ProofPath.Application.Resumes;

namespace ProofPath.Infrastructure.Resumes;

public static class ResumeProviderInput
{
    // Contact details are unnecessary for professional fact extraction. Keep the original source
    // privately for review and send only redacted text; no binary is sent to the provider.
    public static SourceBlock[] Redact(DocumentText source) => source.Blocks.Select(block => block with { Text = RedactText(block.Text) }).ToArray();
    public static string RedactText(string text)
    {
        text = Regex.Replace(text, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", "[contact removed]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return Regex.Replace(text, @"(?<!\w)\+?\d[\d ()+.-]{6,}\d(?!\w)", match =>
            match.Value.Count(char.IsDigit) >= 10 ? "[contact removed]" : match.Value, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}
