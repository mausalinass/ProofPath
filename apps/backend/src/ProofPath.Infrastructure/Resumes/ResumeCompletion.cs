using Microsoft.EntityFrameworkCore;
using ProofPath.Application.Analysis;
using ProofPath.Application.Resumes;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Infrastructure.Resumes;

public sealed class ResumeCompletion(ProofPathDbContext database) : IAnalysisCompletion
{
    public async Task SaveAsync(AnalysisLease lease, AnalysisOutput output, CancellationToken ct)
    {
        if (lease.Kind != AnalysisKind.Resume) return;
        var resume = await database.Resumes.SingleOrDefaultAsync(item => item.Id == lease.ResourceId && item.CandidateProfileId == lease.CandidateProfileId, ct);
        if (resume is null) return;
        var result = ResumeJson.Read<ResumeAnalysisResult>(output.ResultJson);
        ResumeValidation.Validate(result.Draft, result.Source, machine: true);
        database.ResumeExtractions.Add(new ResumeExtraction { Id = Guid.NewGuid(), ResumeId = resume.Id, MachineJson = output.ResultJson,
            DraftJson = ResumeJson.Serialize(result.Draft), Model = result.Model, CreatedAt = DateTime.UtcNow,
            PromptVersion = result.PromptVersion, SchemaVersion = result.SchemaVersion, ExtractionVersion = result.ExtractionVersion });
        resume.Status = "ReadyForReview"; await database.SaveChangesAsync(ct);
    }
}
