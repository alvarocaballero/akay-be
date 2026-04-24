param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

dotnet restore "Akay.Be.slnx" -p:UseLocalAkayTo=true
dotnet build "Akay.Be.slnx" --configuration $Configuration --no-restore -p:UseLocalAkayTo=true
dotnet test "Akay.Be.slnx" --configuration $Configuration --no-build -p:UseLocalAkayTo=true
