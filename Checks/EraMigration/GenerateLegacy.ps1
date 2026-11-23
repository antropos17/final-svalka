param([string]$OutputRoot = "$PSScriptRoot/Fixtures", [string]$WorkRoot = 'X:/tmp/jtn-era-migration03/generate')
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$pins = [ordered]@{ r6 = '8903237'; r7 = 'f5bfc34' }
New-Item -ItemType Directory -Force $WorkRoot | Out-Null
$WorkRoot = Join-Path $WorkRoot ('run-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $WorkRoot | Out-Null
$env:TEMP = "$WorkRoot/temp"; $env:TMP = $env:TEMP
New-Item -ItemType Directory -Force $env:TEMP | Out-Null
foreach ($era in $pins.Keys) {
    $revision = (& git -C $repoRoot rev-parse $pins[$era]).Trim()
    if ($LASTEXITCODE) { throw 'Pinned historical revision is unavailable' }
    $source = Join-Path $WorkRoot $era
    New-Item -ItemType Directory -Force "$source/src", "$OutputRoot/$era" | Out-Null
    $blobs = [ordered]@{}
    $paths = & git -C $repoRoot ls-tree -r --name-only $revision -- Unity/Assets/Scripts/Core Unity/Assets/Scripts/Contracts Unity/Assets/Scripts/Content Unity/Assets/Scripts/Effects Unity/Assets/Scripts/Upgrades
    foreach ($path in ($paths | Where-Object { $_.EndsWith('.cs') })) {
        $text = (& git -C $repoRoot show "${revision}:$path") -join "`n"
        if ($LASTEXITCODE) { throw "Cannot extract $path" }
        $local = Join-Path "$source/src" $path
        New-Item -ItemType Directory -Force (Split-Path $local) | Out-Null
        [IO.File]::WriteAllText($local, $text + "`n")
        $blobs[$path] = (& git -C $repoRoot rev-parse "${revision}:$path").Trim()
    }
    Copy-Item -LiteralPath "$PSScriptRoot/LegacyGenerator.cs" -Destination "$source/Program.cs"
    $define = if ($era -eq 'r6') { 'LEGACY_R6' } else { 'LEGACY_R7' }
    [IO.File]::WriteAllText("$source/Generate.csproj", "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable><DefineConstants>$define</DefineConstants></PropertyGroup></Project>")
    & dotnet run --project "$source/Generate.csproj" -- (Resolve-Path "$OutputRoot/$era").Path
    if ($LASTEXITCODE) { throw "Historical $era generator failed" }
    $fixtureHashes = [ordered]@{}
    foreach ($fixture in (Get-ChildItem "$OutputRoot/$era" -Filter '*.json' -File | Where-Object Name -ne 'provenance.json' | Sort-Object Name)) {
        $fixtureHashes[$fixture.Name] = (Get-FileHash -LiteralPath $fixture.FullName -Algorithm SHA256).Hash
    }
    [ordered]@{ revision=$revision; sourceBlobs=$blobs; generator='Checks/EraMigration/LegacyGenerator.cs'; generatorSha256=(Get-FileHash -LiteralPath "$PSScriptRoot/LegacyGenerator.cs" -Algorithm SHA256).Hash; format=$era; fixtureSha256=$fixtureHashes } | ConvertTo-Json -Depth 5 | Set-Content "$OutputRoot/$era/provenance.json"
}
