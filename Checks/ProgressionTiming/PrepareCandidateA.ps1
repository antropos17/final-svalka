param([string]$OutputRoot='X:/tmp/jtn-progression-timing03/candidate-a-source')
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path "$PSScriptRoot/../..").Path
if(Test-Path -LiteralPath $OutputRoot){throw 'Use a new exact candidate output directory'}
$proposal=Get-Content "$PSScriptRoot/evidence/candidate-a-proposal.json" -Raw|ConvertFrom-Json
if($proposal.class -ne 'price-only' -or $proposal.name -ne 'candidate-a' -or $proposal.loaded_by_unity){throw 'Unexpected or live proposal'}
$expected=[ordered]@{'skill.universal.2'=@(160,120);'skill.recycling.2'=@(90,40);'skill.delivery.1'=@(60,40)}
if(@($proposal.changes_vs_committed_v3.PSObject.Properties).Count -ne 3){throw 'Candidate must contain exactly three price deltas'}
foreach($id in $expected.Keys){if(($proposal.changes_vs_committed_v3.$id -join ',') -ne ($expected[$id] -join ',')){throw "Unexpected candidate price: $id"}}
$revision=(& git -C $repoRoot rev-parse c086395).Trim();$originalBlobs=[ordered]@{}
$paths=& git -C $repoRoot ls-tree -r --name-only $revision -- Unity/Assets/Scripts/Core Unity/Assets/Scripts/Contracts Unity/Assets/Scripts/Content Unity/Assets/Scripts/Effects Unity/Assets/Scripts/Upgrades Unity/Assets/Scripts/Orders Unity/Assets/Scripts/Guidance
foreach($path in ($paths|Where-Object {$_.EndsWith('.cs')})){$raw=(& git -C $repoRoot show "${revision}:$path") -join "`n";if($LASTEXITCODE){throw "Missing source $path"};$target=Join-Path $OutputRoot $path;New-Item -ItemType Directory -Force (Split-Path $target)|Out-Null;[IO.File]::WriteAllText($target,$raw+"`n");$originalBlobs[$path]=(& git -C $repoRoot rev-parse "${revision}:$path").Trim()}
function ReplaceOnce([string]$text,[string]$old,[string]$new){if(([regex]::Matches($text,[regex]::Escape($old))).Count -ne 1){throw "Source anchor drift: $old"};return $text.Replace($old,$new)}
$pricingPath=Join-Path $OutputRoot 'Unity/Assets/Scripts/Upgrades/SkillPricing.cs';$pricing=[IO.File]::ReadAllText($pricingPath)
$pricing=ReplaceOnce $pricing 'public const string CurrentRevision = RevisionV3;' 'public const string RevisionV4 = "skill.prices.v4"; public const string CurrentRevision = RevisionV4;'
$pricing=ReplaceOnce $pricing 'revision == RevisionV2 || revision == RevisionV3;' 'revision == RevisionV2 || revision == RevisionV3 || revision == RevisionV4;'
$pricing=ReplaceOnce $pricing 'case SkillIds.Universal2: return old ? 360 : 160;' 'case SkillIds.Universal2: return old ? 360 : revision == RevisionV4 ? 120 : 160;'
$pricing=ReplaceOnce $pricing 'case SkillIds.Recycling2: return old ? 480 : 90;' 'case SkillIds.Recycling2: return old ? 480 : revision == RevisionV4 ? 40 : 90;'
$pricing=ReplaceOnce $pricing 'case SkillIds.Delivery1: return old ? 180 : 60;' 'case SkillIds.Delivery1: return old ? 180 : revision == RevisionV4 ? 40 : 60;'
[IO.File]::WriteAllText($pricingPath,$pricing)
$treePath=Join-Path $OutputRoot 'Unity/Assets/Scripts/Upgrades/SkillTree.cs';$tree=[IO.File]::ReadAllText($treePath)
$tree=ReplaceOnce $tree 'static readonly IReadOnlyList<SkillNode> revision2Nodes = MakeNodes(SkillPricing.RevisionV2);' "static readonly IReadOnlyList<SkillNode> revision2Nodes = MakeNodes(SkillPricing.RevisionV2);`n        static readonly IReadOnlyList<SkillNode> revision3Nodes = MakeNodes(SkillPricing.RevisionV3);"
$tree=ReplaceOnce $tree 'pricingRevision == SkillPricing.RevisionV2 ? revision2Nodes : legacyNodes;' 'pricingRevision == SkillPricing.RevisionV2 ? revision2Nodes : pricingRevision == SkillPricing.RevisionV3 ? revision3Nodes : legacyNodes;'
[IO.File]::WriteAllText($treePath,$tree)
$hashes=[ordered]@{};foreach($path in $originalBlobs.Keys){$hashes[$path]=(Get-FileHash -LiteralPath (Join-Path $OutputRoot $path) -Algorithm SHA256).Hash}
[ordered]@{scope='Isolated proposal-only actual C# Core experiment, NOT production/release qualification';base=$revision;proposalSha256=(Get-FileHash "$PSScriptRoot/evidence/candidate-a-proposal.json").Hash;changedFiles=@('Unity/Assets/Scripts/Upgrades/SkillPricing.cs','Unity/Assets/Scripts/Upgrades/SkillTree.cs');originalGitBlobs=$originalBlobs;sha256=$hashes}|ConvertTo-Json -Depth 5|Set-Content "$OutputRoot/candidate-inputs.json"
Write-Output $OutputRoot
