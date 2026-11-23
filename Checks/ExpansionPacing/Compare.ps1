param(
    [Parameter(Mandatory=$true)][string]$Baseline,
    [Parameter(Mandatory=$true)][string]$Candidate,
    [Parameter(Mandatory=$true)][string]$Output
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Preserve existing comparison receipt; choose a new exact output path.'}
$baselineResult=Get-Content -LiteralPath $Baseline -Raw|ConvertFrom-Json
$candidateResult=Get-Content -LiteralPath $Candidate -Raw|ConvertFrom-Json
if($baselineResult.runs.Count -ne 9 -or $candidateResult.runs.Count -ne 9){throw 'Expected nine declared seed/policy runs in each packet.'}
$rows=@(foreach($run in $candidateResult.runs){
    $old=@($baselineResult.runs|Where-Object {$_.Seed -eq $run.Seed -and $_.Policy -eq $run.Policy})
    if($old.Count -ne 1 -or $run.Status -ne 'complete' -or $old[0].Status -ne 'complete'){throw 'Missing unique completed matched baseline.'}
    $cap=[Math]::Min(25,[Math]::Ceiling($old[0].ChapterSpin*.10))
    $first60=$run.Checkpoints|Where-Object Spin -eq 60|Select-Object -First 1
    $candidatePrefix=@($run.Purchases|Where-Object Spin -le 60|ForEach-Object {"$($_.Kind):$($_.Id):$($_.Spin):$($_.Price)"}) -join '|'
    $baselinePrefix=@($old[0].Purchases|Where-Object Spin -le 60|ForEach-Object {"$($_.Kind):$($_.Id):$($_.Spin):$($_.Price)"}) -join '|'
    $samePrefix=$candidatePrefix -ceq $baselinePrefix
    $incomeShare=if($first60.SpinIncome -gt 0){$first60.ExchangeIncome/[double]$first60.SpinIncome}else{$null}
    $baseLowerBound=0
    $energyLost30=0
    foreach($spin in @($run.ExpansionSpinTrace|Where-Object Spins -le 60)){
        $unique=@($spin.symbols.ToCharArray()|Sort-Object -Unique).Count
        $baseLowerBound+=if($unique -eq 1){23}elseif($unique -eq 2){8}else{3}
        if($spin.Spins -le 30){
            $nominalEnergy=1+$spin.powerEquipment
            if($spin.loadout.Split('|') -contains 'module.dynamo' -and $spin.symbols.Contains('3')){$nominalEnergy++}
            $energyLost30+=$nominalEnergy-$spin.creditedEnergy
        }
    }
    $shareUpperBound=if($baseLowerBound -gt 0){$first60.ExchangeIncome/[double]$baseLowerBound}else{$null}
    $nonSocketEffects=@($run.Expansion.firstObservedEffect.PSObject.Properties|Where-Object {$_.Name -notlike 'socket.*' -and $_.Value -le 50}).Count
    [pscustomobject]@{
        seed=$run.Seed;policy=$run.Policy
        baselineChapterSpins=$old[0].ChapterSpin;candidateChapterSpins=$run.ChapterSpin;chapterDifference=$run.ChapterSpin-$old[0].ChapterSpin
        baselineMaximumGap=$old[0].MaximumConstructionGap;candidateMaximumGap=$run.MaximumConstructionGap
        firstUsefulShopAffordabilitySpin=$run.FirstUsefulShopAffordabilitySpin
        firstUsefulShopPurchaseSpin=$run.FirstUsefulShopPurchaseSpin;usefulShopPurchasesBy50=$run.UsefulShopPurchasesBy50
        independentNonSocketEffectsBy50=$nonSocketEffects
        firstPotatoSpendSpin=$run.FirstPotatoSpendSpin;firstBoostAffordabilitySpin=$run.FirstBoostAffordabilitySpin
        boostArms=$run.BoostArms;boostConsumptions=$run.BoostConsumptions;exchangeIncome=$run.ExchangeIncome
        partsAtEnd=$run.Expansion.Parts;potatoesAtEnd=$run.Expansion.Potatoes;energyAtEnd=$run.Expansion.Energy
        partsAtCapSpins=$run.Expansion.partsAtCapSpins;potatoesAtCapSpins=$run.Expansion.potatoesAtCapSpins
        energyAtCapSpins=$run.Expansion.energyAtCapSpins;wastedEnergyAtCap=$run.Expansion.wastedEnergyAtCap
        first60ExchangeIncome=$first60.ExchangeIncome;first60UnderlyingCoinIncome=$first60.SpinIncome
        first60ExchangeToUnderlyingIncomeRatio=$incomeShare
        first60EconomicPurchasePrefixMatchesBaseline=$samePrefix
        first60RatioIsMatchedLegacyRatio=$samePrefix
        provisionalFirst60ExchangeShareBelow25pct=if($null -ne $incomeShare){$incomeShare -le .25}else{$null}
        first60MatchedLegacyBasePayoutLowerBound=$baseLowerBound
        first60ExchangeShareUpperBoundUsingBasePayout=$shareUpperBound
        provisional25pctScreenEstablishedByLowerBound=if($null -ne $shareUpperBound){$shareUpperBound -le .25}else{$null}
        denominatorBoundary='Base payout on identical observed symbols is a lower bound on legacy earnings with nonnegative old bonuses; full legacy income is exact only when the economic purchase prefix matches.'
        wastedEnergyFirst30Spins=$energyLost30
        chapterWithinDeclaredDelayLimit=($run.ChapterSpin-$old[0].ChapterSpin -le $cap)
        resourceOnlyConstructionExactlyMatches=if($run.ExchangeIncome -eq 0){$run.ChapterSpin -eq $old[0].ChapterSpin -and $run.ConstructionSpend -eq $old[0].ConstructionSpend}else{$null}
        earlyUsefulAffordabilityIn5to15=($run.FirstUsefulShopAffordabilitySpin -ge 5 -and $run.FirstUsefulShopAffordabilitySpin -le 15)
        threeUsefulChoicesBy50=if($run.Policy -ne 'house-first'){$run.UsefulShopPurchasesBy50 -ge 3}else{$null}
        threeNonSocketEffectsBy50=if($run.Policy -ne 'house-first'){$nonSocketEffects -ge 3}else{$null}
        boosterAvailableIn5to15=if($run.Policy -ne 'house-first'){$run.FirstBoostAffordabilitySpin -ge 5 -and $run.FirstBoostAffordabilitySpin -le 15}else{$null}
        potatoUseBy25=if($run.Policy -eq 'skills-first'){$run.FirstPotatoSpendSpin -ge 0 -and $run.FirstPotatoSpendSpin -le 25}else{$null}
        projectedNormalManualMinutes=$run.ChapterSpin*3.5/60
        projectedFastManualMinutes=$run.ChapterSpin*1.5/60
        timingScope='Arithmetic presentation-only projection; excludes decisions, IO and runtime measurement.'
    }
})
$report=[pscustomobject]@{
    baselinePath=(Resolve-Path -LiteralPath $Baseline).Path;candidatePath=(Resolve-Path -LiteralPath $Candidate).Path
    baselineSha256=(Get-FileHash -LiteralPath $Baseline -Algorithm SHA256).Hash
    candidateSha256=(Get-FileHash -LiteralPath $Candidate -Algorithm SHA256).Hash
    baselineSource=$baselineResult.sourceCommit;candidateSource=$candidateResult.sourceCommit
    scope='Nine fixed matched scenarios; targets declared before baseline; no population/enjoyment/native claim.'
    rows=$rows
}
$report|ConvertTo-Json -Depth 15|Set-Content -LiteralPath $Output -Encoding utf8NoBOM
$rows|Format-Table policy,seed,candidateChapterSpins,firstUsefulShopAffordabilitySpin,firstUsefulShopPurchaseSpin,usefulShopPurchasesBy50,firstBoostAffordabilitySpin -AutoSize
