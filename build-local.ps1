param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$slnxPath = Join-Path $scriptDir "Akay.Be.slnx"

# Verify Akay.To projects exist
$corePath = Join-Path $scriptDir "..\Akay.To\Akay.To.Core\Akay.To.Core.csproj" | Resolve-Path
$azurePath = Join-Path $scriptDir "..\Akay.To\Akay.To.Azure\Akay.To.Azure.csproj" | Resolve-Path
$rebusPath = Join-Path $scriptDir "..\Akay.To\Akay.To.Messaging.Rebus\Akay.To.Messaging.Rebus.csproj" | Resolve-Path

if (-not (Test-Path $corePath)) {
    Write-Error "Akay.To.Core project not found at: $corePath"
    exit 1
}
if (-not (Test-Path $azurePath)) {
    Write-Error "Akay.To.Azure project not found at: $azurePath"
    exit 1
}
if (-not (Test-Path $rebusPath)) {
    Write-Error "Akay.To.Messaging.Rebus project not found at: $rebusPath"
    exit 1
}

# Load slnx and add Akay.To projects if missing
[xml]$slnx = Get-Content $slnxPath
$ns = New-Object System.Xml.XmlNamespaceManager($slnx.NameTable)
$ns.AddNamespace("s", "http://schemas.microsoft.com/developer/msbuild/2003")

$hasCore = $false
$hasAzure = $false
$hasRebus = $false
foreach ($proj in $slnx.Solution.Project) {
    if ($proj.Path -like "*Akay.To.Core*") { $hasCore = $true }
    if ($proj.Path -like "*Akay.To.Azure*") { $hasAzure = $true }
    if ($proj.Path -like "*Akay.To.Messaging.Rebus*") { $hasRebus = $true }
}

$modified = $false
if (-not $hasCore) {
    $projNode = $slnx.CreateElement("Project")
    $projNode.SetAttribute("Path", "../Akay.To/Akay.To.Core/Akay.To.Core.csproj")
    $slnx.Solution.AppendChild($projNode) | Out-Null
    $modified = $true
    Write-Host "Added Akay.To.Core to solution."
}
if (-not $hasAzure) {
    $projNode = $slnx.CreateElement("Project")
    $projNode.SetAttribute("Path", "../Akay.To/Akay.To.Azure/Akay.To.Azure.csproj")
    $slnx.Solution.AppendChild($projNode) | Out-Null
    $modified = $true
    Write-Host "Added Akay.To.Azure to solution."
}
if (-not $hasRebus) {
    $projNode = $slnx.CreateElement("Project")
    $projNode.SetAttribute("Path", "../Akay.To/Akay.To.Messaging.Rebus/Akay.To.Messaging.Rebus.csproj")
    $slnx.Solution.AppendChild($projNode) | Out-Null
    $modified = $true
    Write-Host "Added Akay.To.Messaging.Rebus to solution."
}

if ($modified) {
    $slnx.Save($slnxPath)
    Write-Host "Solution file updated."
} else {
    Write-Host "Akay.To projects already present in solution."
}

dotnet restore "Akay.Be.slnx" -p:UseLocalAkayTo=true
dotnet build "Akay.Be.slnx" --configuration $Configuration --no-restore -p:UseLocalAkayTo=true
dotnet test "Akay.Be.slnx" --configuration $Configuration --no-build -p:UseLocalAkayTo=true
