param(
    [Parameter(Position=0)][ValidateSet('build','check','smoke','explore','compat','replay')][string]$Mode = 'check',
    [Parameter(ValueFromRemainingArguments=$true)][string[]]$LabArguments
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:TEMP = Join-Path $PSScriptRoot '.temp'
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'dotnet'
$env:NUGET_PACKAGES = Join-Path $env:TEMP 'nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
New-Item -ItemType Directory -Force $env:TEMP | Out-Null
if ($Mode -eq 'build') {
    & dotnet build CommandSequenceLab.csproj --nologo -v:q --ignore-failed-sources
} else {
    & dotnet bin/Debug/net10.0/CommandSequenceLab.dll $Mode @LabArguments
}
exit $LASTEXITCODE
