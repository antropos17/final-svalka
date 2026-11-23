using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JunkToNeon.Contracts;

namespace JunkToNeon.Effects
{
    // Format 2 composes the literal format-1 building math, but freezes all new
    // source identities and skill scalars. Sources exclude existing workshop,
    // recycler, delivery and slot/universal effects; Core validates registration
    // and PRE-command ownership against its pinned catalog before reservation.
    public sealed class FrozenSpinEffectsV2
    {
        public const int FormatVersion = 2;
        public FrozenSpinEffects Baseline { get; }
        public string RuleRevision { get; }
        public ChapterSymbolModifiers Modifiers { get; }
        public long UniversalCoinsPerSpin { get; }
        public long ExtraCoinsPerTrash { get; }
        public long ExtraCoinsPerDelivery { get; }
        public long UniversalCoins { get; }
        public long RecyclingSkillCoins { get; }
        public long DeliverySkillCoins { get; }
        public long PotatoCoins { get; }
        public long BoxCoins { get; }
        public long TrashCoins { get; }
        public long PigeonCoins { get; }
        public long TotalCoins { get; }
        public long OperationSequence { get { return Baseline.OperationSequence; } }
        public DeliveryState Before { get { return Baseline.Before; } }
        public DeliveryState After { get; }
        public IReadOnlyList<EffectContribution> Contributions { get; }

        // Restore only frozen descriptors and deltas. No live catalog, clock or
        // RNG participates in reconstruction. A changed compatible catalog must
        // not replace a pending operation's complete frozen Sources collection.
        public FrozenSpinEffectsV2(int formatVersion, FrozenSpinEffects baseline, string ruleRevision, ChapterSymbolModifiers modifiers,
            long universalCoinsPerSpin, long extraCoinsPerTrash, long extraCoinsPerDelivery,
            long universalCoins, long recyclingSkillCoins, long deliverySkillCoins,
            long potatoCoins, long boxCoins, long trashCoins, long pigeonCoins)
        {
            if (formatVersion != FormatVersion) throw new ArgumentException("Unknown chapter effects version", nameof(formatVersion));
            if (baseline == null || modifiers == null) throw new ArgumentNullException(nameof(baseline));
            if (!StableId.IsValid(ruleRevision)) throw new ArgumentException("Invalid frozen rule revision", nameof(ruleRevision));
            ValidateSkills(baseline.Ownership, universalCoinsPerSpin, extraCoinsPerTrash, extraCoinsPerDelivery);
            if (baseline.Rules.RecyclerCoinsPerTrash != 1 || baseline.Rules.PigeonsPerDelivery != 3 || baseline.Rules.CoinsPerDelivery != 3 || baseline.Before.Cadence != 3)
                throw new ArgumentException("Format 2 preserves baseline1/3/3 and delivery cadence3");
            var expected = ChapterEffectMath.Calculate(baseline.Outcome, baseline.Ownership, baseline.Deliveries,
                universalCoinsPerSpin, extraCoinsPerTrash, extraCoinsPerDelivery,
                modifiers.CoinsPerSymbol(0), modifiers.CoinsPerSymbol(1), modifiers.CoinsPerSymbol(2), modifiers.CoinsPerSymbol(3));
            var frozen = new[] { universalCoins, recyclingSkillCoins, deliverySkillCoins, potatoCoins, boxCoins, trashCoins, pigeonCoins };
            for (int i = 0; i < expected.Length; i++) if (frozen[i] != expected[i]) throw new ArgumentException("Inconsistent frozen chapter/skill credit");
            Baseline = baseline; RuleRevision = ruleRevision; Modifiers = modifiers;
            UniversalCoinsPerSpin = universalCoinsPerSpin; ExtraCoinsPerTrash = extraCoinsPerTrash; ExtraCoinsPerDelivery = extraCoinsPerDelivery;
            UniversalCoins = universalCoins; RecyclingSkillCoins = recyclingSkillCoins; DeliverySkillCoins = deliverySkillCoins;
            PotatoCoins = potatoCoins; BoxCoins = boxCoins; TrashCoins = trashCoins; PigeonCoins = pigeonCoins;
            var contributions = new List<EffectContribution>(9);
            foreach (var contribution in baseline.Contributions) contributions.Add(contribution);
            contributions.Add(new EffectContribution(ChapterEffectIds.SkillUniversal, universalCoins));
            contributions.Add(new EffectContribution(ChapterEffectIds.SkillRecycling, recyclingSkillCoins));
            contributions.Add(new EffectContribution(ChapterEffectIds.SkillDelivery, deliverySkillCoins));
            contributions.Add(new EffectContribution(ChapterEffectIds.PotatoCoins, potatoCoins));
            contributions.Add(new EffectContribution(ChapterEffectIds.BoxCoins, boxCoins));
            contributions.Add(new EffectContribution(ChapterEffectIds.TrashCoins, trashCoins));
            contributions.Add(new EffectContribution(ChapterEffectIds.PigeonCoins, pigeonCoins));
            Contributions = contributions.AsReadOnly(); TotalCoins = CoinEffects.Total(Contributions);
            var identity = new List<string> { "effects.v2", ruleRevision, baseline.After.LastProposalKey, baseline.Before.LastProposalKey,
                N(universalCoinsPerSpin), N(extraCoinsPerTrash), N(extraCoinsPerDelivery) };
            foreach (var source in modifiers.Sources) { identity.Add(source.SourceId); identity.Add(N(source.SymbolId)); identity.Add(N(source.CoinsPerSymbol)); }
            foreach (long amount in frozen) identity.Add(N(amount));
            After = new DeliveryState(OperationSequence, baseline.After.PigeonProgress, 3, ChapterEffectMath.Key(identity.ToArray()));
        }

        static string N(long value) { return value.ToString(CultureInfo.InvariantCulture); }
        internal static void ValidateSkills(EffectOwnership ownership, long universal, long recycling, long delivery)
        {
            if (universal < 0 || universal > 2 || recycling < 0 || recycling > 2 || delivery < 0 || delivery > 2) throw new ArgumentOutOfRangeException(nameof(universal), "Skill scalars must be0..2");
            if ((recycling != 0 && ownership.RecyclerLevel != 1) || (delivery != 0 && ownership.PigeonLoftLevel != 1)) throw new InvalidOperationException("Skill requires its pre-owned building");
        }

        // frozenExternalCredit is SAME-operation base plus workshop only. Format2
        // UniversalCoins already includes canonical slot.coin-boost; adding the
        // old slot credit again would double-count it. GameStore saves this result,
        // the global sequence, all receipts and purchases as one atomic snapshot.
        public EffectCommit Apply(DeliveryState current, long wallet, long frozenExternalCredit)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (wallet < 0 || frozenExternalCredit < 0) throw new ArgumentOutOfRangeException(nameof(wallet));
            if (current.LastOperationSequence == OperationSequence)
            {
                if (!current.Same(After)) throw new InvalidOperationException("Conflicting chapter effects replay");
                return new EffectCommit(current, wallet, false);
            }
            if (!current.Same(Before)) throw new InvalidOperationException("Stale or mismatched chapter effects checkpoint");
            return new EffectCommit(After, checked(checked(wallet + frozenExternalCredit) + TotalCoins), true);
        }
    }

    public static class ChapterSpinEffects
    {
        public static FrozenSpinEffectsV2 Prepare(SpinOutcome outcome, EffectOwnership preOwned,
            DeliveryState before, long operationSequence, EffectRules frozenBaselineRules, string ruleRevision, ChapterSymbolModifiers chapter,
            long universalCoinsPerSpin, long extraCoinsPerTrash, long extraCoinsPerDelivery)
        {
            if (chapter == null) throw new ArgumentNullException(nameof(chapter));
            FrozenSpinEffectsV2.ValidateSkills(preOwned, universalCoinsPerSpin, extraCoinsPerTrash, extraCoinsPerDelivery);
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (before.Cadence != 3) throw new InvalidOperationException("Delivery cadence change requires explicit migration");
            var baseline = SpinEffects.Prepare(outcome, preOwned, before, operationSequence, frozenBaselineRules);
            var amounts = ChapterEffectMath.Calculate(outcome, preOwned, baseline.Deliveries,
                universalCoinsPerSpin, extraCoinsPerTrash, extraCoinsPerDelivery,
                chapter.CoinsPerSymbol(0), chapter.CoinsPerSymbol(1), chapter.CoinsPerSymbol(2), chapter.CoinsPerSymbol(3));
            return new FrozenSpinEffectsV2(2, baseline, ruleRevision, chapter, universalCoinsPerSpin, extraCoinsPerTrash, extraCoinsPerDelivery,
                amounts[0], amounts[1], amounts[2], amounts[3], amounts[4], amounts[5], amounts[6]);
        }
    }

    internal static class ChapterEffectMath
    {
        internal static string Key(params string[] frozenFields)
        {
            var text = new StringBuilder();
            foreach (var field in frozenFields)
            {
                if (field == null) text.Append("-1:");
                else text.Append(field.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(field);
            }
            // A bounded replay identity, not a signature or claim of save authenticity.
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                var result = new StringBuilder("effects.v2/");
                const string hex = "0123456789abcdef";
                foreach (var value in bytes) result.Append(hex[value >> 4]).Append(hex[value & 15]);
                return result.ToString();
            }
        }
        // Skill amounts are total purchased tiers, not an additional legacy slot source.
        internal static long[] Calculate(SpinOutcome outcome, EffectOwnership ownership, int deliveries,
            long universalCoinsPerSpin, long extraCoinsPerTrash, long extraCoinsPerDelivery,
            long potatoCoinsPerSymbol, long boxCoinsPerSymbol, long trashCoinsPerSymbol, long pigeonCoinsPerSymbol)
        {
            return new[] {
                universalCoinsPerSpin,
                ownership.RecyclerLevel == 1 ? checked(extraCoinsPerTrash * outcome.Count(SymbolIds.Trash)) : 0,
                ownership.PigeonLoftLevel == 1 ? checked(extraCoinsPerDelivery * deliveries) : 0,
                checked(potatoCoinsPerSymbol * outcome.Count(SymbolIds.Potato)),
                checked(boxCoinsPerSymbol * outcome.Count(SymbolIds.Box)),
                checked(trashCoinsPerSymbol * outcome.Count(SymbolIds.Trash)),
                checked(pigeonCoinsPerSymbol * outcome.Count(SymbolIds.Pigeon)) };
        }
    }
}
