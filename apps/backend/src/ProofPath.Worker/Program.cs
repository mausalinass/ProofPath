using ProofPath.Worker;
using Microsoft.EntityFrameworkCore;
using ProofPath.Application.Analysis;
using ProofPath.Infrastructure.Persistence;
using ProofPath.Infrastructure.Resumes;
using ProofPath.Infrastructure.GitHub;
using ProofPath.Infrastructure.Jobs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);
if (string.Equals(builder.Configuration["Deployment:EnvironmentName"], "production", StringComparison.OrdinalIgnoreCase) &&
    !builder.Configuration.GetValue<bool>("OpenAI:RealDataProcessingApproved"))
    throw new InvalidOperationException("Production real-data processing requires explicit owner approval or an approved provider retention control.");
builder.Services.AddDbContext<ProofPathDbContext>(options =>
    options.UseNpgsql(DatabaseConfiguration.Resolve(builder.Configuration)));
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("proofpath-worker"))
    .WithMetrics(metrics => metrics.AddHttpClientInstrumentation().AddRuntimeInstrumentation())
    .WithTracing(tracing => tracing.AddHttpClientInstrumentation());
if (builder.Configuration.GetValue<bool>("Observability:OtlpEnabled"))
{
    builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
    builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());
}
builder.Services.AddScoped<IAnalysisQueue, PostgresAnalysisQueue>();
builder.Services.AddResumeModule(builder.Configuration, builder.Environment.EnvironmentName, builder.Environment.ContentRootPath);
builder.Services.AddGitHubModule(builder.Configuration);
builder.Services.AddJobModule(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
