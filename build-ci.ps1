param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$slnxPath = Join-Path $scriptDir "Akay.Be.slnx"

# Load slnx and remove Akay.To projects if present
[xml]$slnx = Get-Content $slnxPath

$nodesToRemove = @()
foreach ($proj in $slnx.Solution.Project) {
    if ($proj.Path -like "*Akay.To*") {
        $nodesToRemove += $proj
    }
}

if ($nodesToRemove.Count -gt 0) {
    foreach ($node in $nodesToRemove) {
        $slnx.Solution.RemoveChild($node) | Out-Null
    }
    $slnx.Save($slnxPath)
    Write-Host "Removed $($nodesToRemove.Count) Akay.To project(s) from solution."
} else {
    Write-Host "No Akay.To projects in solution."
}

dotnet restore "Akay.Be.slnx" -p:UseLocalAkayTo=false
dotnet build "Akay.Be.slnx" --configuration $Configuration --no-restore -p:UseLocalAkayTo=false
dotnet test "Akay.Be.slnx" --configuration $Configuration --no-build -p:UseLocalAkayTo=false
