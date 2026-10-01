using System.Diagnostics;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ProofPath.Api.Endpoints;
using ProofPath.Api.Production;
using ProofPath.Application.Analysis;
using ProofPath.Application.Candidates;
using ProofPath.Infrastructure.GitHub;
using ProofPath.Infrastructure.Identity;
using ProofPath.Infrastructure.Jobs;
using ProofPath.Infrastructure.Matching;
using ProofPath.Infrastructure.Persistence;
using ProofPath.Infrastructure.Recommendations;
using ProofPath.Infrastructure.Resumes;

var builder = WebApplication.CreateBuilder(args);
ProductionConfiguration.Validate(builder.Configuration, builder.Environment);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
builder.Services.AddDbContext<ProofPathDbContext>(options => options.UseNpgsql(DatabaseConfiguration.Resolve(builder.Configuration)));
builder.Services.AddHealthChecks().AddDbContextCheck<ProofPathDbContext>("postgres");
builder.Services.AddDataProtection()
    .SetApplicationName("ProofPath")
    .PersistKeysToDbContext<ProofPathDbContext>();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("proofpath-api"))
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation())
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health")).AddHttpClientInstrumentation());
if (builder.Configuration.GetValue<bool>("Observability:OtlpEnabled"))
{
    builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
    builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());
}
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 4;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<ProofPathDbContext>().AddSignInManager<SignInManager<ApplicationUser>>();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme, options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.Name = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? "ProofPath" : "__Host-ProofPath";
    options.Cookie.Path = "/";
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
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
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
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
builder.Services.AddRecommendationModule();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.Name = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? "ProofPath-Csrf" : "__Host-ProofPath-Csrf";
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = builder.Configuration.GetValue("Security:AuthRequestsPerMinute", 20),
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    }));
});
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var origins = builder.Configuration.GetSection("Frontend:Origins").Get<string[]>() ?? (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? ["http://localhost:5173"] : []);
    if (origins.Length == 0 || origins.Any(origin => origin == "*")) throw new InvalidOperationException("Configure explicit Frontend:Origins.");
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

var app = builder.Build();
if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ProofPathDbContext>().Database.MigrateAsync();
    return;
}
if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) app.UseHsts();
var forwardedHeaders = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, ForwardLimit = 2 };
// Production ingress is restricted by security group to the reviewed load balancer/CloudFront path.
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    var started = Stopwatch.GetTimestamp();
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    context.Response.Headers["X-Trace-Id"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next(context);
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ProofPath.Request");
    logger.LogInformation("HTTP {Method} completed {StatusCode} in {ElapsedMs}ms trace={TraceId}", context.Request.Method, context.Response.StatusCode,
        Stopwatch.GetElapsedTime(started).TotalMilliseconds, Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier);
});
app.UseCors("Frontend");
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method))
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException)
        {
            await Results.Problem("Refresh the form and retry.", statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = "CSRF_INVALID" }).ExecuteAsync(context);
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
app.MapHealthChecks("/health/live", HealthResponse.Live);
app.MapHealthChecks("/health/ready", HealthResponse.Ready);
app.MapHealthChecks("/health", HealthResponse.Ready);
app.MapAuthEndpoints();
app.MapAuthEndpoints("/api/v1/auth");
app.MapCandidateEndpoints();
app.MapAnalysisEndpoints();
app.MapResumeEndpoints();
app.MapGitHubEndpoints();
app.MapJobEndpoints();
app.MapMatchingEndpoints();
app.MapRecommendationEndpoints();
app.Run();

public partial class Program { }
