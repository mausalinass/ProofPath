using ProofPath.Worker;
using Microsoft.EntityFrameworkCore;
using ProofPath.Application.Analysis;
using ProofPath.Infrastructure.Persistence;
using ProofPath.Infrastructure.Resumes;
using ProofPath.Infrastructure.GitHub;
using ProofPath.Infrastructure.Jobs;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDbContext<ProofPathDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Configure Worker DefaultConnection securely.")));
builder.Services.AddScoped<IAnalysisQueue, PostgresAnalysisQueue>();
builder.Services.AddResumeModule(builder.Configuration, builder.Environment.EnvironmentName, builder.Environment.ContentRootPath);
builder.Services.AddGitHubModule(builder.Configuration);
builder.Services.AddJobModule(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
