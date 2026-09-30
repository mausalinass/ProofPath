using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProofPath.Application.GitHub;

public sealed record TechnologyDefinition(string Key, string DisplayName, string Category, string SupportLevel,
    string? CanonicalSkillKey, string DetectorProfile, IReadOnlyList<string> Aliases);
public sealed record EvidenceProvenance(string RevisionSha, string Path, int? StartLine, int? EndLine,
    string Detector, string DetectorVersion, DateTime ObservedAt);
public sealed record GitHubEvidenceCandidate(string SkillKey, string OriginalTerm, string EvidenceType,
    string Detail, string Strength, decimal ExtractionConfidence, EvidenceProvenance Provenance);
public sealed record RepositoryCoverage(int TreeEntriesInspected, int CandidateFiles, int FilesFetched,
    long FetchedBytes, bool Limited);
public sealed record RepositoryAnalysisResult(long RepositoryId, string FullName, string RevisionSha,
    string ExtractionVersion, string AnalysisPolicyVersion, string Status, RepositoryCoverage Coverage,
    IReadOnlyList<string> DetectedTechnologies, IReadOnlyList<GitHubEvidenceCandidate> Evidence,
    IReadOnlyList<string> Warnings);

public static class TechnologyRegistry
{
    public static readonly IReadOnlyList<TechnologyDefinition> Entries =
    [
        new("csharp", "C#", "Language", "Deep", "csharp", "csharp", [".cs", "C#"]),
        new("dotnet", ".NET", "Platform", "Deep", "dotnet", "dotnet", [".csproj", ".sln"]),
        new("typescript", "TypeScript", "Language", "Deep", "typescript", "typescript", [".ts", ".tsx"]),
        new("javascript", "JavaScript", "Language", "Deep", "javascript", "javascript", [".js", ".jsx"]),
        new("aspnet-core", "ASP.NET Core", "Framework", "Deep", "aspnet-core", "dotnet", ["Microsoft.NET.Sdk.Web"]),
        new("react", "React", "Framework", "Deep", "react", "node", ["react"]),
        new("entity-framework-core", "Entity Framework Core", "Data", "Deep", "entity-framework-core", "dotnet", ["Microsoft.EntityFrameworkCore", "DbContext"]),
        new("postgresql", "PostgreSQL", "Data", "Deep", "postgresql", "database", ["Npgsql", "postgres"]),
        new("docker", "Docker", "Practice", "Deep", "docker", "docker", ["Dockerfile", "compose.yml", "compose.yaml"]),
        new("github-actions", "GitHub Actions", "Practice", "Deep", "github-actions", "workflow", [".github/workflows/"]),
        new("python", "Python", "Language", "Basic", "python", "python", [".py", "requirements.txt", "pyproject.toml"]),
        new("java", "Java", "Language", "Basic", "java", "java", [".java", "pom.xml", "build.gradle"]),
        new("sql", "SQL", "Data", "Basic", "sql", "sql", [".sql"]),
        new("nodejs", "Node.js", "Platform", "Basic", "nodejs", "node", ["package.json"]),
        new("vite", "Vite", "Tool", "Recognition", null, "node", ["vite"]),
        new("playwright", "Playwright", "Testing", "Recognition", null, "node", ["@playwright/test"]),
        new("xunit", "xUnit", "Testing", "Recognition", null, "dotnet", ["xunit"]),
        new("vitest", "Vitest", "Testing", "Recognition", null, "node", ["vitest"]),
        new("deployment-automation", "Deployment automation", "Practice", "Recognition", null, "workflow", ["deploy", "deployment"])
    ];
}

public sealed class DeterministicRepositoryAnalyzer
{
    public const string ExtractionVersion = "github-extraction-v1";
    public const string PolicyVersion = "github-policy-v1";
    private const string DetectorVersion = "detectors-v1";

    public RepositoryAnalysisResult Analyze(GitHubRepositorySnapshot snapshot)
    {
        var signals = new Dictionary<string, List<(string Type, string Detail, string Path, string Detector, decimal Confidence)>>();
        void Add(string key, string type, string detail, string path, string detector, decimal confidence)
        {
            if (!signals.TryGetValue(key, out var values)) signals[key] = values = [];
            if (!values.Any(item => item.Type == type && item.Path == path && item.Detector == detector))
                values.Add((type, detail, path, detector, confidence));
        }
        foreach (var language in snapshot.Languages.Keys)
        {
            var key = language.ToLowerInvariant() switch { "c#" => "csharp", "typescript" => "typescript", "javascript" => "javascript", "python" => "python", "java" => "java", _ => null };
            if (key is not null) Add(key, "Presence", $"GitHub reports {language} in the repository snapshot.", "", "LanguageDetector", .96m);
        }
        foreach (var file in snapshot.Files)
        {
            var path = file.Path.Replace('\\', '/'); var lower = path.ToLowerInvariant(); var content = file.Content;
            if (lower.EndsWith(".csproj"))
            {
                Add("dotnet", "Configuration", ".NET project configuration is present.", path, "ManifestDetector", .99m);
                if (content.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)) Add("aspnet-core", "Configuration", "ASP.NET Core web SDK is configured.", path, "FrameworkDetector", .99m);
                if (content.Contains("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)) Add("entity-framework-core", "Configuration", "Entity Framework Core is a direct project dependency.", path, "DependencyDetector", .98m);
                if (content.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)) Add("postgresql", "Configuration", "Npgsql PostgreSQL integration is configured.", path, "DependencyDetector", .98m);
                if (content.Contains("xunit", StringComparison.OrdinalIgnoreCase)) Add("xunit", "Configuration", "xUnit is configured as a test dependency.", path, "TestDetector", .98m);
            }
            if (lower.EndsWith("package.json") && TryDependencies(content, out var dependencies))
            {
                if (dependencies.Contains("react")) Add("react", "Configuration", "React is a direct package dependency.", path, "DependencyDetector", .98m);
                if (dependencies.Contains("typescript")) Add("typescript", "Configuration", "TypeScript is a direct package dependency.", path, "DependencyDetector", .98m);
                if (dependencies.Contains("vite")) Add("vite", "Configuration", "Vite is configured in the package manifest.", path, "DependencyDetector", .98m);
                if (dependencies.Contains("vitest")) Add("vitest", "Configuration", "Vitest is configured in the package manifest.", path, "TestDetector", .98m);
                if (dependencies.Contains("@playwright/test")) Add("playwright", "Configuration", "Playwright is configured in the package manifest.", path, "TestDetector", .98m);
                Add("nodejs", "Presence", "A Node.js package manifest is present.", path, "ManifestDetector", .95m);
            }
            if (Path.GetFileName(lower) == "dockerfile" || lower.EndsWith("compose.yml") || lower.EndsWith("compose.yaml"))
                Add("docker", "Configuration", "Container configuration is present.", path, "DockerDetector", .99m);
            if (lower.StartsWith(".github/workflows/") && (lower.EndsWith(".yml") || lower.EndsWith(".yaml")))
            {
                Add("github-actions", "Workflow", "A GitHub Actions workflow is configured.", path, "CiCdDetector", .99m);
                if (Regex.IsMatch(content, @"(?i)\b(deploy|deployment|docker\s+push|aws|azure)\b"))
                    Add("deployment-automation", "Deployment", "The CI/CD workflow contains an explicit deployment step or target.", path, "DeploymentDetector", .94m);
            }
            if (lower.EndsWith(".cs") && Regex.IsMatch(content, @"\b(DbContext|UseNpgsql|AddDbContext)\b"))
                Add("entity-framework-core", "Implementation", "Application source configures or uses an EF Core database context.", path, "ImplementationSignalDetector", .97m);
            if ((lower.EndsWith(".tsx") || lower.EndsWith(".jsx")) && Regex.IsMatch(content, @"\b(useState|useEffect|useQuery|createRoot)\b"))
                Add("react", "Implementation", "Application source contains React component or hook usage.", path, "ImplementationSignalDetector", .96m);
            if (lower.EndsWith(".cs") && Regex.IsMatch(content, @"\[(Fact|Theory)\]") && Regex.IsMatch(content, @"\bAssert\."))
                Add("xunit", "Testing", "A non-template xUnit test contains an assertion.", path, "TestImplementationDetector", .96m);
            if ((lower.EndsWith(".ts") || lower.EndsWith(".tsx") || lower.EndsWith(".js") || lower.EndsWith(".jsx")) &&
                Regex.IsMatch(content, @"\b(it|test)\s*\(") && Regex.IsMatch(content, @"\bexpect\s*\("))
                Add("vitest", "Testing", "A JavaScript or TypeScript test contains an expectation.", path, "TestImplementationDetector", .94m);
            if (Path.GetFileName(lower).StartsWith("readme", StringComparison.Ordinal))
                foreach (var definition in TechnologyRegistry.Entries.Where(item => item.Key != "deployment-automation"))
                    if (definition.Aliases.Any(alias => content.Contains(alias, StringComparison.OrdinalIgnoreCase)))
                        Add(definition.Key, "Claim", $"README claims {definition.DisplayName} as project context.", path, "ReadmeClaimDetector", .70m);
        }
        var evidence = new List<GitHubEvidenceCandidate>();
        foreach (var (key, values) in signals)
        {
            var definition = TechnologyRegistry.Entries.First(item => item.Key == key);
            var best = values.OrderByDescending(item => Rank(item.Type)).ThenByDescending(item => item.Confidence).First();
            var corroborated = values.Select(item => item.Detector).Distinct().Count() > 1;
            var strength = best.Type is "Implementation" or "Workflow" ? "Strong" : best.Type == "Configuration" || corroborated ? "Moderate" : "Weak";
            var confidence = Math.Min(.99m, best.Confidence + (corroborated ? .01m : 0));
            evidence.Add(new(definition.CanonicalSkillKey ?? definition.Key, definition.DisplayName, best.Type, best.Detail, strength,
                confidence, new(snapshot.RevisionSha, best.Path, null, null, best.Detector, DetectorVersion, DateTime.UtcNow)));
        }
        var coverage = new RepositoryCoverage(snapshot.TreeEntriesInspected, snapshot.CandidateFiles, snapshot.Files.Count,
            snapshot.FetchedBytes, snapshot.TreeTruncated || snapshot.Warnings.Count > 0);
        return new(snapshot.Repository.Id, snapshot.Repository.FullName, snapshot.RevisionSha, ExtractionVersion, PolicyVersion,
            coverage.Limited ? "CompletedLimited" : "Completed", coverage, signals.Keys.Order().ToArray(), evidence,
            snapshot.Warnings);
    }
    private static int Rank(string type) => type switch { "Implementation" => 5, "Testing" => 5, "Workflow" => 5, "Deployment" => 5, "Configuration" => 4, "Presence" => 2, _ => 1 };
    private static bool TryDependencies(string content, out HashSet<string> dependencies)
    {
        dependencies = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var json = JsonDocument.Parse(content);
            foreach (var name in new[] { "dependencies", "devDependencies", "peerDependencies" })
                if (json.RootElement.TryGetProperty(name, out var group) && group.ValueKind == JsonValueKind.Object)
                    foreach (var item in group.EnumerateObject()) dependencies.Add(item.Name);
            return true;
        }
        catch (JsonException) { return false; }
    }
}
