param(
    [Parameter(Mandatory = $true)][string]$Email,
    [Parameter(Mandatory = $true)][string]$Password,
    [Parameter(Mandatory = $true)][string]$ResumePath,
    [string]$BaseUrl = "https://d27i0fde08ymkz.cloudfront.net"
)

$ErrorActionPreference = "Stop"
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.UseCookies = $true
$client = [System.Net.Http.HttpClient]::new($handler)
$client.BaseAddress = [Uri]$BaseUrl

function Read-Json([System.Net.Http.HttpResponseMessage]$response) {
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) {
        throw "HTTP $([int]$response.StatusCode) $($response.RequestMessage.RequestUri.AbsolutePath)"
    }
    if ([string]::IsNullOrWhiteSpace($body)) { return $null }
    return $body | ConvertFrom-Json
}

function Send-Json([string]$method, [string]$path, $body = $null) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::$method, $path)
    if ($script:csrf) { $request.Headers.Add("X-CSRF-TOKEN", $script:csrf) }
    if ($null -ne $body) {
        $json = $body | ConvertTo-Json -Depth 30 -Compress
        $request.Content = [System.Net.Http.StringContent]::new($json, [Text.Encoding]::UTF8, "application/json")
    }
    return Read-Json ($client.SendAsync($request).GetAwaiter().GetResult())
}

function Send-Upload([byte[]]$bytes, [string]$fileName) {
    $multipart = [System.Net.Http.MultipartFormDataContent]::new()
    $file = [System.Net.Http.ByteArrayContent]::new($bytes)
    $file.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/pdf")
    $multipart.Add($file, "file", $fileName)
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "/api/v1/resumes/")
    $request.Headers.Add("X-CSRF-TOKEN", $script:csrf)
    $request.Content = $multipart
    return $client.SendAsync($request).GetAwaiter().GetResult()
}

function Wait-Analysis([string]$id, [int]$seconds = 300) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    do {
        $status = Send-Json Get "/api/v1/analysis-jobs/$id"
        if ($status.state -in @("Completed", "PartiallyCompleted", "Failed", "Cancelled")) { return $status }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out waiting for analysis job."
}

function New-EicarPdfBytes {
    $signature = 'X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*'
    $content = 'BT /F1 12 Tf 72 720 Td (Synthetic antivirus validation document) Tj ET'
    $objects = @(
        "1 0 obj`n<< /Type /Catalog /Pages 2 0 R >>`nendobj`n",
        "2 0 obj`n<< /Type /Pages /Kids [3 0 R] /Count 1 >>`nendobj`n",
        "3 0 obj`n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>`nendobj`n",
        "4 0 obj`n<< /Length $($content.Length) >>`nstream`n$content`nendstream`nendobj`n",
        "5 0 obj`n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>`nendobj`n",
        "6 0 obj`n<< /Length $($signature.Length) >>`nstream`n$signature`nendstream`nendobj`n"
    )
    $document = "%PDF-1.4`n"
    $offsets = [Collections.Generic.List[int]]::new()
    foreach ($object in $objects) {
        $offsets.Add([Text.Encoding]::ASCII.GetByteCount($document))
        $document += $object
    }
    $xref = [Text.Encoding]::ASCII.GetByteCount($document)
    $document += "xref`n0 7`n0000000000 65535 f `n"
    foreach ($offset in $offsets) { $document += ('{0:D10} 00000 n ' -f $offset) + "`n" }
    $document += "trailer`n<< /Size 7 /Root 1 0 R >>`nstartxref`n$xref`n%%EOF`n"
    return [Text.Encoding]::ASCII.GetBytes($document)
}

try {
    $script:csrf = (Send-Json Get "/api/v1/auth/csrf").token
    Send-Json Post "/api/v1/auth/login" @{ email = $Email; password = $Password; rememberMe = $false } | Out-Null
    $script:csrf = (Send-Json Get "/api/v1/auth/csrf").token

    $cleanBytes = [IO.File]::ReadAllBytes($ResumePath)
    $unsafeBytes = New-EicarPdfBytes
    $unsafe = Send-Upload $unsafeBytes "synthetic-eicar-resume.pdf"
    "malware_upload_status=$([int]$unsafe.StatusCode)"
    if ([int]$unsafe.StatusCode -ne 422) { throw "Malware test file was not rejected with 422." }

    $uploadResponse = Send-Upload $cleanBytes "synthetic-session3-resume.pdf"
    $upload = Read-Json $uploadResponse
    "resume_upload_status=$([int]$uploadResponse.StatusCode)"
    $resumeJob = Wait-Analysis $upload.analysisJobId
    "resume_analysis=$($resumeJob.state);attempts=$($resumeJob.attempts)"
    if ($resumeJob.state -notin @("Completed", "PartiallyCompleted")) { throw "Resume analysis did not complete." }
    $resumeReview = Send-Json Get "/api/v1/resumes/$($upload.id)/extraction"
    "resume_review_revision=$($resumeReview.revision);facts=$($resumeReview.draft.facts.Count);skills=$($resumeReview.draft.skills.Count)"
    Send-Json Post "/api/v1/resumes/$($upload.id)/confirm" @{ revision = $resumeReview.revision } | Out-Null
    "resume_confirmed=true"

    $description = @"
Synthetic Backend Software Engineer role. Required: 3+ years building production services with C# and ASP.NET Core; PostgreSQL and Entity Framework Core; REST API design; automated tests with xUnit; Docker containers; GitHub Actions continuous integration; AWS deployment experience. Preferred: React and TypeScript, security threat modeling, observability, and clear technical documentation. The engineer collaborates across product and operations, owns delivery, reviews incidents, and communicates tradeoffs.
"@
    $created = Send-Json Post "/api/v1/jobs/" @{ company = "Synthetic Systems"; title = "Backend Software Engineer"; description = $description.Trim(); sourceUrl = $null }
    "job_created=true"
    $jobId = $created.job.id
    $jobAnalysis = Wait-Analysis $created.analysisJobId
    "job_analysis=$($jobAnalysis.state);attempts=$($jobAnalysis.attempts);error=$($jobAnalysis.errorCode);retryable=$($jobAnalysis.retryable)"
    if ($jobAnalysis.state -eq "Failed" -and $jobAnalysis.retryable) {
        Send-Json Post "/api/v1/analysis-jobs/$($created.analysisJobId)/retry" | Out-Null
        $jobAnalysis = Wait-Analysis $created.analysisJobId
        "job_retry=$($jobAnalysis.state);attempts=$($jobAnalysis.attempts);error=$($jobAnalysis.errorCode)"
    }
    if ($jobAnalysis.state -notin @("Completed", "PartiallyCompleted")) { throw "Job analysis did not complete." }
    $jobReview = Send-Json Get "/api/v1/jobs/$jobId/requirements"
    "job_review_revision=$($jobReview.revision);requirements=$($jobReview.draft.requirements.Count)"
    Send-Json Post "/api/v1/jobs/$jobId/confirm" @{ revision = $jobReview.revision } | Out-Null
    "job_confirmed=true"

    $match = Send-Json Post "/api/v1/jobs/$jobId/matches/" @{}
    "match_status=$($match.result.status);classification=$($match.result.classification);score=$($match.result.overallScore);coverage=$($match.result.evaluationCoverage)"
    "match_requirements=$($match.result.requirements.Count);gaps=$($match.result.gaps.Count)"
    $recommendations = @(Send-Json Get "/api/v1/jobs/$jobId/recommendations")
    "recommendations=$($recommendations.Count)"
    if ($recommendations.Count -gt 0) {
        $updated = Send-Json Put "/api/v1/jobs/$jobId/recommendations/$($recommendations[0].id)" @{ status = "InProgress" }
        "recommendation_status=$($updated.status)"
    }
    $tracking = Send-Json Put "/api/v1/jobs/$jobId/tracking" @{ stage = "Applied"; notes = "Synthetic Session 3 validation only."; nextActionAt = "2026-10-30T00:00:00Z" }
    "tracking_stage=$($tracking.stage)"
    $history = @(Send-Json Get "/api/v1/jobs/$jobId/score-history")
    "score_history=$($history.Count)"

    $cancelledCreate = Send-Json Post "/api/v1/jobs/" @{ company = "Synthetic Cancellation"; title = "Cancellation Check"; description = ($description.Trim() + " This duplicate synthetic role validates cancellation behavior without real data."); sourceUrl = $null }
    Send-Json Post "/api/v1/analysis-jobs/$($cancelledCreate.analysisJobId)/cancel" | Out-Null
    $cancelled = Send-Json Get "/api/v1/analysis-jobs/$($cancelledCreate.analysisJobId)"
    "cancelled_job_state=$($cancelled.state)"

    $githubEvidence = @(Send-Json Get "/api/v1/github/evidence")
    "github_evidence=$($githubEvidence.Count)"
    "journey=completed"
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
