using System;
using System.Collections.Generic;
using System.Globalization;
using JunkToNeon.Contracts;

namespace JunkToNeon.Effects
{
    // Prototype tuning only. The descriptor is frozen into each pending proposal.
    public sealed class EffectRules
    {
        public const int MaximumCadence = 1000000;
        public long RecyclerCoinsPerTrash { get; }
        public int PigeonsPerDelivery { get; }
        public long CoinsPerDelivery { get; }
        public static EffectRules Provisional { get { return new EffectRules(1, 3, 3); } }
        public EffectRules(long recyclerCoinsPerTrash, int pigeonsPerDelivery, long coinsPerDelivery)
        {
            if (recyclerCoinsPerTrash < 0 || coinsPerDelivery < 0) throw new ArgumentOutOfRangeException(nameof(recyclerCoinsPerTrash));
            if (pigeonsPerDelivery < 1 || pigeonsPerDelivery > MaximumCadence) throw new ArgumentOutOfRangeException(nameof(pigeonsPerDelivery));
            RecyclerCoinsPerTrash = recyclerCoinsPerTrash; PigeonsPerDelivery = pigeonsPerDelivery; CoinsPerDelivery = coinsPerDelivery;
        }
    }

    public readonly struct EffectOwnership
    {
        public int RecyclerLevel { get; }
        public int PigeonLoftLevel { get; }
        public EffectOwnership(int recyclerLevel, int pigeonLoftLevel)
        {
            if (recyclerLevel < 0 || recyclerLevel > 1 || pigeonLoftLevel < 0 || pigeonLoftLevel > 1) throw new ArgumentOutOfRangeException(nameof(recyclerLevel));
            RecyclerLevel = recyclerLevel; PigeonLoftLevel = pigeonLoftLevel;
        }
    }

    // Persist these four fields in the owner's full snapshot. This is one bounded
    // checkpoint, not an ever-growing receipt list. The key detects conflicting replay;
    // it is an identity token, not an authentication mechanism.
    public sealed class DeliveryState
    {
        public long LastOperationSequence { get; }
        public int PigeonProgress { get; }
        public int Cadence { get; }
        public string LastProposalKey { get; }
        public DeliveryState(long lastOperationSequence, int pigeonProgress, int cadence, string lastProposalKey)
        {
            if (lastOperationSequence < 0 || cadence < 1 || cadence > EffectRules.MaximumCadence || pigeonProgress < 0 || pigeonProgress >= cadence) throw new ArgumentOutOfRangeException(nameof(pigeonProgress));
            if (lastOperationSequence == 0 ? pigeonProgress != 0 || lastProposalKey != null : string.IsNullOrEmpty(lastProposalKey) || lastProposalKey.Length > 256) throw new ArgumentException("Invalid delivery checkpoint identity");
            LastOperationSequence = lastOperationSequence; PigeonProgress = pigeonProgress; Cadence = cadence; LastProposalKey = lastProposalKey;
        }
        public static DeliveryState Fresh(EffectRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            return new DeliveryState(0, 0, rules.PigeonsPerDelivery, null);
        }
        internal bool Same(DeliveryState other)
        {
            return other != null && LastOperationSequence == other.LastOperationSequence && PigeonProgress == other.PigeonProgress && Cadence == other.Cadence && LastProposalKey == other.LastProposalKey;
        }
    }

    public sealed class EffectCommit
    {
        public DeliveryState Delivery { get; }
        public long Wallet { get; }
        public bool Applied { get; }
        internal EffectCommit(DeliveryState delivery, long wallet, bool applied) { Delivery = delivery; Wallet = wallet; Applied = applied; }
    }

    // This entire immutable value is the pending transaction proposal. The owner
    // freezes it alongside symbols/RNG/base reward BEFORE applying any wallet change.
    // Its public constructor also validates restored fields using only frozen rules.
    public sealed class FrozenSpinEffects
    {
        public const int FormatVersion = 1;
        public long OperationSequence { get; }
        public SpinOutcome Outcome { get; }
        public EffectOwnership Ownership { get; }
        public EffectRules Rules { get; }
        public DeliveryState Before { get; }
        public DeliveryState After { get; }
        public long RecyclerCoins { get; }
        public long DeliveryCoins { get; }
        public int Deliveries { get; }
        public long TotalCoins { get; }
        public IReadOnlyList<EffectContribution> Contributions { get; }

        public FrozenSpinEffects(int formatVersion, long operationSequence, SpinOutcome outcome, EffectOwnership ownership,
            EffectRules rules, DeliveryState before, int pigeonProgressAfter, int deliveries, long recyclerCoins, long deliveryCoins)
        {
            if (formatVersion != FormatVersion) throw new ArgumentException("Unknown effects proposal version", nameof(formatVersion));
            if (rules == null || before == null) throw new ArgumentNullException(nameof(rules));
            if (operationSequence <= before.LastOperationSequence || operationSequence < 1) throw new ArgumentOutOfRangeException(nameof(operationSequence));
            if (before.Cadence != rules.PigeonsPerDelivery) throw new InvalidOperationException("Delivery cadence change requires an explicit save migration");
            if (ownership.PigeonLoftLevel == 0 && before.PigeonProgress != 0) throw new InvalidOperationException("Unowned delivery progress");
            long expectedRecycler = ownership.RecyclerLevel == 1 ? checked(rules.RecyclerCoinsPerTrash * outcome.Count(SymbolIds.Trash)) : 0;
            int progress = before.PigeonProgress + (ownership.PigeonLoftLevel == 1 ? outcome.Count(SymbolIds.Pigeon) : 0);
            int expectedDeliveries = progress / rules.PigeonsPerDelivery;
            int expectedAfter = progress % rules.PigeonsPerDelivery;
            long expectedDelivery = checked(rules.CoinsPerDelivery * expectedDeliveries);
            if (pigeonProgressAfter != expectedAfter || deliveries != expectedDeliveries || recyclerCoins != expectedRecycler || deliveryCoins != expectedDelivery) throw new ArgumentException("Inconsistent frozen effects proposal");
            OperationSequence = operationSequence; Outcome = outcome; Ownership = ownership; Rules = rules; Before = before;
            RecyclerCoins = recyclerCoins; DeliveryCoins = deliveryCoins; Deliveries = deliveries;
            var contributions = new List<EffectContribution>(2);
            if (ownership.RecyclerLevel == 1) contributions.Add(new EffectContribution(GameIds.RecyclerTrashCoins, recyclerCoins));
            if (ownership.PigeonLoftLevel == 1) contributions.Add(new EffectContribution(GameIds.PigeonDeliveryCoins, deliveryCoins));
            Contributions = contributions.AsReadOnly(); TotalCoins = CoinEffects.Total(Contributions);
            var key = string.Join("/", new[] { "effects.v1", N(operationSequence), N(before.LastOperationSequence), N(before.PigeonProgress),
                N(outcome.Left), N(outcome.Middle), N(outcome.Right), N(ownership.RecyclerLevel), N(ownership.PigeonLoftLevel),
                N(rules.RecyclerCoinsPerTrash), N(rules.PigeonsPerDelivery), N(rules.CoinsPerDelivery), N(pigeonProgressAfter), N(deliveries), N(recyclerCoins), N(deliveryCoins) });
            After = new DeliveryState(operationSequence, pigeonProgressAfter, rules.PigeonsPerDelivery, key);
        }

        static string N(long value) { return value.ToString(CultureInfo.InvariantCulture); }

        // Pure proposal application: GameStore must atomically save Wallet, Delivery,
        // base receipt, contributions and its global sequence before exposing effects.
        // baseReward is the owner's SAME frozen pending amount (including any independently
        // frozen workshop/slot additions once); it must not be recomputed from live tuning.
        // No catalog is consulted here, including during pending-operation recovery.
        public EffectCommit Apply(DeliveryState current, long wallet, long baseReward)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (wallet < 0 || baseReward < 0) throw new ArgumentOutOfRangeException(nameof(wallet));
            if (current.LastOperationSequence == OperationSequence)
            {
                if (!current.Same(After)) throw new InvalidOperationException("Conflicting effects replay");
                return new EffectCommit(current, wallet, false);
            }
            if (!current.Same(Before)) throw new InvalidOperationException("Stale or mismatched effects checkpoint");
            return new EffectCommit(After, checked(checked(wallet + baseReward) + TotalCoins), true);
        }
    }

    public static class SpinEffects
    {
        // Snapshot ownership before the command. The purchase funded by this spin
        // takes effect only for the next spin and cannot count earlier pigeon symbols.
        public static FrozenSpinEffects Prepare(SpinOutcome frozenOutcome, EffectOwnership preCommandOwnership,
            DeliveryState before, long operationSequence, EffectRules rules)
        {
            if (rules == null || before == null) throw new ArgumentNullException(nameof(rules));
            long recycler = preCommandOwnership.RecyclerLevel == 1 ? checked(rules.RecyclerCoinsPerTrash * frozenOutcome.Count(SymbolIds.Trash)) : 0;
            int progress = before.PigeonProgress + (preCommandOwnership.PigeonLoftLevel == 1 ? frozenOutcome.Count(SymbolIds.Pigeon) : 0);
            int deliveries = progress / rules.PigeonsPerDelivery;
            long coins = checked(rules.CoinsPerDelivery * deliveries);
            return new FrozenSpinEffects(FrozenSpinEffects.FormatVersion, operationSequence, frozenOutcome, preCommandOwnership,
                rules, before, progress % rules.PigeonsPerDelivery, deliveries, recycler, coins);
        }
    }
}
