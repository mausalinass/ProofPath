using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Configuration;

namespace ProofPath.Infrastructure.Identity;

public interface IAccountEmailSender
{
    Task SendVerificationAsync(string email, string link, CancellationToken cancellationToken);
    Task SendPasswordResetAsync(string email, string link, CancellationToken cancellationToken);
}

public sealed class SesAccountEmailSender(IAmazonSimpleEmailServiceV2 ses, IConfiguration configuration) : IAccountEmailSender
{
    public Task SendVerificationAsync(string email, string link, CancellationToken cancellationToken) =>
        SendAsync(email, "Verify your ProofPath email", "Verify email", link,
            "This link expires according to the ProofPath identity-token policy. If you did not create an account, ignore this message.", cancellationToken);

    public Task SendPasswordResetAsync(string email, string link, CancellationToken cancellationToken) =>
        SendAsync(email, "Reset your ProofPath password", "Reset password", link,
            "This single-use link expires according to the ProofPath identity-token policy. If you did not request it, ignore this message.", cancellationToken);

    private async Task SendAsync(string email, string subject, string action, string link, string explanation, CancellationToken cancellationToken)
    {
        var sender = configuration["Email:SenderAddress"] ?? throw new InvalidOperationException("Email:SenderAddress is not configured.");
        var safeLink = System.Net.WebUtility.HtmlEncode(link);
        await ses.SendEmailAsync(new SendEmailRequest
        {
            FromEmailAddress = sender,
            Destination = new Destination { ToAddresses = [email] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject, Charset = "UTF-8" },
                    Body = new Body
                    {
                        Text = new Content { Data = $"{action}: {link}\n\n{explanation}", Charset = "UTF-8" },
                        Html = new Content { Data = $"<p><a href=\"{safeLink}\">{action}</a></p><p>{System.Net.WebUtility.HtmlEncode(explanation)}</p>", Charset = "UTF-8" }
                    }
                }
            }
        }, cancellationToken);
    }
}

public sealed class DisabledAccountEmailSender : IAccountEmailSender
{
    public Task SendVerificationAsync(string email, string link, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task SendPasswordResetAsync(string email, string link, CancellationToken cancellationToken) => Task.CompletedTask;
}
