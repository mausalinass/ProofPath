using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Analysis;
using ProofPath.Application.Jobs;
using ProofPath.Infrastructure.Resumes;

namespace ProofPath.Infrastructure.Jobs;

public static class JobRegistration
{
    public static IServiceCollection AddJobModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IJobRequirementProvider>(provider => new OpenAiJobRequirementProvider(new HttpClient { Timeout = Timeout.InfiniteTimeSpan },
            configuration, provider.GetRequiredService<ProviderCircuit>()));
        services.AddScoped<IJobRequirementNormalizer, JobRequirementNormalizer>();
        services.AddScoped<JobWorkspace>(); services.AddScoped<IJobWorkspace>(provider => provider.GetRequiredService<JobWorkspace>());
        services.AddScoped<IJobInputReader>(provider => provider.GetRequiredService<JobWorkspace>());
        services.AddScoped<IAnalysisHandler, JobAnalysisHandler>(); services.AddScoped<IAnalysisCompletion, JobAnalysisCompletion>();
        return services;
    }
}
