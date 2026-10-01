using ProofPath.Application.Candidates;
using ProofPath.Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using ProofPath.Infrastructure.Identity;

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

            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var user = new ApplicationUser
            {
                Email = request.Email,
                UserName = request.Email
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
            return Results.Created($"{prefix}/register/{user.Id}", new
            {
                user.Id,
                user.Email
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
            if (!result.Succeeded)
            {
                return Results.Problem("Invalid credentials.", statusCode: 401);
            }

            return Results.Ok(new { user.Id, user.Email });
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

    public sealed record RegisterRequest(string? Email, string? Password, string? FirstName = null, string? LastName = null);
    public sealed record LoginRequest(string? Email, string? Password);
}
