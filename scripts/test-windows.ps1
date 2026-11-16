$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)
dotnet run --project tests/MechToolbox.Checks -c Release
if ($LASTEXITCODE -ne 0) { throw "Core checks failed" }
dotnet publish src/MechToolbox.Desktop -c Release -r win-x64 --self-contained false -p:DebugType=None -o artifacts/ui-app
if ($LASTEXITCODE -ne 0) { throw "Desktop build failed" }
$report = Join-Path (Get-Location) "artifacts/ui-smoke"
if (Test-Path $report) { Remove-Item $report -Recurse -Force }
$process = Start-Process -FilePath "artifacts/ui-app/MechToolbox.exe" -ArgumentList @("--smoke-test", "`"$report`"") -PassThru
if (-not $process.WaitForExit(60000)) {
    $process.Kill()
    throw "UI smoke test timed out"
}
if ($process.ExitCode -ne 0 -or -not (Test-Path "$report/PASS.txt")) {
    if (Test-Path "$report/FAILED.txt") { Get-Content "$report/FAILED.txt" }
    throw "UI smoke test failed"
}
Get-Content "$report/PASS.txt"
