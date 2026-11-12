using System.Net;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProofPath.Application.Analysis;
using ProofPath.Application.GitHub;

namespace ProofPath.Infrastructure.GitHub;

public sealed class GitHubAppProvider(HttpClient http, IConfiguration configuration, ILogger<GitHubAppProvider>? logger = null) : IGitHubProvider
{
    private readonly ILogger<GitHubAppProvider> log = logger ?? NullLogger<GitHubAppProvider>.Instance;
    private const int MaxTreeEntries = 5000, MaxFiles = 150, MaxFileBytes = 512 * 1024, MaxTotalBytes = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string Setting(string key) => configuration[$"GitHub:{key}"] is { Length: > 0 } value
        && !string.IsNullOrWhiteSpace(value) && value != "REPLACE"
        ? value : throw new GitHubProblem("GITHUB_NOT_CONFIGURED", 503);

    private string CallbackUri()
    {
        var value = configuration["GitHub:CallbackUrl"]
            ?? (configuration["Frontend:PublicUrl"] ?? "http://127.0.0.1:5173").TrimEnd('/') + "/api/v1/github/callback";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.IsLoopback && uri.Scheme == "http")) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new GitHubProblem("GITHUB_NOT_CONFIGURED", 503);
        return uri.AbsoluteUri;
    }
    private string PrivateKey()
    {
        var direct = configuration["GitHub:PrivateKeyPem"];
        if (!string.IsNullOrWhiteSpace(direct)) return direct.Replace("\\n", "\n", StringComparison.Ordinal);
        var path = configuration["GitHub:PrivateKeyPath"];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new GitHubProblem("GITHUB_NOT_CONFIGURED", 503);
        return File.ReadAllText(path);
    }
    public Uri BuildInstallationUri(string state) => new($"https://github.com/apps/{Uri.EscapeDataString(Setting("AppSlug"))}/installations/new?state={Uri.EscapeDataString(state)}");

    public Uri BuildAuthorizationUri(string state, string challenge)
    {
        _ = Setting("ClientSecret");
        return new Uri("https://github.com/login/oauth/authorize" +
            $"?client_id={Uri.EscapeDataString(Setting("ClientId"))}&redirect_uri={Uri.EscapeDataString(CallbackUri())}" +
            $"&state={Uri.EscapeDataString(state)}&code_challenge={Uri.EscapeDataString(challenge)}&code_challenge_method=S256");
    }

    public async Task<VerifiedGitHubInstallation?> VerifyInstallationAsync(string code, string verifier, long? installationId, CancellationToken ct)
    {
        using var exchange = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = Setting("ClientId"),
                ["client_secret"] = Setting("ClientSecret"),
                ["code"] = code,
                ["redirect_uri"] = CallbackUri(),
                ["code_verifier"] = verifier
            })
        };
        exchange.Headers.Accept.Add(new("application/json"));
        using var exchanged = await http.SendAsync(exchange, ct);
        var token = await ReadAuthorizationResponse<UserTokenDto>(exchanged, ct);
        if (string.IsNullOrWhiteSpace(token.AccessToken) || !string.Equals(token.TokenType, "bearer", StringComparison.OrdinalIgnoreCase))
            throw new GitHubProblem("GITHUB_INSTALLATION_NOT_AUTHORIZED", 403);

        using var userRequest = Request(HttpMethod.Get, "https://api.github.com/user", token.AccessToken);
        using var userResponse = await http.SendAsync(userRequest, ct);
        var user = await ReadAuthorizationResponse<AccountDto>(userResponse, ct);
        if (user.Id <= 0 || string.IsNullOrWhiteSpace(user.Login))
            throw new GitHubProblem("GITHUB_INSTALLATION_NOT_AUTHORIZED", 403);

        for (var page = 1; page <= 10; page++)
        {
            using var listRequest = Request(HttpMethod.Get, $"https://api.github.com/user/installations?per_page=100&page={page}", token.AccessToken);
            using var listResponse = await http.SendAsync(listRequest, ct);
            var list = await ReadAuthorizationResponse<UserInstallationsDto>(listResponse, ct);
            if (list.Installations is null) throw new GitHubProblem("GITHUB_AUTHORIZATION_UNAVAILABLE", 502);
            // Installation tokens can see every repository granted to the app. Limit linking to the
            // authenticated user's personal installation before switching to installation tokens.
            var installation = list.Installations.FirstOrDefault(item => item.Id > 0 &&
                (installationId is null || item.Id == installationId) && item.Account?.Id == user.Id &&
                item.TargetType == "User" && item.SuspendedAt is null);
            if (installation is not null)
                return new(user.Id, user.Login, installation.Id, user.Id, user.Login, "User",
                    installation.RepositorySelection ?? "selected", installation.Permissions ?? new());
            if (list.Installations.Count < 100)
            {
                if (installationId is not null) throw new GitHubProblem("GITHUB_INSTALLATION_NOT_AUTHORIZED", 403);
                return null;
            }
        }
        throw new GitHubProblem("GITHUB_AUTHORIZATION_UNAVAILABLE", 502);
    }

    private static async Task<T> ReadAuthorizationResponse<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            throw new GitHubProblem("GITHUB_INSTALLATION_NOT_AUTHORIZED", 403);
        if (!response.IsSuccessStatusCode) throw new GitHubProblem("GITHUB_AUTHORIZATION_UNAVAILABLE", 502);
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new GitHubProblem("GITHUB_AUTHORIZATION_UNAVAILABLE", 502);
        }
        catch (JsonException)
        {
            throw new GitHubProblem("GITHUB_AUTHORIZATION_UNAVAILABLE", 502);
        }
    }
    public async Task<GitHubInstallationAccess> CreateInstallationAccessAsync(long installationId, IReadOnlyCollection<long> repositoryIds, CancellationToken ct)
    {
        string jwt;
        try { jwt = AppJwt(); }
        catch (Exception error) when (error is GitHubProblem or ArgumentException or CryptographicException or IOException or UnauthorizedAccessException)
        {
            log.LogWarning("GitHub App credentials are missing or invalid");
            throw new AnalysisFailure("GITHUB_APP_CREDENTIALS", false);
        }
        using var request = Request(HttpMethod.Post, $"https://api.github.com/app/installations/{installationId}/access_tokens", jwt);
        if (repositoryIds.Count > 0) request.Content = JsonContent.Create(new { repository_ids = repositoryIds });
        using var response = await Send(request, "installation_token", ct);
        var value = await Read<InstallationTokenDto>(response, ct);
        if (string.IsNullOrWhiteSpace(value.Token)) throw new AnalysisFailure("GITHUB_INVALID_RESPONSE", false);
        return new(value.Token, value.ExpiresAt);
    }
    public async Task<IReadOnlyList<GitHubRepositoryDescriptor>> ListRepositoriesAsync(long installationId, CancellationToken ct)
    {
        var access = await CreateInstallationAccessAsync(installationId, [], ct); var repositories = new List<GitHubRepositoryDescriptor>();
        for (var page = 1; page <= 10; page++)
        {
            using var response = await Get($"https://api.github.com/installation/repositories?per_page=100&page={page}", access.Token, "installation_repositories", ct);
            var result = await Read<RepositoriesDto>(response, ct);
            if (result.Repositories is null) throw new AnalysisFailure("GITHUB_INVALID_RESPONSE", false);
            repositories.AddRange(result.Repositories.Select(Map));
            if (result.Repositories.Count < 100) break;
        }
        return repositories;
    }
    public async Task<GitHubRepositorySnapshot> ReadSnapshotAsync(long installationId, long repositoryId, CancellationToken ct)
    {
        var access = await CreateInstallationAccessAsync(installationId, [repositoryId], ct);
        using var repositoryResponse = await Get($"https://api.github.com/repositories/{repositoryId}", access.Token, "repository", ct);
        var repository = await Read<RepositoryDto>(repositoryResponse, ct); var descriptor = Map(repository);
        if (string.IsNullOrWhiteSpace(repository.DefaultBranch)) throw new AnalysisFailure("GITHUB_BRANCH_MISSING", false);
        var repoUrl = $"https://api.github.com/repos/{Uri.EscapeDataString(repository.Owner.Login)}/{Uri.EscapeDataString(repository.Name)}";
        var branch = string.Join("/", repository.DefaultBranch.Split('/').Select(Uri.EscapeDataString));
        using var referenceResponse = await Get($"{repoUrl}/git/ref/heads/{branch}", access.Token, "default_branch", ct);
        var revision = (await Read<ReferenceDto>(referenceResponse, ct)).Object?.Sha;
        if (string.IsNullOrWhiteSpace(revision)) throw new AnalysisFailure("GITHUB_INVALID_RESPONSE", false);
        using var commitResponse = await Get($"{repoUrl}/git/commits/{Uri.EscapeDataString(revision)}", access.Token, "commit", ct);
        var treeSha = (await Read<CommitDto>(commitResponse, ct)).Tree?.Sha;
        if (string.IsNullOrWhiteSpace(treeSha)) throw new AnalysisFailure("GITHUB_INVALID_RESPONSE", false);
        using var treeResponse = await Get($"{repoUrl}/git/trees/{Uri.EscapeDataString(treeSha)}?recursive=1", access.Token, "tree", ct);
        var tree = await Read<TreeDto>(treeResponse, ct);
        if (tree.Tree is null) throw new AnalysisFailure("GITHUB_INVALID_RESPONSE", false);
        using var languagesResponse = await Get($"{repoUrl}/languages", access.Token, "languages", ct);
        var languages = await Read<Dictionary<string, long>>(languagesResponse, ct); var warnings = new List<string>();
        var entries = tree.Tree.Take(MaxTreeEntries).ToArray(); if (tree.Truncated || tree.Tree.Count > MaxTreeEntries) warnings.Add("TREE_LIMIT_REACHED");
        var candidates = entries.Where(item => item.Type == "blob" && item.Size is > 0 and <= MaxFileBytes && Valuable(item.Path) && !Ignored(item.Path))
            .OrderBy(item => Priority(item.Path)).ThenBy(item => item.Path, StringComparer.Ordinal).ToArray();
        var files = new List<GitHubSourceFile>(); long total = 0;
        foreach (var item in candidates)
        {
            if (files.Count == MaxFiles) { warnings.Add("FILE_COUNT_LIMIT_REACHED"); break; }
            if (total + item.Size!.Value > MaxTotalBytes) { warnings.Add("TOTAL_BYTES_LIMIT_REACHED"); break; }
            using var blobResponse = await Get($"{repoUrl}/git/blobs/{Uri.EscapeDataString(item.Sha)}", access.Token, "blob", ct);
            var blob = await Read<BlobDto>(blobResponse, ct);
            if (blob.Encoding != "base64" || blob.Content is null) continue;
            byte[] bytes; try { bytes = Convert.FromBase64String(blob.Content.Replace("\n", "", StringComparison.Ordinal)); } catch (FormatException) { continue; }
            if (bytes.Any(value => value == 0)) continue; files.Add(new(item.Path, Encoding.UTF8.GetString(bytes), bytes.LongLength)); total += bytes.LongLength;
        }
        return new(descriptor, revision, languages, files, entries.Length, tree.Truncated || tree.Tree.Count > MaxTreeEntries, candidates.Length, total, warnings);
    }
    public async Task RevokeInstallationAsync(long installationId, CancellationToken ct)
    {
        using var response = await http.SendAsync(Request(HttpMethod.Delete, $"https://api.github.com/app/installations/{installationId}", AppJwt()), ct);
        if (response.StatusCode != HttpStatusCode.NotFound && !response.IsSuccessStatusCode) throw new GitHubProblem("GITHUB_DISCONNECT_FAILED", 502);
    }
    private async Task<HttpResponseMessage> Get(string uri, string token, string operation, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, uri, token);
        return await Send(request, operation, ct);
    }

    private async Task<HttpResponseMessage> Send(HttpRequestMessage request, string operation, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        catch (Exception error) when (error is HttpRequestException || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            log.LogWarning("GitHub request failed: operation={Operation} durationMs={DurationMs} code={Code}",
                operation, Stopwatch.GetElapsedTime(started).TotalMilliseconds, "GITHUB_NETWORK_ERROR");
            throw new AnalysisFailure("GITHUB_NETWORK_ERROR", true);
        }
        if (response.IsSuccessStatusCode) return response;
        using (response)
        {
            var failure = await ProviderFailure(response, operation, request.Content is not null, ct);
            var requestId = Header(response, "X-GitHub-Request-Id");
            if (requestId is not { Length: > 0 and <= 80 } ||
                requestId.Any(character => !char.IsAsciiHexDigit(character) && character != ':'))
                requestId = null;
            log.LogWarning("GitHub request failed: operation={Operation} status={Status} requestId={RequestId} remaining={Remaining} resetEpoch={ResetEpoch} retryNotBefore={RetryNotBefore} durationMs={DurationMs} code={Code}",
                operation, (int)response.StatusCode, requestId,
                int.TryParse(Header(response, "X-RateLimit-Remaining"), out var remaining) ? remaining : (int?)null,
                long.TryParse(Header(response, "X-RateLimit-Reset"), out var reset) ? reset : (long?)null,
                failure.RetryNotBefore, Stopwatch.GetElapsedTime(started).TotalMilliseconds, failure.Code);
            throw failure;
        }
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static async Task<AnalysisFailure> ProviderFailure(HttpResponseMessage response, string operation,
        bool hasRequestBody, CancellationToken ct)
    {
        var status = response.StatusCode;
        var now = DateTimeOffset.UtcNow;
        var retryAfter = response.Headers.RetryAfter?.Date;
        if (response.Headers.RetryAfter?.Delta is { } delay && delay <= DateTimeOffset.MaxValue - now)
            retryAfter = now + delay;
        var remainingExhausted = Header(response, "X-RateLimit-Remaining") == "0";
        if (status == HttpStatusCode.TooManyRequests || (status == HttpStatusCode.Forbidden &&
            (remainingExhausted || retryAfter.HasValue || await HasProviderMessage(response, "secondary rate limit", ct))))
        {
            if (remainingExhausted && long.TryParse(Header(response, "X-RateLimit-Reset"), out var reset) &&
                reset is >= 0 and <= 253402300799)
            {
                var resetTime = DateTimeOffset.FromUnixTimeSeconds(reset);
                if (retryAfter is null || resetTime > retryAfter) retryAfter = resetTime;
            }
            if (retryAfter is null || retryAfter <= now) retryAfter = now.AddMinutes(1);
            return new("GITHUB_RATE_LIMITED", true, retryAfter);
        }
        if ((int)status is >= 500 and < 600) return new("GITHUB_UNAVAILABLE", true, retryAfter);
        if (status == HttpStatusCode.Unauthorized && operation == "installation_token")
            return new("GITHUB_APP_CREDENTIALS", false);
        if (status == HttpStatusCode.Forbidden) return new("GITHUB_PERMISSIONS_INVALID", false);
        if (operation == "installation_token" && status == HttpStatusCode.UnprocessableEntity && hasRequestBody)
            return new("GITHUB_REPOSITORY_NOT_GRANTED", false);
        if (status == HttpStatusCode.Conflict && operation == "default_branch" &&
            await HasProviderMessage(response, "repository is empty", ct))
            return new("GITHUB_REPOSITORY_EMPTY", false);
        if (status == HttpStatusCode.NotFound && operation == "default_branch")
            return new("GITHUB_BRANCH_MISSING", false);
        if (status == HttpStatusCode.NotFound && operation is "commit" or "tree" or "blob")
            return new("GITHUB_OBJECT_MISSING", false);
        if (status == HttpStatusCode.Conflict) return new("GITHUB_REPOSITORY_CONFLICT", false);
        if (status is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized) return new("GITHUB_ACCESS_LOST", false);
        return new("GITHUB_REQUEST_REJECTED", false);
    }

    private static async Task<bool> HasProviderMessage(HttpResponseMessage response, string marker, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            var buffer = new char[2048];
            var length = await reader.ReadBlockAsync(buffer.AsMemory(), ct);
            return buffer.AsSpan(0, length).Contains(marker, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is IOException or HttpRequestException) { return false; }
    }
    private static HttpRequestMessage Request(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new("application/vnd.github+json")); request.Headers.UserAgent.ParseAdd("ProofPath/1.0"); request.Headers.Add("X-GitHub-Api-Version", "2022-11-28"); return request;
    }
    private string AppJwt()
    {
        var now = DateTimeOffset.UtcNow; var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iat = now.AddSeconds(-60).ToUnixTimeSeconds(), exp = now.AddMinutes(9).ToUnixTimeSeconds(), iss = Setting("AppId") }));
        var unsigned = $"{header}.{payload}"; using var rsa = RSA.Create(); rsa.ImportFromPem(PrivateKey());
        return $"{unsigned}.{Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))}";
    }
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new AnalysisFailure("GITHUB_INVALID_RESPONSE", false); }
        catch (JsonException) { throw new AnalysisFailure("GITHUB_INVALID_RESPONSE", false); }
    }
    private static GitHubRepositoryDescriptor Map(RepositoryDto item)
    {
        if (item.Id <= 0 || string.IsNullOrWhiteSpace(item.Owner?.Login) ||
            string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.FullName))
            throw new AnalysisFailure("GITHUB_INVALID_RESPONSE", false);
        return new(item.Id, item.Owner.Login, item.Name, item.FullName, item.Private, item.DefaultBranch ?? "", item.HtmlUrl);
    }
    private static bool Ignored(string path) => path.Replace('\\', '/').Split('/').Any(part => part is "node_modules" or "vendor" or "dist" or "build" or "bin" or "obj" or "coverage" or ".git" or "generated");
    private static bool Valuable(string path)
    {
        var lower = path.ToLowerInvariant(); var name = Path.GetFileName(lower);
        return lower.StartsWith(".github/workflows/") || name is "dockerfile" or "compose.yml" or "compose.yaml" or "package.json" or "pyproject.toml" or "requirements.txt" or "pom.xml" or "build.gradle" || name.StartsWith("readme") ||
            lower.EndsWith(".csproj") || lower.EndsWith(".sln") || lower.EndsWith(".cs") || lower.EndsWith(".ts") || lower.EndsWith(".tsx") || lower.EndsWith(".js") || lower.EndsWith(".jsx") || lower.EndsWith(".py") || lower.EndsWith(".java") || lower.EndsWith(".sql") || lower.EndsWith(".json") || lower.EndsWith(".xml") || lower.EndsWith(".yml") || lower.EndsWith(".yaml");
    }
    private static int Priority(string path) { var lower = path.ToLowerInvariant(); var name = Path.GetFileName(lower); return name is "package.json" or "dockerfile" or "compose.yml" or "compose.yaml" || lower.EndsWith(".csproj") ? 0 : lower.StartsWith(".github/workflows/") ? 1 : name.StartsWith("readme") ? 2 : 3; }
    private sealed record AccountDto(long Id, string Login);
    private sealed record UserTokenDto([property: JsonPropertyName("access_token")] string? AccessToken, [property: JsonPropertyName("token_type")] string? TokenType);
    private sealed record UserInstallationsDto(List<InstallationDto>? Installations);
    private sealed record InstallationDto(long Id, AccountDto? Account, [property: JsonPropertyName("target_type")] string? TargetType,
        [property: JsonPropertyName("repository_selection")] string? RepositorySelection,
        [property: JsonPropertyName("suspended_at")] DateTimeOffset? SuspendedAt, Dictionary<string, string>? Permissions);
    private sealed record InstallationTokenDto(string Token, [property: JsonPropertyName("expires_at")] DateTime ExpiresAt); private sealed record OwnerDto(string Login);
    private sealed record RepositoryDto(long Id, OwnerDto Owner, string Name, [property: JsonPropertyName("full_name")] string FullName, bool Private, [property: JsonPropertyName("default_branch")] string DefaultBranch, [property: JsonPropertyName("html_url")] string HtmlUrl);
    private sealed record RepositoriesDto(List<RepositoryDto> Repositories); private sealed record GitObjectDto(string Sha); private sealed record ReferenceDto(GitObjectDto Object);
    private sealed record GitTreeRefDto(string Sha); private sealed record CommitDto(GitTreeRefDto Tree); private sealed record TreeItemDto(string Path, string Type, string Sha, long? Size);
    private sealed record TreeDto(List<TreeItemDto> Tree, bool Truncated); private sealed record BlobDto(string Content, string Encoding);
}
