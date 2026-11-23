param([string]$SourceRoot='X:/PROJECTS/FINAL-SVALKA', [string]$OutputRoot='X:/tmp/jtn-era-migration03/phase2-source-1')
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputRoot){throw 'Use a new output directory for each immutable input snapshot'}
$paths=@();foreach($module in @('Core','Contracts','Content','Effects','Upgrades')){$paths+=Get-ChildItem -LiteralPath "$SourceRoot/Unity/Assets/Scripts/$module" -Filter '*.cs' -File}
$hashes=[ordered]@{};foreach($file in ($paths|Sort-Object FullName)){$relative=[IO.Path]::GetRelativePath($SourceRoot,$file.FullName).Replace('\','/');$hashes[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
foreach($relative in $hashes.Keys){$target=Join-Path $OutputRoot $relative;New-Item -ItemType Directory -Force (Split-Path $target)|Out-Null;Copy-Item -LiteralPath (Join-Path $SourceRoot $relative) -Destination $target}
foreach($relative in $hashes.Keys){foreach($root in @($SourceRoot,$OutputRoot)){if((Get-FileHash -LiteralPath (Join-Path $root $relative) -Algorithm SHA256).Hash -ne $hashes[$relative]){throw "Source changed while freezing: $relative"}}}
$current=@();foreach($module in @('Core','Contracts','Content','Effects','Upgrades')){$current+=Get-ChildItem -LiteralPath "$SourceRoot/Unity/Assets/Scripts/$module" -Filter '*.cs' -File}
if($current.Count -ne $hashes.Count){throw 'Source file set changed while freezing'}
[ordered]@{sourceRoot=$SourceRoot;head=(& git -C $SourceRoot rev-parse HEAD).Trim();dirty=(@(& git -C $SourceRoot status --short -- Unity/Assets/Scripts));sha256=$hashes}|ConvertTo-Json -Depth 5|Set-Content "$OutputRoot/inputs.json"
Write-Output $OutputRoot
