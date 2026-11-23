using System;
using System.Text.Json;
using JunkToNeon.Contracts;
using JunkToNeon.Effects;
using JunkToNeon.Tests;

foreach (var name in EffectsRegression.Run()) Console.WriteLine("PASS " + name);
foreach (var name in ChapterEffectsRegression.Run()) Console.WriteLine("PASS " + name);
Console.WriteLine("PASS " + ChapterJsonChecks.Run());

var rules = EffectRules.Provisional;
var fresh = DeliveryState.Fresh(rules);
var preceding = SpinEffects.Prepare(new SpinOutcome(3, 3, 0), new EffectOwnership(1, 1), fresh, 4, rules);
var pending = SpinEffects.Prepare(new SpinOutcome(3, 2, 2), new EffectOwnership(1, 1), preceding.After, 8, rules);
var encoded = JsonSerializer.Serialize(PersistedEffects.Capture(pending));
if (encoded.Length > 4096) throw new Exception("Unexpected unbounded pending fixture");
var restored = JsonSerializer.Deserialize<PersistedEffects>(encoded).Restore();
var committed = restored.Apply(preceding.After, 10, 8);
var restoredCheckpoint = JsonSerializer.Deserialize<PersistedCheckpoint>(JsonSerializer.Serialize(PersistedCheckpoint.Capture(committed.Delivery))).Restore();
var replay = restored.Apply(restoredCheckpoint, committed.Wallet, 8);
if (committed.Wallet != 23 || !committed.Applied || replay.Applied || replay.Wallet != 23 || restoredCheckpoint.PigeonProgress != 0) throw new Exception("Pending/committed JSON restart changed payout or replayed");
Console.WriteLine("PASS JSON pending/checkpoint roundtrip: frozen2+3, base8, wallet23, replay unchanged");

// Harness-only explicit persistence mapping. Production strict codec ownership stays in Core.
public sealed class PersistedCheckpoint
{
    public long LastOperationSequence { get; set; }
    public int PigeonProgress { get; set; }
    public int Cadence { get; set; }
    public string LastProposalKey { get; set; }
    public static PersistedCheckpoint Capture(DeliveryState state)
    {
        return new PersistedCheckpoint { LastOperationSequence = state.LastOperationSequence, PigeonProgress = state.PigeonProgress, Cadence = state.Cadence, LastProposalKey = state.LastProposalKey };
    }
    public DeliveryState Restore() { return new DeliveryState(LastOperationSequence, PigeonProgress, Cadence, LastProposalKey); }
}
public sealed class PersistedEffects
{
    public int FormatVersion { get; set; }
    public long OperationSequence { get; set; }
    public int Left { get; set; }
    public int Middle { get; set; }
    public int Right { get; set; }
    public int RecyclerLevel { get; set; }
    public int PigeonLoftLevel { get; set; }
    public long RecyclerCoinsPerTrash { get; set; }
    public int PigeonsPerDelivery { get; set; }
    public long CoinsPerDelivery { get; set; }
    public PersistedCheckpoint Before { get; set; }
    public int PigeonProgressAfter { get; set; }
    public int Deliveries { get; set; }
    public long RecyclerCoins { get; set; }
    public long DeliveryCoins { get; set; }
    public static PersistedEffects Capture(FrozenSpinEffects p)
    {
        return new PersistedEffects { FormatVersion = FrozenSpinEffects.FormatVersion, OperationSequence = p.OperationSequence,
            Left = p.Outcome.Left, Middle = p.Outcome.Middle, Right = p.Outcome.Right,
            RecyclerLevel = p.Ownership.RecyclerLevel, PigeonLoftLevel = p.Ownership.PigeonLoftLevel,
            RecyclerCoinsPerTrash = p.Rules.RecyclerCoinsPerTrash, PigeonsPerDelivery = p.Rules.PigeonsPerDelivery, CoinsPerDelivery = p.Rules.CoinsPerDelivery,
            Before = PersistedCheckpoint.Capture(p.Before), PigeonProgressAfter = p.After.PigeonProgress, Deliveries = p.Deliveries,
            RecyclerCoins = p.RecyclerCoins, DeliveryCoins = p.DeliveryCoins };
    }
    public FrozenSpinEffects Restore()
    {
        return new FrozenSpinEffects(FormatVersion, OperationSequence, new SpinOutcome(Left, Middle, Right), new EffectOwnership(RecyclerLevel, PigeonLoftLevel),
            new EffectRules(RecyclerCoinsPerTrash, PigeonsPerDelivery, CoinsPerDelivery), Before.Restore(), PigeonProgressAfter, Deliveries, RecyclerCoins, DeliveryCoins);
    }
}
