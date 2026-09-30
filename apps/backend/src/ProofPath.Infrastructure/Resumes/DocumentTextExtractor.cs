using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ProofPath.Application.Analysis;
using ProofPath.Application.Files;
using ProofPath.Application.Resumes;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ProofPath.Infrastructure.Resumes;

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    public const string Pdf = "application/pdf";
    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public void Validate(byte[] content, string fileName, string contentType)
    {
        if (content.Length == 0) throw new ResumeProblem("EMPTY_FILE");
        if (content.LongLength > ResumeLimits.MaxBytes) throw new ResumeProblem("FILE_TOO_LARGE", 413);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".pdf" && contentType == Pdf && content.AsSpan().StartsWith("%PDF-"u8))
        {
            // A coarse additional rejection, not a claim of antivirus coverage.
            var syntax = Encoding.Latin1.GetString(content);
            if (new[] { "/JavaScript", "/JS", "/Launch", "/EmbeddedFile", "/Encrypt", "/RichMedia", "/XFA" }
                .Any(token => syntax.Contains(token, StringComparison.Ordinal))) throw new ResumeProblem("ACTIVE_OR_ENCRYPTED_PDF");
            try { using var pdf = PdfDocument.Open(content); if (pdf.NumberOfPages > 100) throw new ResumeProblem("DOCUMENT_TOO_COMPLEX"); }
            catch (ResumeProblem) { throw; }
            catch (Exception) { throw new ResumeProblem("INVALID_PDF"); }
            return;
        }
        if (extension == ".docx" && contentType == Docx && content.AsSpan().StartsWith("PK\x03\x04"u8))
        {
            try { using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read); ValidateArchive(zip); }
            catch (ResumeProblem) { throw; }
            catch (Exception) { throw new ResumeProblem("INVALID_DOCX"); }
            return;
        }
        throw new ResumeProblem("UNSUPPORTED_FILE_TYPE");
    }
    private static void ValidateArchive(ZipArchive zip)
    {
        if (zip.Entries.Count > 1000 || zip.Entries.Sum(entry => entry.Length) > 40 * 1024 * 1024 ||
            zip.Entries.Any(entry => entry.Length > 10 * 1024 * 1024 || entry.FullName.Contains("..") ||
                entry.FullName.Contains('\\') || entry.FullName.StartsWith('/') ||
                entry.FullName.Contains("vba", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.Contains("embeddings/", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.Contains("activeX", StringComparison.OrdinalIgnoreCase))) throw new ResumeProblem("UNSAFE_DOCX_CONTAINER");
        if (zip.Entries.GroupBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new ResumeProblem("INVALID_DOCX");
        var types = ReadXml(zip.GetEntry("[Content_Types].xml") ?? throw new ResumeProblem("INVALID_DOCX"));
        if (!types.Descendants().Any(node => (string?)node.Attribute("PartName") == "/word/document.xml" &&
            (string?)node.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"))
            throw new ResumeProblem("INVALID_DOCX");
        if (types.ToString().Contains("macroEnabled", StringComparison.OrdinalIgnoreCase)) throw new ResumeProblem("UNSAFE_DOCX_CONTAINER");
        _ = ReadXml(zip.GetEntry("word/document.xml") ?? throw new ResumeProblem("INVALID_DOCX"));
        foreach (var relationship in zip.Entries.Where(entry => entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
        {
            var xml = ReadXml(relationship);
            // Hyperlinks are inert text metadata; linked templates/objects may not be fetched.
            if (xml.Descendants().Any(node => (string?)node.Attribute("TargetMode") == "External" &&
                !((string?)node.Attribute("Type") ?? "").EndsWith("/hyperlink", StringComparison.Ordinal)))
                throw new ResumeProblem("UNSAFE_DOCX_CONTAINER");
        }
    }
    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 10 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    public DocumentText Extract(byte[] content, string contentType, CancellationToken ct)
    {
        try
        {
            Validate(content, contentType == Pdf ? "resume.pdf" : "resume.docx", contentType);
            var blocks = new List<SourceBlock>(); var warnings = new List<string>();
            if (contentType == Pdf)
            {
                using var pdf = PdfDocument.Open(content);
                foreach (var page in pdf.GetPages())
                {
                    ct.ThrowIfCancellationRequested();
                    var text = ContentOrderTextExtractor.GetText(page).Trim();
                    if (text.Length > 0) blocks.Add(new SourceBlock($"page-{page.Number}", page.Number, text));
                    else warnings.Add($"Page {page.Number} contains no extractable text. Image content was not analyzed.");
                }
                warnings.Add("Review PDF reading order, especially multi-column layouts. Images were not analyzed.");
            }
            else
            {
                using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
                foreach (var entry in zip.Entries.Where(entry => entry.FullName == "word/document.xml" ||
                    entry.FullName.StartsWith("word/header", StringComparison.Ordinal) && entry.FullName.EndsWith(".xml") ||
                    entry.FullName.StartsWith("word/footer", StringComparison.Ordinal) && entry.FullName.EndsWith(".xml"))
                    .OrderBy(entry => entry.FullName, StringComparer.Ordinal))
                {
                    ct.ThrowIfCancellationRequested(); var index = 0;
                    foreach (var paragraph in ReadXml(entry).Descendants(W + "p"))
                    {
                        index++;
                        var text = string.Concat(paragraph.Descendants().Where(node => node.Name == W + "t" || node.Name == W + "tab" || node.Name == W + "br")
                            .Select(node => node.Name == W + "t" ? node.Value : " ")).Trim();
                        if (text.Length > 0) blocks.Add(new SourceBlock($"{entry.FullName}:p{index}", null, text));
                    }
                }
                if (zip.Entries.Any(entry => entry.FullName.StartsWith("word/media/", StringComparison.Ordinal)))
                    warnings.Add("Images were not analyzed; review the extracted text for missing information.");
            }
            if (blocks.Count == 0) throw new AnalysisFailure("NO_EXTRACTABLE_TEXT", false);
            if (blocks.Count > 2000 || blocks.Sum(block => block.Text.Length) > 120000)
                throw new AnalysisFailure("DOCUMENT_TEXT_TOO_LONG", false);
            return new DocumentText(blocks.ToArray(), warnings.Distinct().ToArray());
        }
        catch (OperationCanceledException) { throw; }
        catch (AnalysisFailure) { throw; }
        catch (ResumeProblem problem) { throw new AnalysisFailure(problem.Code, false); }
        catch (Exception) { throw new AnalysisFailure("DOCUMENT_EXTRACTION_FAILED", false); }
    }
}
