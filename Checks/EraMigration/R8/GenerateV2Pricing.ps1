param([string]$WorkRoot='X:/tmp/jtn-r8-compat03/v2-generation')
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path "$PSScriptRoot/../../..").Path
$revision=(& git -C $repoRoot rev-parse bff8b564).Trim()
$runRoot=Join-Path $WorkRoot ([Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Force "$runRoot/src","$PSScriptRoot/V2Pricing"|Out-Null
$blobs=[ordered]@{};$paths=& git -C $repoRoot ls-tree -r --name-only $revision -- Unity/Assets/Scripts/Contracts Unity/Assets/Scripts/Upgrades
foreach($path in ($paths|Where-Object {$_.EndsWith('.cs')})){$raw=(& git -C $repoRoot show "${revision}:$path") -join "`n";if($LASTEXITCODE){throw "Missing pinned source $path"};$target=Join-Path "$runRoot/src" $path;New-Item -ItemType Directory -Force (Split-Path $target)|Out-Null;[IO.File]::WriteAllText($target,$raw+"`n");$blobs[$path]=(& git -C $repoRoot rev-parse "${revision}:$path").Trim()}
Copy-Item -LiteralPath "$PSScriptRoot/V2PricingGenerator.cs" -Destination "$runRoot/Program.cs"
[IO.File]::WriteAllText("$runRoot/Generate.csproj",'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup></Project>')
$env:TEMP="$runRoot/temp";$env:TMP=$env:TEMP;New-Item -ItemType Directory -Force $env:TEMP|Out-Null
& dotnet run --project "$runRoot/Generate.csproj" -- "$PSScriptRoot/V2Pricing" "$PSScriptRoot/Fixtures/owned-delivery-1.json"
if($LASTEXITCODE){throw 'Pinned V2 proposal generation failed'}
$hashes=[ordered]@{};foreach($file in (Get-ChildItem "$PSScriptRoot/V2Pricing" -File -Filter '*.json'|Where-Object Name -ne 'provenance.json')){$hashes[$file.Name]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
$data=[ordered]@{revision=$revision;scope='Pinned Upgrades module proposal/ownership payloads, NOT a historical full Core snapshot';sourceBlobs=$blobs;generatorSha256=(Get-FileHash -LiteralPath "$PSScriptRoot/V2PricingGenerator.cs" -Algorithm SHA256).Hash;r8InputSha256=(Get-FileHash -LiteralPath "$PSScriptRoot/Fixtures/owned-delivery-1.json" -Algorithm SHA256).Hash;fixtureSha256=$hashes}|ConvertTo-Json -Depth 5
[IO.File]::WriteAllText("$PSScriptRoot/V2Pricing/provenance.json",$data.Replace("`r`n","`n")+"`n")
