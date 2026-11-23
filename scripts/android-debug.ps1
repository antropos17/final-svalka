param([string]$JavaHome = $env:JAVA_HOME, [string]$SdkRoot = $env:ANDROID_HOME)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $SdkRoot) { $SdkRoot = $env:ANDROID_SDK_ROOT }
if (-not $JavaHome -or -not (Test-Path -LiteralPath "$JavaHome/bin/javac.exe")) {
    throw 'JDK 21+ is required. Supply -JavaHome or set JAVA_HOME for this process. Global settings are not changed.'
}
$compilerVersion = (& "$JavaHome/bin/javac.exe" -version 2>&1 | Out-String).Trim()
if ($compilerVersion -notmatch 'javac (\d+)' -or [int]$Matches[1] -lt 21) { throw "JDK 21+ is required; observed $compilerVersion" }
if (-not $SdkRoot -or -not (Test-Path -LiteralPath "$SdkRoot/platforms/android-36/android.jar")) { throw 'Android SDK platform 36 is required. Supply -SdkRoot or ANDROID_HOME.' }
$drive = Get-PSDrive -Name ([IO.Path]::GetPathRoot($projectRoot).Substring(0,1))
if ($drive.Free -lt 5GB) { throw 'Less than 5 GiB free on the project drive. Stop before downloading/building.' }
$workRoot = Join-Path $projectRoot '.work'
$existingBytes = (Get-ChildItem -LiteralPath $workRoot -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
if ($existingBytes -gt 4GB) { throw '.work exceeds 4 GiB. Inspect closed disposable files before another native build; do not delete saves or receipts.' }
$env:JAVA_HOME = $JavaHome; $env:ANDROID_HOME = $SdkRoot
$env:GRADLE_USER_HOME = Join-Path $workRoot 'gradle'
$env:TEMP = Join-Path $workRoot 'tmp'; $env:TMP = $env:TEMP
$env:npm_config_cache = Join-Path $workRoot 'npm-cache'
New-Item -ItemType Directory -Force $env:GRADLE_USER_HOME,$env:TEMP | Out-Null
Push-Location $projectRoot
try {
    & npm run android:sync
    if ($LASTEXITCODE -ne 0) { throw 'Web build or Capacitor sync failed; native build was not started.' }
    Push-Location (Join-Path $projectRoot 'android')
    try { & ./gradlew.bat assembleDebug --no-daemon --console=plain; if ($LASTEXITCODE -ne 0) { throw 'Android debug build failed.' } }
    finally { Pop-Location }
} finally { Pop-Location }
