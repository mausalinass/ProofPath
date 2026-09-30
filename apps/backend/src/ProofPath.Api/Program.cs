using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using ProofPath.Application.Candidates;
using ProofPath.Application.Analysis;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProofPath.Api.Endpoints;
using ProofPath.Infrastructure.Identity;
using ProofPath.Infrastructure.Persistence;
using ProofPath.Infrastructure.Resumes;
using ProofPath.Infrastructure.GitHub;
using ProofPath.Infrastructure.Jobs;
using ProofPath.Infrastructure.Matching;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProofPathDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityCore<ApplicationUser>()
    .AddEntityFrameworkStores<ProofPathDbContext>()
    .AddSignInManager<SignInManager<ApplicationUser>>();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.Events.OnValidatePrincipal = async context =>
        {
            var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = id is null ? null : await users.FindByIdAsync(id);
            var stamp = context.Principal?.FindFirstValue(users.Options.ClaimsIdentity.SecurityStampClaimType);
            if (user is null || stamp != await users.GetSecurityStampAsync(user))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            }
        };
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddScoped<ICandidateProfileStore, CandidateProfileStore>();
builder.Services.AddScoped<CandidateProfileService>();
builder.Services.AddScoped<IAnalysisQueue, PostgresAnalysisQueue>();
builder.Services.AddResumeModule(builder.Configuration, builder.Environment.EnvironmentName, builder.Environment.ContentRootPath);
builder.Services.AddGitHubModule(builder.Configuration);
builder.Services.AddJobModule(builder.Configuration);
builder.Services.AddMatchingModule();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("Security:AuthRequestsPerMinute", 20),
            Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Frontend:Origins").Get<string[]>()
            ?? (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
                ? ["http://localhost:5173"] : []);
        if (origins.Length == 0 || origins.Any(origin => origin == "*"))
            throw new InvalidOperationException("Configure explicit Frontend:Origins.");
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseCors("Frontend");

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
        !HttpMethods.IsOptions(context.Request.Method))
    {
        try
        {
            await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            await Results.Problem("Refresh the form and retry.", statusCode: 400,
                extensions: new Dictionary<string, object?> { ["code"] = "CSRF_INVALID" }).ExecuteAsync(context);
            return;
        }
    }
    await next(context);
});
app.MapGet("/api/v1/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/health", () => Results.Json(new { status = "healthy" }))
    .WithName("GetHealth");

app.MapAuthEndpoints();
app.MapAuthEndpoints("/api/v1/auth");
app.MapCandidateEndpoints();
app.MapAnalysisEndpoints();
app.MapResumeEndpoints();
app.MapGitHubEndpoints();
app.MapJobEndpoints();
app.MapMatchingEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public partial class Program { }
