# Direct update to the existing current-user installation; no backup is created.
$ErrorActionPreference = 'Stop'
$sourceFolder = $PSScriptRoot
$installFolder = Join-Path $env:LOCALAPPDATA 'Programs/MechToolbox'
$exe = Join-Path $installFolder 'MechToolbox.exe'
if (-not (Test-Path (Join-Path $sourceFolder 'data/circlips-gb.v1.json'))) { throw 'Run the installer from the fully extracted MechToolbox package.' }
foreach ($process in @(Get-Process MechToolbox -ErrorAction SilentlyContinue)) {
    $process.Kill()
    $process.WaitForExit()
    $process.Dispose()
}
New-Item $installFolder -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $sourceFolder '*') $installFolder -Recurse -Force
# v0.4.0 migrates to manual-section schema v2; retire the unused empirical range file.
$legacyKeyways = Join-Path $installFolder 'data/parallel-keyways-gb.v1.json'
if (Test-Path $legacyKeyways) { Remove-Item $legacyKeyways -Force }
foreach ($sourceFile in Get-ChildItem $sourceFolder -Recurse -File) {
    $relative = $sourceFile.FullName.Substring($sourceFolder.Length).TrimStart([char]92,[char]47)
    $installedFile = Join-Path $installFolder $relative
    if ((Get-FileHash $sourceFile.FullName).Hash -ne (Get-FileHash $installedFile).Hash) { throw "Installed file mismatch: $relative" }
}
$desktopFolder = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $desktopFolder '机设工具箱.lnk'))
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $installFolder
$shortcut.IconLocation = (Join-Path $installFolder 'Assets/toolbox.ico') + ',0'
$shortcut.Description = '机设工具箱 · 螺栓、键槽、卡簧标准查询'
$shortcut.Save()
Write-Output 'INSTALL_OK: v0.4.0; all copied files verified; shortcut icon updated; no backup created.'
Start-Process $exe
