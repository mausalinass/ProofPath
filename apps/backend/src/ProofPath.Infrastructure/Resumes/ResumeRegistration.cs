using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ProofPath.Application.Analysis;
using ProofPath.Application.Files;
using ProofPath.Application.FileSecurity;
using ProofPath.Application.Resumes;
using ProofPath.Infrastructure.Files;

namespace ProofPath.Infrastructure.Resumes;

public static class ResumeRegistration
{
    public static IServiceCollection AddResumeModule(this IServiceCollection services, IConfiguration configuration, string environmentName, string contentRoot)
    {
        services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
        var fileSecurityProvider = configuration["FileSecurity:Provider"];
        if (string.Equals(fileSecurityProvider, "ClamAV", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IPrivateFileSecurityScanner, ClamAvPrivateFileSecurityScanner>();
        else if (environmentName is "Development" or "Testing")
            services.AddSingleton<IPrivateFileSecurityScanner, DevelopmentPrivateFileSecurityScanner>();
        else
            throw new InvalidOperationException("Production requires FileSecurity:Provider=ClamAV.");
        services.AddSingleton<ProviderCircuit>();
        services.AddSingleton<ILlmProvider>(provider => new OpenAiResumeProvider(new HttpClient { Timeout = Timeout.InfiniteTimeSpan },
            configuration, provider.GetRequiredService<ProviderCircuit>()));
        services.AddSingleton<IPrivateFileStore>(_ =>
        {
            if (configuration["Storage:Provider"] == "S3")
            {
                if (!configuration.GetValue<bool>("Storage:FileSecurityReviewed")) throw new InvalidOperationException("Review file security before enabling S3 uploads.");
                var bucket = configuration["Storage:Bucket"] ?? throw new InvalidOperationException("Configure a private S3 bucket.");
                var region = configuration["Storage:Region"] ?? throw new InvalidOperationException("Configure S3 region.");
                return new S3PrivateFileStore(new AmazonS3Client(RegionEndpoint.GetBySystemName(region)), bucket);
            }
            if (environmentName != "Development" && environmentName != "Testing")
                throw new InvalidOperationException("Private S3 storage and file security review must be configured before enabling production resumes.");
            var root = configuration["Storage:PrivateRoot"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProofPath", "private-resumes");
            var full = Path.GetFullPath(root); var appRoot = Path.GetFullPath(contentRoot) + Path.DirectorySeparatorChar;
            if (full.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Private storage must be outside the application root.");
            return new DevelopmentPrivateFileStore(full);
        });
        services.AddScoped<IResumePersistence, ResumePersistence>();
        services.AddScoped<ResumeWorkspaceService>();
        services.AddScoped<IResumeWorkspace>(provider => provider.GetRequiredService<ResumeWorkspaceService>());
        services.AddScoped<IResumeInputReader>(provider => provider.GetRequiredService<ResumeWorkspaceService>());
        services.AddScoped<IAnalysisHandler, ResumeAnalysisHandler>();
        services.AddScoped<IAnalysisCompletion, ResumeCompletion>();
        services.AddScoped<IPrivateFileCleanup, PrivateFileCleanup>();
        return services;
    }
}
