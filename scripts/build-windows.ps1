param([string]$Runtime = "win-x64")
$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)
[xml]$project = Get-Content src/MechToolbox.Desktop/MechToolbox.Desktop.csproj
$version = $project.Project.PropertyGroup.Version
dotnet run --project tests/MechToolbox.Checks -c Release
if ($LASTEXITCODE -ne 0) { throw "Core checks failed" }
foreach ($kind in @("portable", "lite")) {
    $folder = "dist/$Runtime-$kind"
    if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
    if ($kind -eq "portable") {
        dotnet publish src/MechToolbox.Desktop -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $folder
    } else {
        dotnet publish src/MechToolbox.Desktop -c Release -r $Runtime --self-contained false -p:DebugType=None -o $folder
    }
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $kind" }
    Copy-Item README.md "$folder/README.md"
    Copy-Item LICENSE, THIRD_PARTY_NOTICES.md $folder
    if ($kind -eq "portable") {
        New-Item "$folder/licenses" -ItemType Directory -Force | Out-Null
        Copy-Item licenses/dotnet "$folder/licenses/dotnet" -Recurse -Force
    }
    New-Item "$folder/Assets" -ItemType Directory -Force | Out-Null
    Copy-Item src/MechToolbox.Desktop/Assets/toolbox.ico "$folder/Assets/toolbox.ico"
    [IO.File]::WriteAllText((Join-Path (Resolve-Path $folder) "Install-CurrentUser.ps1"), [IO.File]::ReadAllText((Join-Path $PSScriptRoot "install-current-user.ps1")), [Text.UTF8Encoding]::new($true))
    Compress-Archive -Path "$folder/*" -DestinationPath "dist/MechToolbox-$version-$Runtime-$kind.zip" -Force
}
Get-FileHash "dist/*.zip" -Algorithm SHA256
