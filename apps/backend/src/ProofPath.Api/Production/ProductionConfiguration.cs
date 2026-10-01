namespace ProofPath.Api.Production;

public static class ProductionConfiguration
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing")) return;

        var origins = configuration.GetSection("Frontend:Origins").Get<string[]>() ?? [];
        Require(origins.Length > 0 && origins.All(origin => Uri.TryCreate(origin, UriKind.Absolute, out _) && origin != "*"),
            "Configure explicit Frontend:Origins for production.");
        Require(string.Equals(configuration["Storage:Provider"], "S3", StringComparison.OrdinalIgnoreCase),
            "Production requires Storage:Provider=S3.");
        Require(configuration.GetValue<bool>("Storage:FileSecurityReviewed"),
            "Production requires Storage:FileSecurityReviewed=true.");
        Require(configuration.GetValue<bool>("OpenAI:PrivacyReviewed"),
            "Production requires OpenAI:PrivacyReviewed=true.");
        foreach (var key in new[] { "Storage:Bucket", "Storage:Region", "OpenAI:ApiKey", "GitHub:AppId", "GitHub:AppSlug", "GitHub:PrivateKeyPem" })
            Require(!string.IsNullOrWhiteSpace(configuration[key]), $"Configure {key} securely for production.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
