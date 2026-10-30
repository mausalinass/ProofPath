using ProofPath.Application.Candidates;
using ProofPath.Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using ProofPath.Infrastructure.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace ProofPath.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api/auth")
    {
        endpoints.MapPost($"{prefix}/register", async (
            RegisterRequest request,
            UserManager<ApplicationUser> userManager,
            ProofPathDbContext database,
            CandidateProfileService profiles,
            IAccountEmailSender emails,
            IConfiguration configuration,
            IHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var registrationEnabled = configuration.GetValue<bool?>("Auth:RegistrationEnabled")
                ?? environment.IsDevelopment() || environment.IsEnvironment("Testing");
            if (!registrationEnabled)
                return Results.Problem("Public registration is not enabled for this environment.", statusCode: 403,
                    extensions: new Dictionary<string, object?> { ["code"] = "REGISTRATION_DISABLED" });
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["Email is required."]
                });
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = ["Password is required."]
                });
            }

            if (string.IsNullOrWhiteSpace(request.FirstName))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["firstName"] = ["Name is required."] });

            var privacyVersion = configuration["Privacy:CurrentVersion"] ?? "2026-10-30";
            var privacyRequired = configuration.GetValue<bool?>("Auth:RequirePrivacyConsent")
                ?? !(environment.IsDevelopment() || environment.IsEnvironment("Testing"));
            if (privacyRequired && (!request.AcceptPrivacy || !string.Equals(request.PrivacyVersion, privacyVersion, StringComparison.Ordinal)))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["privacy"] = ["Accept the current privacy notice to create an account."] });

            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var user = new ApplicationUser
            {
                Email = request.Email,
                UserName = request.Email,
                PrivacyAcceptedAt = request.AcceptPrivacy ? DateTime.UtcNow : null,
                PrivacyVersion = request.AcceptPrivacy ? privacyVersion : null
            };

            var result = await userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

                return Results.ValidationProblem(errors);
            }

            await profiles.SaveAsync(user.Id, new ProfileInput(request.FirstName, request.LastName, null, null, null, null), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var confirmationRequired = userManager.Options.SignIn.RequireConfirmedEmail;
            if (confirmationRequired)
            {
                var token = EncodeToken(await userManager.GenerateEmailConfirmationTokenAsync(user));
                var origin = configuration["Frontend:PublicUrl"] ?? configuration.GetSection("Frontend:Origins").Get<string[]>()?.FirstOrDefault()
                    ?? throw new InvalidOperationException("Frontend:PublicUrl is not configured.");
                var link = $"{origin.TrimEnd('/')}/verify-email?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
                await emails.SendVerificationAsync(user.Email!, link, cancellationToken);
            }
            return Results.Created($"{prefix}/register/{user.Id}", new
            {
                user.Id,
                user.Email,
                requiresEmailConfirmation = confirmationRequired
            });
        }).RequireRateLimiting("auth");

        endpoints.MapPost($"{prefix}/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["Email is required."]
                });
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = ["Password is required."]
                });
            }

            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                return Results.Problem("Invalid credentials.", statusCode: 401);
            }

            var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
            if (result.IsNotAllowed)
                return Results.Problem("Verify your email before signing in.", statusCode: 403,
                    extensions: new Dictionary<string, object?> { ["code"] = "EMAIL_NOT_VERIFIED" });
            if (!result.Succeeded)
            {
                return Results.Problem("Invalid credentials.", statusCode: 401);
            }

            return Results.Ok(new { user.Id, user.Email });
        }).RequireRateLimiting("auth");

        endpoints.MapPost($"{prefix}/confirm-email", async (ConfirmEmailRequest request,
            UserManager<ApplicationUser> users, CancellationToken cancellationToken) =>
        {
            var user = string.IsNullOrWhiteSpace(request.Email) ? null : await users.FindByEmailAsync(request.Email);
            if (user is null || !TryDecodeToken(request.Token, out var token)) return Results.Problem("The verification link is invalid.", statusCode: 400);
            var result = await users.ConfirmEmailAsync(user, token);
            return result.Succeeded ? Results.NoContent() : Results.Problem("The verification link is invalid or expired.", statusCode: 400);
        }).RequireRateLimiting("auth");

        endpoints.MapPost($"{prefix}/forgot-password", async (ForgotPasswordRequest request,
            UserManager<ApplicationUser> users, IAccountEmailSender emails, IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var user = string.IsNullOrWhiteSpace(request.Email) ? null : await users.FindByEmailAsync(request.Email);
            if (user is not null && await users.IsEmailConfirmedAsync(user))
            {
                var token = EncodeToken(await users.GeneratePasswordResetTokenAsync(user));
                var origin = configuration["Frontend:PublicUrl"] ?? configuration.GetSection("Frontend:Origins").Get<string[]>()?.FirstOrDefault()
                    ?? throw new InvalidOperationException("Frontend:PublicUrl is not configured.");
                var link = $"{origin.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
                await emails.SendPasswordResetAsync(user.Email!, link, cancellationToken);
            }
            return Results.Accepted(value: new { message = "If that verified account exists, a reset link has been sent." });
        }).RequireRateLimiting("auth");

        endpoints.MapPost($"{prefix}/reset-password", async (ResetPasswordRequest request,
            UserManager<ApplicationUser> users) =>
        {
            var user = string.IsNullOrWhiteSpace(request.Email) ? null : await users.FindByEmailAsync(request.Email);
            if (user is null || !TryDecodeToken(request.Token, out var token) || string.IsNullOrWhiteSpace(request.Password))
                return Results.Problem("The password reset link is invalid or expired.", statusCode: 400);
            var result = await users.ResetPasswordAsync(user, token, request.Password);
            if (!result.Succeeded)
            {
                var errors = result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
                return Results.ValidationProblem(errors);
            }
            await users.UpdateSecurityStampAsync(user);
            return Results.NoContent();
        }).RequireRateLimiting("auth");

        endpoints.MapPost($"{prefix}/logout", async (
            SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.NoContent();
        });

        endpoints.MapGet($"{prefix}/me", (ClaimsPrincipal principal) =>
        {
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = principal.FindFirstValue(ClaimTypes.Email);

            return Results.Ok(new { id, email });
        })
        .RequireAuthorization();

        return endpoints;
    }

    private static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    private static bool TryDecodeToken(string? value, out string token)
    {
        token = "";
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)); return true; }
        catch (FormatException) { return false; }
    }

    public sealed record RegisterRequest(string? Email, string? Password, string? FirstName = null, string? LastName = null,
        bool AcceptPrivacy = false, string? PrivacyVersion = null);
    public sealed record LoginRequest(string? Email, string? Password);
    public sealed record ConfirmEmailRequest(string? Email, string? Token);
    public sealed record ForgotPasswordRequest(string? Email);
    public sealed record ResetPasswordRequest(string? Email, string? Token, string? Password);
}
