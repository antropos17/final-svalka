param([string]$WorkRoot='X:/tmp/jtn-r8-compat03/generate')
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path "$PSScriptRoot/../../..").Path
$revision=(& git -C $repoRoot rev-parse 042fb52).Trim()
$runRoot=Join-Path $WorkRoot ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force "$runRoot/src","$PSScriptRoot/Fixtures"|Out-Null
$blobs=[ordered]@{}
$paths=& git -C $repoRoot ls-tree -r --name-only $revision -- Unity/Assets/Scripts/Core Unity/Assets/Scripts/Contracts Unity/Assets/Scripts/Content Unity/Assets/Scripts/Effects Unity/Assets/Scripts/Upgrades
foreach($path in ($paths|Where-Object {$_.EndsWith('.cs')})){
    $raw=(& git -C $repoRoot show "${revision}:$path") -join "`n";if($LASTEXITCODE){throw "Cannot extract $path"}
    $output=Join-Path "$runRoot/src" $path;New-Item -ItemType Directory -Force (Split-Path $output)|Out-Null
    [IO.File]::WriteAllText($output,$raw+"`n");$blobs[$path]=(& git -C $repoRoot rev-parse "${revision}:$path").Trim()
}
Copy-Item -LiteralPath "$PSScriptRoot/R8Generator.cs" -Destination "$runRoot/Program.cs"
[IO.File]::WriteAllText("$runRoot/Generate.csproj",'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup></Project>')
$env:TEMP="$runRoot/temp";$env:TMP=$env:TEMP;New-Item -ItemType Directory -Force $env:TEMP|Out-Null
& dotnet run --project "$runRoot/Generate.csproj" -- "$PSScriptRoot/Fixtures" "$PSScriptRoot/../Fixtures/r7/owned-offline-seen.json"
if($LASTEXITCODE){throw 'Pinned r8 generation failed'}
$hashes=[ordered]@{};foreach($file in (Get-ChildItem "$PSScriptRoot/Fixtures" -File -Filter '*.json'|Where-Object Name -ne 'provenance.json'|Sort-Object Name)){$hashes[$file.Name]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
$manifest=[ordered]@{revision=$revision;sourceBlobs=$blobs;generatorSha256=(Get-FileHash -LiteralPath "$PSScriptRoot/R8Generator.cs" -Algorithm SHA256).Hash;literalR7InputSha256=(Get-FileHash -LiteralPath "$PSScriptRoot/../Fixtures/r7/owned-offline-seen.json" -Algorithm SHA256).Hash;fixtureSha256=$hashes}|ConvertTo-Json -Depth 5
[IO.File]::WriteAllText("$PSScriptRoot/Fixtures/provenance.json",$manifest.Replace("`r`n","`n")+"`n")
