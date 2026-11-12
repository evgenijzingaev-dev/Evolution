param([string]$EnginePath = '')

$ErrorActionPreference = 'Stop'
$ProjectDirectory = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($EnginePath)) {
    $EnginePath = Join-Path $ProjectDirectory 'Godot\Godot_v4.7.2-stable_win64_console.exe'
}
if (-not (Test-Path -LiteralPath $EnginePath)) { throw "Godot executable not found: $EnginePath" }
$env:APPDATA = Join-Path $ProjectDirectory '.godot\appdata'
$env:LOCALAPPDATA = Join-Path $ProjectDirectory '.godot\localdata'
$TestDirectory = Join-Path $ProjectDirectory 'tests'
$ResultDirectory = Join-Path $TestDirectory 'results'
New-Item -ItemType Directory -Path $ResultDirectory -Force | Out-Null
$LogPath = Join-Path $ResultDirectory 'suite.log'
Set-Content -LiteralPath $LogPath -Value '' -Encoding utf8
$TestNames = @('simulation_smoke.gd') + @(Get-ChildItem -LiteralPath $TestDirectory -Filter '*_test.gd' | Sort-Object Name | Select-Object -ExpandProperty Name)
$Failed = @()
foreach ($TestName in $TestNames) {
    $Lines = @(& $EnginePath --headless --path $ProjectDirectory --script "res://tests/$TestName" --quit-after 240 2>&1)
    $ExitCode = $LASTEXITCODE
    $Output = $Lines -join "`n"
    Add-Content -LiteralPath $LogPath -Value ("`n$TestName`n$Output") -Encoding utf8
    if ($ExitCode -ne 0 -or $Output -match '(?im)(SCRIPT ERROR:|^ERROR:|Assertion failed|Parse Error)' -or $Output -notmatch '(?i)passed') {
        $Failed += $TestName
        Write-Output "FAIL $TestName"
        Write-Output $Output
    } else {
        Write-Output "PASS $TestName"
    }
}
if ($Failed.Count -gt 0) {
    Write-Output ("Failed: " + ($Failed -join ', '))
    exit 1
}
Write-Output ("Passed {0} checks. Full log: {1}" -f $TestNames.Count, $LogPath)
exit 0
