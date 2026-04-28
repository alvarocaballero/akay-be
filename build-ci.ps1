param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

dotnet restore "Akay.Be.slnx" -p:UseLocalAkayTo=false
dotnet build "Akay.Be.slnx" --configuration $Configuration --no-restore -p:UseLocalAkayTo=false
dotnet test "Akay.Be.slnx" --configuration $Configuration --no-build -p:UseLocalAkayTo=false
