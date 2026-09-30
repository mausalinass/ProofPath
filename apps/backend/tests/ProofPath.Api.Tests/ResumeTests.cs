using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Analysis;
using ProofPath.Application.Files;
using ProofPath.Application.Resumes;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Persistence;
using ProofPath.Infrastructure.Resumes;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ProofPath.Api.Tests;

public static class ResumeFixtures
{
    public const string Text = "Engineer at Acme. Built React applications. Mentored teammates. 2021 to 2024.";
    public static byte[] Pdf(string text = Text)
    {
        var builder = new PdfDocumentBuilder(); var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        if (text.Length > 0) page.AddText(text, 12, new PdfPoint(20, 700), builder.AddStandard14Font(Standard14Font.Helvetica));
        return builder.Build();
    }
    public static byte[] Docx(string text = Text, string? unsafeName = null)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string content) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(content); }
            Add("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Add("word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>" + System.Security.SecurityElement.Escape(text) + "</w:t></w:r></w:p></w:body></w:document>");
            if (unsafeName is not null) Add(unsafeName, "unsafe");
        }
        return stream.ToArray();
    }
    public static ResumeDraft Draft(DocumentText source)
    {
        var block = source.Blocks[0];
        return new ResumeDraft([new FactDraft("Experience", "Engineer", "Acme", "Built React applications.", "2021", "2024", null, block.Id, block.Text)],
            [new SkillDraft("React", "ExperienceStatement", block.Id, "Built React applications.")],
            [new BehaviorDraft("MENTORING", "Mentored teammates.", block.Id, "Mentored teammates.")]);
    }
}

public sealed class DocumentParserTests
{
    private readonly DocumentTextExtractor parser = new();
    [Fact] public void PdfAndDocxProduceGroundedSourceBlocks()
    {
        var pdf = parser.Extract(ResumeFixtures.Pdf(), DocumentTextExtractor.Pdf, default);
        Assert.Contains("Engineer", pdf.Blocks[0].Text); Assert.Equal(1, pdf.Blocks[0].Page);
        var docx = parser.Extract(ResumeFixtures.Docx(), DocumentTextExtractor.Docx, default);
        Assert.Equal(ResumeFixtures.Text, docx.Blocks[0].Text); Assert.Contains("p1", docx.Blocks[0].Id);
    }
    [Fact] public void ImageOnlyPdfFailsWithActionableCode()
    {
        Assert.Equal("NO_EXTRACTABLE_TEXT", Assert.Throws<AnalysisFailure>(() => parser.Extract(ResumeFixtures.Pdf(""), DocumentTextExtractor.Pdf, default)).Code);
    }
    [Theory]
    [InlineData("word/vbaProject.bin")]
    [InlineData("word/embeddings/object.bin")]
    [InlineData("../escape")]
    public void RejectsUnsafeDocx(string name) => Assert.Throws<ResumeProblem>(() => parser.Validate(ResumeFixtures.Docx(unsafeName: name), "resume.docx", DocumentTextExtractor.Docx));
    [Fact] public void RejectsMimeMismatchCorruptPdfAndOversize()
    {
        Assert.Throws<ResumeProblem>(() => parser.Validate(ResumeFixtures.Docx(), "resume.pdf", DocumentTextExtractor.Pdf));
        Assert.Throws<ResumeProblem>(() => parser.Validate(Encoding.UTF8.GetBytes("%PDF-not-a-document"), "resume.pdf", DocumentTextExtractor.Pdf));
        Assert.Equal(413, Assert.Throws<ResumeProblem>(() => parser.Validate(new byte[ResumeLimits.MaxBytes + 1], "resume.pdf", DocumentTextExtractor.Pdf)).Status);
    }
    [Fact] public void GroundingRejectsInventedDatesAndInvalidThemeOrScore()
    {
        var source = parser.Extract(ResumeFixtures.Docx(), DocumentTextExtractor.Docx, default); var draft = ResumeFixtures.Draft(source);
        ResumeValidation.Validate(draft, source, true);
        Assert.Throws<AnalysisFailure>(() => ResumeValidation.Validate(draft with { Facts = [draft.Facts[0] with { StartDateText = "2015" }] }, source, true));
        Assert.Throws<AnalysisFailure>(() => ResumeValidation.Validate(draft with { Behaviors = [draft.Behaviors[0] with { ThemeKey = "PERSONALITY" }] }, source, true));
        Assert.Throws<JsonException>(() => ResumeJson.Read<ResumeDraft>("{\"facts\":[],\"skills\":[],\"behaviors\":[],\"score\":100}"));
    }
}

[Collection("PostgreSQL API")]
public sealed class ResumeWorkflowTests(ApiFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProofPathDbContext>().AnalysisJobs.ExecuteDeleteAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private const string Password = "Resume-tests.P4ssword!";
    private static async Task<HttpResponseMessage> Write(HttpClient client, HttpMethod method, string path, object? body)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(method, path) { Content = body is HttpContent content ? content : body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString()); return await client.SendAsync(request);
    }
    private async Task<HttpClient> User()
    {
        var client = fixture.Client(); var email = $"{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Created, (await Write(client, HttpMethod.Post, "/api/v1/auth/register", new { email, password = Password, firstName = "Ada" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Write(client, HttpMethod.Post, "/api/v1/auth/login", new { email, password = Password })).StatusCode); return client;
    }
    private static MultipartFormDataContent File(byte[]? bytes = null)
    {
        var form = new MultipartFormDataContent(); var part = new ByteArrayContent(bytes ?? ResumeFixtures.Docx());
        part.Headers.ContentType = new MediaTypeHeaderValue(DocumentTextExtractor.Docx); form.Add(part, "file", "resume.docx"); return form;
    }
    private static async Task<ResumeUploadResult> Upload(HttpClient client)
    {
        var response = await Write(client, HttpMethod.Post, "/api/v1/resumes/", File());
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ResumeUploadResult>())!;
    }
    private async Task Complete(ResumeUploadResult upload)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var queue = services.GetRequiredService<IAnalysisQueue>(); var lease = (await queue.ClaimAsync(default))!;
        Assert.Equal(upload.AnalysisJobId, lease.Id);
        var handler = new ResumeAnalysisHandler(services.GetRequiredService<IResumeInputReader>(), services.GetRequiredService<IPrivateFileStore>(),
            services.GetRequiredService<IDocumentTextExtractor>(), new FixtureProvider());
        var result = await handler.ProcessAsync(lease, default); Assert.True(await queue.CompleteAsync(lease, result, default));
    }
    private sealed class FixtureProvider : ILlmProvider
    {
        public Task<LlmResumeResult> ExtractResumeAsync(DocumentText source, CancellationToken ct) =>
            Task.FromResult(new LlmResumeResult(ResumeFixtures.Draft(source), "fixture-only", 100, 100, 1));
    }
    [Fact] public async Task UploadReviewConfirmIsIdempotentAndReplacementPreservesHistory()
    {
        using var client = await User(); var first = await Upload(client); await Complete(first);
        var path = $"/api/v1/resumes/{first.Id}/extraction";
        var review = (await client.GetFromJsonAsync<ResumeReview>(path, ResumeJson.Options))!;
        Assert.Null(review.ConfirmedAt);
        var corrected = review.Draft with { Facts = [review.Draft.Facts[0] with { Name = "Senior engineer" }] };
        Assert.Equal(HttpStatusCode.OK, (await Write(client, HttpMethod.Put, path, new { revision = 1, draft = corrected })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, HttpMethod.Put, path, new { revision = 1, draft = corrected })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Write(client, HttpMethod.Post, $"/api/v1/resumes/{first.Id}/confirm", new { revision = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Write(client, HttpMethod.Post, $"/api/v1/resumes/{first.Id}/confirm", new { revision = 2 })).StatusCode);
        var second = await Upload(client); await Complete(second);
        Assert.Equal(HttpStatusCode.NoContent, (await Write(client, HttpMethod.Post, $"/api/v1/resumes/{second.Id}/confirm", new { revision = 1 })).StatusCode);
        await Write(client, HttpMethod.Post, $"/api/v1/resumes/{first.Id}/confirm", new { revision = 2 });
        var versions = (await client.GetFromJsonAsync<ResumeSummary[]>("/api/v1/resumes/"))!;
        Assert.Equal(2, versions.Length); Assert.Equal(second.Id, Assert.Single(versions, item => item.Active).Id);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        var extraction = await db.ResumeExtractions.SingleAsync(item => item.ResumeId == first.Id);
        var experience = await db.Experiences.SingleAsync(item => item.ResumeExtractionId == extraction.Id);
        Assert.True(experience.UserCorrected); Assert.Equal("Senior engineer", experience.Name);
        Assert.Equal("Engineer", ResumeJson.Read<ResumeAnalysisResult>(extraction.MachineJson).Draft.Facts[0].Name);
        Assert.Equal("react", (await db.EvidenceItems.SingleAsync(item => item.ResumeExtractionId == extraction.Id)).SkillId);
    }
    [Fact] public async Task OwnershipCsrfAndDeletionCoverFilesAndDerivedData()
    {
        using var a = await User(); using var b = await User(); using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsync("/api/v1/resumes/", File())).StatusCode);
        var upload = await Upload(a); await Complete(upload);
        foreach (var suffix in new[] { "", "/download", "/extraction" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/resumes/{upload.Id}{suffix}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/resumes/{upload.Id}{suffix}")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await Write(b, HttpMethod.Post, $"/api/v1/resumes/{upload.Id}/confirm", new { revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/v1/resumes/{upload.Id}/download")).StatusCode);
        await Write(a, HttpMethod.Post, $"/api/v1/resumes/{upload.Id}/confirm", new { revision = 1 });
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        var resume = await db.Resumes.AsNoTracking().SingleAsync(item => item.Id == upload.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await Write(a, HttpMethod.Delete, "/api/v1/account", new { confirm = true, password = Password })).StatusCode);
        Assert.False(await db.Resumes.AnyAsync(item => item.Id == upload.Id));
        Assert.False(await db.Experiences.AnyAsync(item => item.CandidateProfileId == resume.CandidateProfileId));
        Assert.False(await db.EvidenceItems.AnyAsync(item => item.CandidateProfileId == resume.CandidateProfileId));
        await scope.ServiceProvider.GetRequiredService<IPrivateFileCleanup>().RunAsync(default);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IPrivateFileStore>().OpenReadAsync(resume.StorageKey, default));
    }
    [Fact] public async Task ProviderFailurePreservesDownloadWithoutPublishingFacts()
    {
        using var client = await User(); var upload = await Upload(client);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var queue = scope.ServiceProvider.GetRequiredService<IAnalysisQueue>();
        var lease = (await queue.ClaimAsync(default))!;
        Assert.True(await queue.FailAsync(lease, "PROVIDER_NOT_CONFIGURED", true, default));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/resumes/{upload.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/resumes/{upload.Id}/extraction")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Write(client, HttpMethod.Post, $"/api/v1/resumes/{upload.Id}/confirm", new { revision = 1 })).StatusCode);
    }
    [Fact] public async Task CorruptUploadCreatesNoMetadata()
    {
        using var client = await User();
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, HttpMethod.Post, "/api/v1/resumes/", File([1, 2, 3]))).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<ResumeSummary[]>("/api/v1/resumes/"))!);
    }
}
