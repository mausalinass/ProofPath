param(
  [Parameter(Mandatory=$true)][string]$BaseUrl,
  [Parameter(Mandatory=$true)][string]$Email,
  [Parameter(Mandatory=$true)][SecureString]$Password
)
$ErrorActionPreference = 'Stop'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$plain = [System.Net.NetworkCredential]::new('', $Password).Password
function Write-Api([string]$method, [string]$path, [hashtable]$body) {
  $csrf = Invoke-RestMethod "$BaseUrl/api/v1/auth/csrf" -WebSession $session
  Invoke-RestMethod "$BaseUrl$path" -Method $method -WebSession $session -Headers @{ 'X-CSRF-TOKEN' = $csrf.token } -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 8)
}
try { Write-Api POST '/api/v1/auth/register' @{ email=$Email; password=$plain; firstName='Ada'; lastName='Demo' } | Out-Null }
catch { Write-Api POST '/api/v1/auth/login' @{ email=$Email; password=$plain } | Out-Null }
Write-Api PUT '/api/v1/profile' @{ firstName='Ada'; lastName='Demo'; headline='Backend Engineer'; location='Remote'; workAuthorization='Authorized'; educationSummary='Computer Science' } | Out-Null
$job = Write-Api POST '/api/v1/jobs/' @{ company='Northstar Labs'; title='Backend Engineer'; sourceUrl='https://example.com/demo-role'; description='Build reliable APIs with C#, PostgreSQL, Docker, GitHub Actions, and cloud observability. Collaborate with product teams and own production delivery.' }
Write-Output "Demo profile and job created. Job id: $($job.job.id)"
$plain = $null
