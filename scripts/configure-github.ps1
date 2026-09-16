# Run from the repository root while gh is authenticated as the repository owner.
# Apply after the first successful deployment; main will then require pull requests.
$ErrorActionPreference = 'Stop'
$repo = 'N8DaGr822/PhilosophyLab'

function Invoke-GitHub([string]$Endpoint, [string]$Method = 'GET', [object]$Body = $null) {
    if ($null -eq $Body) {
        $response = & gh api --method $Method $Endpoint
    }
    else {
        $jsonBody = $Body | ConvertTo-Json -Depth 12 -Compress
        $response = $jsonBody | & gh api --method $Method $Endpoint --input -
    }
    if ($LASTEXITCODE -ne 0) { throw "GitHub rejected $Method $Endpoint. No further settings were changed." }
    if ($response) { return ($response | ConvertFrom-Json) }
}

$repository = Invoke-GitHub "repos/$repo"
if (-not $repository.permissions.admin) {
    throw 'Sign gh into N8DaGr822 (the owner) before running this script. Collaborator write access is insufficient.'
}

$null = Invoke-GitHub "repos/$repo/vulnerability-alerts" 'PUT'
$null = Invoke-GitHub "repos/$repo/automated-security-fixes" 'PUT'
$null = Invoke-GitHub "repos/$repo/private-vulnerability-reporting" 'PUT'
$null = Invoke-GitHub "repos/$repo" 'PATCH' @{
    security_and_analysis = @{
        secret_scanning = @{ status = 'enabled' }
        secret_scanning_push_protection = @{ status = 'enabled' }
    }
}
$null = Invoke-GitHub "repos/$repo/branches/main/protection" 'PUT' @{
    required_status_checks = @{ strict = $true; contexts = @('Validate Release') }
    enforce_admins = $true
    required_pull_request_reviews = @{ dismiss_stale_reviews = $true; required_approving_review_count = 0 }
    restrictions = $null
    required_conversation_resolution = $true
    allow_force_pushes = $false
    allow_deletions = $false
}
$null = Invoke-GitHub "repos/$repo/environments/github-pages" 'PUT' @{
    deployment_branch_policy = @{ protected_branches = $true; custom_branch_policies = $false }
}
Write-Output "Security settings and main-branch protection enabled for $repo. Future changes require a pull request and passing Validate Release."
