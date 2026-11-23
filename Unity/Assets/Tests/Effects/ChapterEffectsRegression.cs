using System;
using System.Collections.Generic;
using JunkToNeon.Contracts;
using JunkToNeon.Effects;

namespace JunkToNeon.Tests
{
    public static class ChapterEffectsRegression
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Fails<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new Exception(message);
        }
        static DeliveryState Carry(int amount)
        {
            var fresh = DeliveryState.Fresh(EffectRules.Provisional);
            if (amount == 0) return fresh;
            return SpinEffects.Prepare(new SpinOutcome(3, amount == 2 ? 3 : 0, 0), new EffectOwnership(0, 1), fresh, 4, EffectRules.Provisional).After;
        }
        static FrozenSpinEffectsV2 Prepare(DeliveryState before, SpinOutcome outcome, ChapterSymbolModifiers modifiers,
            int recycler = 1, int loft = 1, long universal = 0, long recycling = 0, long delivery = 0, long sequence = 9)
        {
            return ChapterSpinEffects.Prepare(outcome, new EffectOwnership(recycler, loft), before, sequence, EffectRules.Provisional, "chapter.rules.v1", modifiers, universal, recycling, delivery);
        }
        static ChapterSymbolModifiers Profile()
        {
            return new ChapterSymbolModifiers(new[] { new SymbolCoinSource("cart.improve", 0, 1), new SymbolCoinSource("power-source.build", 1, 2),
                new SymbolCoinSource("recycler.improve", 2, 3), new SymbolCoinSource("pigeon-loft.improve", 3, 4) });
        }
        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>();
            var profiles = new[] { ChapterSymbolModifiers.Empty, Profile(), new ChapterSymbolModifiers(new[] {
                new SymbolCoinSource("cart.improve", 0, 7), new SymbolCoinSource("cart.workshop-yard.upgrade-1", 0, 2),
                new SymbolCoinSource("recycler.improve", 2, 11) }) };
            int valid = 0, rejected = 0;
            foreach (var profile in profiles)
            for (int recycler = 0; recycler <= 1; recycler++) for (int loft = 0; loft <= 1; loft++)
            for (int carry = 0; carry <= (loft == 1 ? 2 : 0); carry++)
            for (int universal = 0; universal <= 2; universal++) for (int recycling = 0; recycling <= 2; recycling++) for (int delivery = 0; delivery <= 2; delivery++)
            for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) for (int c = 0; c < 4; c++)
            {
                var outcome = new SpinOutcome(a, b, c); var before = Carry(carry);
                if ((recycler == 0 && recycling != 0) || (loft == 0 && delivery != 0))
                {
                    Fails<InvalidOperationException>(() => Prepare(before, outcome, profile, recycler, loft, universal, recycling, delivery), "Skill without pre-owned building accepted"); rejected++; continue;
                }
                int trash = 0, pigeon = 0; long chapter = 0;
                foreach (int symbol in new[] { a, b, c })
                {
                    if (symbol == 2) trash++; if (symbol == 3) pigeon++;
                    foreach (var source in profile.Sources) if (source.SymbolId == symbol) chapter += source.CoinsPerSymbol;
                }
                int progress = carry + (loft == 1 ? pigeon : 0), deliveries = 0;
                while (progress >= 3) { deliveries++; progress -= 3; }
                long expected = (recycler == 1 ? trash : 0) + deliveries * 3 + universal + recycling * trash + delivery * deliveries + chapter;
                var p = Prepare(before, outcome, profile, recycler, loft, universal, recycling, delivery);
                Require(p.TotalCoins == expected && p.After.PigeonProgress == progress && p.Baseline.Deliveries == deliveries, "Independent chapter/skill grid expected amount/carry");
                Require(p.UniversalCoins == universal && p.RecyclingSkillCoins == recycling * trash && p.DeliverySkillCoins == delivery * deliveries, "Independent skill attribution");
                int baseReward = a == b && b == c ? 23 : a == b || a == c || b == c ? 8 : 3;
                Require(p.Apply(before, 100, baseReward).Wallet == 100 + baseReward + expected && p.Contributions.Count <= 9, "Base23/8/3 or combined contribution total changed");
                Require(p.Baseline.Outcome.Left == a && p.Baseline.Outcome.Middle == b && p.Baseline.Outcome.Right == c, "Frozen symbols changed"); valid++;
            }
            Require(valid == 23040 && rejected == 18432, "Incomplete64-outcome27-tier ownership/carry grid");
            passed.Add("Three source profiles x64 outcomes x27 tier combinations x8 valid ownership/carry states:23040 accepted,18432 fail-closed prerequisites");

            var inputs = new[] { new SymbolCoinSource("power-source.build", 2, 2), new SymbolCoinSource("cart.improve", 2, 3) };
            var modifiers = new ChapterSymbolModifiers(inputs);
            inputs[0] = new SymbolCoinSource("other.source", 0, 99);
            var reversed = new ChapterSymbolModifiers(new[] { new SymbolCoinSource("cart.improve", 2, 3), new SymbolCoinSource("power-source.build", 2, 2) });
            var emptyCarry = Carry(0);
            var aggregated = Prepare(emptyCarry, new SpinOutcome(2, 2, 2), modifiers, 0, 0);
            Require(modifiers.CoinsPerSymbol(2) == 5 && aggregated.TrashCoins == 15 && aggregated.After.LastProposalKey == Prepare(emptyCarry, new SpinOutcome(2, 2, 2), reversed, 0, 0).After.LastProposalKey, "Sorted frozen sources changed identity or summed twice");
            Require(modifiers.Sources[0].SourceId == "cart.improve" && ((IList<SymbolCoinSource>)modifiers.Sources).IsReadOnly, "Frozen sources mutable or not canonical");
            passed.Add("Complete source IDs/rates are immutable, canonically ordered and aggregated without source duplication");

            var universalOnly = Prepare(emptyCarry, new SpinOutcome(0, 0, 1), ChapterSymbolModifiers.Empty, 0, 0, 1);
            Require(universalOnly.TotalCoins == 1 && universalOnly.Apply(emptyCarry, 0, 9).Wallet == 10, "Canonical oldslot universal source counted twice");
            foreach (var contribution in universalOnly.Contributions) Require(contribution.EffectId != GameIds.SlotCoinBoost, "Format2 retained a duplicate oldslot contribution");
            Require(SkillIds.Universal1 == GameIds.SlotCoinBoost, "Canonical universal identity changed");
            passed.Add("Canonical slot.coin-boost becomes total universal scalar; base8+workshop1+universal1=10 once");

            var beforeTwo = Carry(2);
            var mixedSources = new ChapterSymbolModifiers(new[] { new SymbolCoinSource("recycler.improve", 2, 2), new SymbolCoinSource("pigeon-loft.improve", 3, 1) });
            var mixed = Prepare(beforeTwo, new SpinOutcome(3, 2, 2), mixedSources, 1, 1, 2, 2, 2);
            Require(mixed.Baseline.RecyclerCoins == 2 && mixed.Baseline.DeliveryCoins == 3 && mixed.UniversalCoins == 2 && mixed.RecyclingSkillCoins == 4 && mixed.DeliverySkillCoins == 2 && mixed.TrashCoins == 4 && mixed.PigeonCoins == 1, "Named combined credit attribution");
            Require(mixed.TotalCoins == 18 && mixed.Apply(beforeTwo, 10, 9).Wallet == 37 && mixed.After.PigeonProgress == 0, "Carry2+pigeon/trash/trash mixed expected wallet37");
            passed.Add("Named baseline/skill/chapter contributions: base8+workshop1+effects18 gives10->37 and carry2->0");

            var acquisition = Prepare(emptyCarry, new SpinOutcome(2, 3, 3), ChapterSymbolModifiers.Empty, 0, 0);
            Require(acquisition.TotalCoins == 0 && acquisition.After.PigeonProgress == 0, "Acquisition-spin effect or retroactive carry");
            var subsequent = Prepare(acquisition.After, new SpinOutcome(2, 3, 3), mixedSources, 1, 1, 1, 1, 1, 11);
            Require(subsequent.UniversalCoins == 1 && subsequent.RecyclingSkillCoins == 1 && subsequent.DeliverySkillCoins == 0 && subsequent.After.PigeonProgress == 2 && subsequent.TrashCoins == 2 && subsequent.PigeonCoins == 2, "First subsequent source/skill spin");
            passed.Add("Pre-command inputs exclude new buildings/skills/chapter sources on acquisition spin");

            var restoredBaseline = new FrozenSpinEffects(1, mixed.Baseline.OperationSequence, mixed.Baseline.Outcome, mixed.Baseline.Ownership,
                new EffectRules(1, 3, 3), new DeliveryState(beforeTwo.LastOperationSequence, 2, 3, beforeTwo.LastProposalKey), 0, 1, 2, 3);
            var restoredModifiers = new ChapterSymbolModifiers(new[] { new SymbolCoinSource("pigeon-loft.improve", 3, 1), new SymbolCoinSource("recycler.improve", 2, 2) });
            var restored = new FrozenSpinEffectsV2(2, restoredBaseline, "chapter.rules.v1", restoredModifiers, 2, 2, 2, 2, 4, 2, 0, 0, 4, 1);
            var applied = restored.Apply(beforeTwo, 10, 9);
            var changedLive = Prepare(beforeTwo, new SpinOutcome(3, 2, 2), Profile(), 1, 1, 0, 0, 0);
            Require(applied.Wallet == 37 && changedLive.TotalCoins != restored.TotalCoins && restored.After.LastProposalKey == mixed.After.LastProposalKey, "Frozen recovery consulted changed chapter/skills");
            Require(!restored.Apply(applied.Delivery, applied.Wallet, 9).Applied && restored.Apply(applied.Delivery, applied.Wallet, 9).Wallet == 37, "Format2 replay credited twice");
            Fails<InvalidOperationException>(() => changedLive.Apply(applied.Delivery, 37, 9), "Changed catalog same sequence replay accepted");
            Fails<InvalidOperationException>(() => mixed.Apply(emptyCarry, 0, 9), "Stale checkpoint accepted");
            var sameRateDifferentSource = Prepare(beforeTwo, new SpinOutcome(3, 2, 2), new ChapterSymbolModifiers(new[] {
                new SymbolCoinSource("other.registered-source", 2, 2), new SymbolCoinSource("pigeon-loft.improve", 3, 1) }), 1, 1, 2, 2, 2);
            Require(sameRateDifferentSource.TotalCoins == mixed.TotalCoins, "Source identity fixture changed amount");
            Fails<InvalidOperationException>(() => sameRateDifferentSource.Apply(applied.Delivery, 37, 9), "Aggregate-only replay ignored source identity");
            var differentRevision = ChapterSpinEffects.Prepare(mixed.Baseline.Outcome, mixed.Baseline.Ownership, beforeTwo, 9,
                EffectRules.Provisional, "chapter.rules.v2", mixedSources, 2, 2, 2);
            Fails<InvalidOperationException>(() => differentRevision.Apply(applied.Delivery, 37, 9), "Different rule revision replay accepted");
            Fails<ArgumentException>(() => ChapterSpinEffects.Prepare(mixed.Baseline.Outcome, mixed.Baseline.Ownership, beforeTwo, 9,
                EffectRules.Provisional, "Chapter rules", mixedSources, 2, 2, 2), "Invalid rule revision shape accepted");
            passed.Add("Full frozen restore ignores changed catalog; exact replay no-op and changed-source/stale conflicts reject");

            Fails<ArgumentException>(() => new FrozenSpinEffectsV2(1, restoredBaseline, "chapter.rules.v1", mixedSources, 2, 2, 2, 2, 4, 2, 0, 0, 4, 1), "UnknownV2 version accepted");
            Fails<ArgumentException>(() => new FrozenSpinEffectsV2(2, restoredBaseline, "chapter.rules.v1", mixedSources, 2, 2, 2, 2, 5, 2, 0, 0, 4, 1), "Forged skill delta accepted");
            Fails<ArgumentException>(() => new FrozenSpinEffectsV2(2, restoredBaseline, "chapter.rules.v1", mixedSources, 2, 2, 2, 2, 4, 2, 0, 0, 5, 1), "Forged chapter delta accepted");
            Fails<ArgumentOutOfRangeException>(() => Prepare(emptyCarry, new SpinOutcome(0, 0, 0), ChapterSymbolModifiers.Empty, 1, 1, 3), "Unbounded skill scalar accepted");
            Fails<ArgumentOutOfRangeException>(() => Prepare(emptyCarry, new SpinOutcome(0, 0, 0), ChapterSymbolModifiers.Empty, 1, 1, -1), "Negative skill scalar accepted");
            var wrongCadence = DeliveryState.Fresh(new EffectRules(1, 4, 3));
            Fails<InvalidOperationException>(() => Prepare(wrongCadence, new SpinOutcome(0, 0, 0), ChapterSymbolModifiers.Empty), "Cadence silently changed/dropped");
            var changedBaseline = SpinEffects.Prepare(new SpinOutcome(0, 0, 0), new EffectOwnership(1, 1), emptyCarry, 9, new EffectRules(2, 3, 3));
            Fails<ArgumentException>(() => new FrozenSpinEffectsV2(2, changedBaseline, "chapter.rules.v1", ChapterSymbolModifiers.Empty, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), "Changed baseline underformat2 accepted");
            passed.Add("Explicit2 rejects unknown versions, forged amounts, invalid skills and unapproved cadence/baseline changes");

            var legacy = SpinEffects.Prepare(new SpinOutcome(3, 2, 2), new EffectOwnership(1, 1), beforeTwo, 9, EffectRules.Provisional);
            Require(legacy.TotalCoins == 5 && legacy.Apply(beforeTwo, 10, 9).Wallet == 24 && legacy.After.LastProposalKey.StartsWith("effects.v1/", StringComparison.Ordinal), "Frozen v1 gained new rewards/key");
            var v2Next = Prepare(legacy.After, new SpinOutcome(3, 3, 3), ChapterSymbolModifiers.Empty, 1, 1, 1, 1, 1, 12);
            Require(v2Next.Baseline.Deliveries == 1 && v2Next.DeliverySkillCoins == 1 && v2Next.After.PigeonProgress == 0, "V1 checkpoint continuation lost carry/history");
            passed.Add("Literal frozen v1 remains reward5/keyv1; sparse next-v2 spin preserves checkpoint/cadence");

            var sources = new List<SymbolCoinSource>();
            for (int i = 0; i < 32; i++) sources.Add(new SymbolCoinSource("source." + i, 2, 1000000));
            var maximum = Prepare(emptyCarry, new SpinOutcome(2, 2, 2), new ChapterSymbolModifiers(sources), 1, 1, 2, 2, 2);
            Require(maximum.TrashCoins == 96000000 && maximum.TotalCoins == 96000011 && maximum.Contributions.Count == 9 && maximum.After.LastProposalKey.Length == 75, "32-source bound/aggregation/key size");
            Require(maximum.Apply(emptyCarry, long.MaxValue - maximum.TotalCoins - 23, 23).Wallet == long.MaxValue, "Exactlong boundary rejected");
            Fails<OverflowException>(() => maximum.Apply(emptyCarry, long.MaxValue - maximum.TotalCoins - 22, 23), "Wallet overflow accepted");
            Fails<OverflowException>(() => maximum.Apply(emptyCarry, long.MaxValue, 23), "External credit overflow accepted");
            Require(emptyCarry.LastOperationSequence == 0 && emptyCarry.PigeonProgress == 0, "Overflow mutated checkpoint");
            sources.Add(new SymbolCoinSource("source.32", 2, 1));
            Fails<ArgumentException>(() => new ChapterSymbolModifiers(sources), "Unbounded source collection accepted");
            passed.Add("32sources x1million x3symbols=96million; max9credits/key75, exactlong success and overflow fail closed");

            var repeated = Prepare(beforeTwo, new SpinOutcome(3, 2, 2), mixedSources, 1, 1, 2, 2, 2);
            Require(beforeTwo.PigeonProgress == 2 && repeated.After.LastProposalKey == mixed.After.LastProposalKey && repeated.TotalCoins == mixed.TotalCoins, "Pure repeated inspection changed counters");
            var list = mixed.Contributions as IList<EffectContribution>;
            Require(list != null && list.IsReadOnly, "V2 contributions mutable");
            Fails<NotSupportedException>(() => list[0] = new EffectContribution(GameIds.RecyclerTrashCoins, 999), "V2 list mutation accepted");
            passed.Add("Preparation/inspection is pure, deterministic and leaves checkpoint intact; all children immutable");
            return passed;
        }
    }
}
