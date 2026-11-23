using System;
using System.Collections.Generic;
using System.Text.Json;
using JunkToNeon.Contracts;
using JunkToNeon.Effects;

public static class ChapterJsonChecks
{
    public static string Run()
    {
        var fresh = DeliveryState.Fresh(EffectRules.Provisional);
        var before = SpinEffects.Prepare(new SpinOutcome(3, 3, 0), new EffectOwnership(1, 1), fresh, 4, EffectRules.Provisional).After;
        var sources = new List<SymbolCoinSource>();
        for (int i = 0; i < 19; i++) sources.Add(new SymbolCoinSource("project.source-" + i, i % 4, i + 1));
        var original = ChapterSpinEffects.Prepare(new SpinOutcome(3, 2, 2), new EffectOwnership(1, 1), before, 10,
            EffectRules.Provisional, "chapter.rules.v1", new ChapterSymbolModifiers(sources), 2, 2, 2);
        var encoded = JsonSerializer.Serialize(PersistedChapterEffects.Capture(original));
        if (encoded.Length > 16384) throw new Exception("Unexpected chapter fixture size");
        var fixture = JsonSerializer.Deserialize<PersistedChapterEffects>(encoded);
        var restored = fixture.Restore();
        if (restored.Modifiers.Sources.Count != 19 || restored.After.LastProposalKey != original.After.LastProposalKey || restored.RuleRevision != "chapter.rules.v1") throw new Exception("Full sources/revision/key not restored");
        for (int i = 0; i < 19; i++)
        {
            var left = original.Modifiers.Sources[i]; var right = restored.Modifiers.Sources[i];
            if (left.SourceId != right.SourceId || left.SymbolId != right.SymbolId || left.CoinsPerSymbol != right.CoinsPerSymbol) throw new Exception("Source descriptor changed through JSON");
        }
        var committed = restored.Apply(before, 10, 9);
        var checkpoint = JsonSerializer.Deserialize<PersistedCheckpoint>(JsonSerializer.Serialize(PersistedCheckpoint.Capture(committed.Delivery))).Restore();
        var replay = restored.Apply(checkpoint, committed.Wallet, 9);
        if (original.TotalCoins != 163 || restored.UniversalCoinsPerSpin != 2 || restored.ExtraCoinsPerTrash != 2 || restored.ExtraCoinsPerDelivery != 2 || committed.Wallet != 182 || checkpoint.PigeonProgress != 0 || replay.Applied || replay.Wallet != 182)
            throw new Exception("Frozen chapter/skills pending/checkpoint replay did not preserve expected182");
        return "JSON format2 full19source IDs/symbols/rates +3scalars +revision +baseline +afterkey roundtrip: wallet182, replay unchanged";
    }
}

// Harness-only mapping exercises complete frozen data, not production Core JSON.
public sealed class PersistedSymbolSource
{
    public string SourceId { get; set; }
    public int SymbolId { get; set; }
    public long CoinsPerSymbol { get; set; }
}
public sealed class PersistedChapterEffects
{
    public int FormatVersion { get; set; }
    public string RuleRevision { get; set; }
    public PersistedEffects Baseline { get; set; }
    public PersistedSymbolSource[] Sources { get; set; }
    public long UniversalCoinsPerSpin { get; set; }
    public long ExtraCoinsPerTrash { get; set; }
    public long ExtraCoinsPerDelivery { get; set; }
    public long UniversalCoins { get; set; }
    public long RecyclingSkillCoins { get; set; }
    public long DeliverySkillCoins { get; set; }
    public long PotatoCoins { get; set; }
    public long BoxCoins { get; set; }
    public long TrashCoins { get; set; }
    public long PigeonCoins { get; set; }
    public PersistedCheckpoint After { get; set; }
    public static PersistedChapterEffects Capture(FrozenSpinEffectsV2 p)
    {
        var sources = new List<PersistedSymbolSource>();
        foreach (var source in p.Modifiers.Sources) sources.Add(new PersistedSymbolSource { SourceId = source.SourceId, SymbolId = source.SymbolId, CoinsPerSymbol = source.CoinsPerSymbol });
        return new PersistedChapterEffects { FormatVersion = 2, RuleRevision = p.RuleRevision, Baseline = PersistedEffects.Capture(p.Baseline), Sources = sources.ToArray(),
            UniversalCoinsPerSpin = p.UniversalCoinsPerSpin, ExtraCoinsPerTrash = p.ExtraCoinsPerTrash, ExtraCoinsPerDelivery = p.ExtraCoinsPerDelivery,
            UniversalCoins = p.UniversalCoins, RecyclingSkillCoins = p.RecyclingSkillCoins, DeliverySkillCoins = p.DeliverySkillCoins,
            PotatoCoins = p.PotatoCoins, BoxCoins = p.BoxCoins, TrashCoins = p.TrashCoins, PigeonCoins = p.PigeonCoins,
            After = PersistedCheckpoint.Capture(p.After) };
    }
    public FrozenSpinEffectsV2 Restore()
    {
        var sources = new List<SymbolCoinSource>();
        foreach (var source in Sources) sources.Add(new SymbolCoinSource(source.SourceId, source.SymbolId, source.CoinsPerSymbol));
        var proposal = new FrozenSpinEffectsV2(FormatVersion, Baseline.Restore(), RuleRevision, new ChapterSymbolModifiers(sources),
            UniversalCoinsPerSpin, ExtraCoinsPerTrash, ExtraCoinsPerDelivery, UniversalCoins, RecyclingSkillCoins, DeliverySkillCoins,
            PotatoCoins, BoxCoins, TrashCoins, PigeonCoins);
        if (proposal.After.LastOperationSequence != After.LastOperationSequence || proposal.After.PigeonProgress != After.PigeonProgress ||
            proposal.After.Cadence != After.Cadence || proposal.After.LastProposalKey != After.LastProposalKey) throw new Exception("Frozen after checkpoint inconsistent");
        return proposal;
    }
}
