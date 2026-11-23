param([string]$SourceRoot='X:/PROJECTS/FINAL-SVALKA',[string]$OutputRoot='X:/tmp/jtn-r8-compat03/journey-source-1')
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputRoot){throw 'Use a new immutable snapshot output directory'}
$modules=@('Core','Contracts','Content','Effects','Upgrades','Orders','Guidance');$hashes=[ordered]@{};$fileCount=0
foreach($module in $modules){$files=@(Get-ChildItem -LiteralPath "$SourceRoot/Unity/Assets/Scripts/$module" -Filter '*.cs' -File);if($files.Count -eq 0){throw "Required module not ready: $module"};foreach($file in $files){$relative=[IO.Path]::GetRelativePath($SourceRoot,$file.FullName).Replace('\','/');$hashes[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash;$fileCount++}}
foreach($relative in $hashes.Keys){$target=Join-Path $OutputRoot $relative;New-Item -ItemType Directory -Force (Split-Path $target)|Out-Null;Copy-Item -LiteralPath (Join-Path $SourceRoot $relative) -Destination $target}
foreach($relative in $hashes.Keys){foreach($root in @($SourceRoot,$OutputRoot)){if((Get-FileHash -LiteralPath (Join-Path $root $relative) -Algorithm SHA256).Hash -ne $hashes[$relative]){throw "Source changed during freeze: $relative"}}}
$nowCount=0;foreach($module in $modules){$nowCount+=@(Get-ChildItem -LiteralPath "$SourceRoot/Unity/Assets/Scripts/$module" -File -Filter '*.cs').Count};if($nowCount -ne $fileCount){throw 'Source set changed during freeze'}
[ordered]@{sourceRoot=$SourceRoot;head=(& git -C $SourceRoot rev-parse HEAD).Trim();dirty=@(& git -C $SourceRoot status --short -- Unity/Assets/Scripts);sha256=$hashes}|ConvertTo-Json -Depth 5|Set-Content "$OutputRoot/inputs.json"
Write-Output $OutputRoot
