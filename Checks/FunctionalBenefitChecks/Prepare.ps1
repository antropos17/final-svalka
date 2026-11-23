param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
# Compile the actual functional-copy methods unchanged. No Unity load, clicks,
# scene parsing, copied formatter implementation, or source mutation is involved.
$runtimeRoot=Join-Path $PSScriptRoot '../../Unity/Assets/Scripts/Runtime'
$chapter=[IO.File]::ReadAllText((Join-Path $runtimeRoot 'JunkGame.Chapter.cs'))
$journey=[IO.File]::ReadAllText((Join-Path $runtimeRoot 'JunkGame.Journey.cs'))
function Section([string]$source,[string]$start,[string]$end) {
    $first=$source.IndexOf($start,[StringComparison]::Ordinal)
    if($first -lt 0){throw "Functional method marker missing: $start"}
    $last=$source.IndexOf($end,$first+$start.Length,[StringComparison]::Ordinal)
    if($last -lt 0){throw "Functional method end missing: $end"}
    return $source.Substring($first,$last-$first)
}
$methods=@(
    (Section $chapter '        public void ShowChapterFamily(' '        void ShowChapterSkill('),
    (Section $chapter '        void ShowChapterSkill(' '        void ShowChapterEra('),
    (Section $chapter '        static string FamilyName(' '        void RenderChapter('),
    (Section $journey '        static string InlineCompletion(' '        static string GuideTitle('),
    (Section $journey '        void ShowOrders(' '        bool OpenJourneyChoice('),
    (Section $journey '        void ShowOrder(' '        bool PreviousJourneyPage(')
)
$prefix='using System; using System.Linq; using JunkToNeon.Content; using JunkToNeon.Contracts; using JunkToNeon.Core; using JunkToNeon.Orders; using JunkToNeon.Upgrades; namespace FunctionalBenefitChecks { sealed partial class CopyProbe {'
$target=[IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
[IO.File]::WriteAllText($target,$prefix+"`n"+($methods -join "`n")+"`n}}",[Text.UTF8Encoding]::new($false))
