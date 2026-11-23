using System;
using System.Collections.Generic;
using JunkToNeon.Contracts;
using JunkToNeon.Effects;

namespace JunkToNeon.Tests
{
    public static class EffectsRegression
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Fails<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new Exception(message);
        }
        static FrozenSpinEffects Prepare(DeliveryState before, long sequence, SpinOutcome outcome, int recycler = 1, int loft = 1, EffectRules rules = null)
        {
            return SpinEffects.Prepare(outcome, new EffectOwnership(recycler, loft), before, sequence, rules ?? EffectRules.Provisional);
        }
        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>();
            var rules = EffectRules.Provisional;
            var fresh = DeliveryState.Fresh(rules);
            var none = Prepare(fresh, 1, new SpinOutcome(3, 2, 2), 0, 0);
            Require(none.TotalCoins == 0 && none.Contributions.Count == 0 && none.After.PigeonProgress == 0, "Unowned effects paid or counted symbols");
            var noneCommit = none.Apply(fresh, 37, 8);
            Require(noneCommit.Wallet == 45 && noneCommit.Applied, "Base payout changed by absent effects");
            passed.Add("No ownership: zero bonus and carry; base8 remains8");

            var trash = Prepare(fresh, 1, new SpinOutcome(2, 2, 2), 1, 0);
            Require(trash.RecyclerCoins == 3 && trash.TotalCoins == 3 && trash.Contributions.Count == 1 && trash.Contributions[0].EffectId == GameIds.RecyclerTrashCoins, "Triple trash contribution");
            Require(trash.Apply(fresh, 0, 23).Wallet == 26, "Recycler multiplied or replaced exclusive triple23");
            var noTrash = Prepare(fresh, 1, new SpinOutcome(0, 1, 3), 1, 0);
            Require(noTrash.TotalCoins == 0 && noTrash.Contributions.Count == 1 && noTrash.Contributions[0].Coins == 0, "Zero-match descriptor");
            passed.Add("Recycler counts each trash once, preserves exclusive base23, identifies zero-match effect");

            var first = Prepare(fresh, 2, new SpinOutcome(3, 3, 0), 0, 1);
            Require(first.TotalCoins == 0 && first.Deliveries == 0 && first.After.PigeonProgress == 2, "First two pigeons paid early");
            var firstCommit = first.Apply(fresh, 0, 8);
            var threshold = Prepare(firstCommit.Delivery, 7, new SpinOutcome(3, 0, 1), 0, 1);
            Require(threshold.Deliveries == 1 && threshold.DeliveryCoins == 3 && threshold.After.PigeonProgress == 0 && threshold.Apply(firstCommit.Delivery, firstCommit.Wallet, 3).Wallet == 14, "Third pigeon threshold or sparse operation identity");
            var triple = Prepare(fresh, 1, new SpinOutcome(3, 3, 3), 0, 1);
            Require(triple.Deliveries == 1 && triple.TotalCoins == 3 && triple.After.PigeonProgress == 0 && triple.Apply(fresh, 0, 23).Wallet == 26, "Triple pigeon first eligible spin");
            passed.Add("Delivery threshold3 across sparse command IDs; first eligible triple pigeon delivers once");

            var mixed = Prepare(firstCommit.Delivery, 9, new SpinOutcome(3, 2, 2));
            Require(mixed.RecyclerCoins == 2 && mixed.DeliveryCoins == 3 && mixed.TotalCoins == 5 && mixed.After.PigeonProgress == 0 && mixed.Contributions.Count == 2, "Combined independent contributions");
            Require(mixed.Contributions[0].EffectId == GameIds.RecyclerTrashCoins && mixed.Contributions[1].EffectId == GameIds.PigeonDeliveryCoins && mixed.Apply(firstCommit.Delivery, 10, 8).Wallet == 23, "Combined base8 plus2 plus3");
            var all = 0;
            for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) for (int c = 0; c < 4; c++)
            {
                var p = Prepare(firstCommit.Delivery, 10, new SpinOutcome(a, b, c));
                int trashCount = (a == 2 ? 1 : 0) + (b == 2 ? 1 : 0) + (c == 2 ? 1 : 0);
                int pigeonCount = (a == 3 ? 1 : 0) + (b == 3 ? 1 : 0) + (c == 3 ? 1 : 0);
                Require(p.RecyclerCoins == trashCount && p.DeliveryCoins == (pigeonCount >= 1 ? 3 : 0) && p.After.PigeonProgress == (2 + pigeonCount) % 3, "64-outcome independent expectation"); all++;
            }
            Require(all == 64, "Missing outcomes");
            passed.Add("Both effects: carry2+pigeon/trash/trash gives2+3; all64 outcomes independently checked");

            var acquisition = Prepare(fresh, 1, new SpinOutcome(3, 3, 3), 0, 0);
            var acquired = acquisition.Apply(fresh, 100, 23);
            var subsequent = Prepare(acquired.Delivery, 3, new SpinOutcome(3, 0, 1), 0, 1);
            Require(acquired.Wallet == 123 && subsequent.After.PigeonProgress == 1 && subsequent.TotalCoins == 0, "Purchase spin accumulated retroactive delivery progress");
            var acquisitionTrash = Prepare(fresh, 1, new SpinOutcome(2, 2, 2), 0, 0);
            Require(acquisitionTrash.TotalCoins == 0 && Prepare(acquisitionTrash.After, 2, new SpinOutcome(2, 2, 2), 1, 0).TotalCoins == 3, "Recycler purchase spin paid retroactively");
            passed.Add("Acquisition uses pre-command ownership; first subsequent spin starts counting at zero");

            var custom = new EffectRules(2, 1, 4);
            var customTriple = Prepare(DeliveryState.Fresh(custom), 11, new SpinOutcome(3, 3, 3), 1, 1, custom);
            Require(customTriple.Deliveries == 3 && customTriple.TotalCoins == 12 && customTriple.After.PigeonProgress == 0, "Cadence1 did not count multiple deliveries");
            Require(Prepare(DeliveryState.Fresh(custom), 11, new SpinOutcome(2, 2, 3), 1, 1, custom).TotalCoins == 8, "Configurable2/trash plus4/delivery");
            passed.Add("Data-defined cadence1 supports three deliveries in one frozen outcome");

            var restored = new FrozenSpinEffects(1, mixed.OperationSequence, mixed.Outcome, mixed.Ownership,
                new EffectRules(1, 3, 3), new DeliveryState(firstCommit.Delivery.LastOperationSequence, 2, 3, firstCommit.Delivery.LastProposalKey), 0, 1, 2, 3);
            var restoredCommit = restored.Apply(firstCommit.Delivery, 10, 8);
            Require(restoredCommit.Wallet == 23 && restoredCommit.Delivery.LastProposalKey == mixed.After.LastProposalKey, "Restored proposal changed frozen result");
            var changedLiveRules = new EffectRules(99, 4, 99);
            Require(changedLiveRules.CoinsPerDelivery == 99 && restored.Apply(firstCommit.Delivery, 10, 8).Wallet == 23, "Frozen apply consulted live tuning");
            var duplicate = restored.Apply(restoredCommit.Delivery, restoredCommit.Wallet, 8);
            Require(!duplicate.Applied && duplicate.Wallet == 23 && duplicate.Delivery.PigeonProgress == 0, "Replay paid base or effects twice");
            Fails<ArgumentOutOfRangeException>(() => Prepare(restoredCommit.Delivery, 9, new SpinOutcome(0, 0, 0)), "Duplicate reserve accepted");
            Fails<InvalidOperationException>(() => mixed.Apply(threshold.After, 0, 8), "Wrong pre-state accepted");
            var conflict = Prepare(firstCommit.Delivery, 9, new SpinOutcome(0, 0, 0));
            Fails<InvalidOperationException>(() => conflict.Apply(restoredCommit.Delivery, 23, 23), "Conflicting sequence replay accepted");
            passed.Add("Frozen restore ignores changed live tuning; duplicate no-op, stale/conflicting replay rejected");

            Fails<ArgumentException>(() => new FrozenSpinEffects(2, 9, mixed.Outcome, mixed.Ownership, rules, firstCommit.Delivery, 0, 1, 2, 3), "Unknown frozen version accepted");
            Fails<ArgumentException>(() => new FrozenSpinEffects(1, 9, mixed.Outcome, mixed.Ownership, rules, firstCommit.Delivery, 0, 1, 3, 3), "Altered recycler delta accepted");
            Fails<ArgumentException>(() => new FrozenSpinEffects(1, 9, mixed.Outcome, mixed.Ownership, rules, firstCommit.Delivery, 1, 1, 2, 3), "Altered carry accepted");
            Fails<InvalidOperationException>(() => Prepare(firstCommit.Delivery, 9, mixed.Outcome, 1, 1, changedLiveRules), "Cadence changed without migration");
            Fails<InvalidOperationException>(() => Prepare(firstCommit.Delivery, 9, mixed.Outcome, 1, 0), "Unowned carry accepted");
            passed.Add("Strict frozen version/delta/carry validation; cadence migration and unowned carry fail closed");

            Fails<ArgumentOutOfRangeException>(() => new SpinOutcome(0, 1, 4), "Invalid symbol accepted");
            Fails<ArgumentOutOfRangeException>(() => new EffectOwnership(2, 0), "Unsupported ownership level accepted");
            Fails<ArgumentOutOfRangeException>(() => new EffectRules(-1, 3, 3), "Negative recycler value accepted");
            Fails<ArgumentOutOfRangeException>(() => new EffectRules(1, 0, 3), "Zero cadence accepted");
            Fails<ArgumentOutOfRangeException>(() => new EffectRules(1, EffectRules.MaximumCadence + 1, 3), "Unbounded cadence accepted");
            Fails<ArgumentOutOfRangeException>(() => new DeliveryState(1, 3, 3, "test"), "Out of range carry accepted");
            Fails<ArgumentException>(() => new DeliveryState(0, 0, 3, "test"), "Fresh checkpoint with history accepted");
            Fails<ArgumentException>(() => new DeliveryState(1, 0, 3, new string('x', 257)), "Unbounded key accepted");
            passed.Add("Three-symbol/level/config/checkpoint bounds are explicit and validated");

            Fails<OverflowException>(() => Prepare(fresh, 1, new SpinOutcome(2, 2, 2), 1, 0, new EffectRules(long.MaxValue, 3, 3)), "Recycler multiplication overflow accepted");
            Fails<OverflowException>(() => Prepare(DeliveryState.Fresh(custom), 1, new SpinOutcome(3, 3, 3), 0, 1, new EffectRules(1, 1, long.MaxValue)), "Delivery multiplication overflow accepted");
            var sumRules = new EffectRules(long.MaxValue, 1, 1);
            Fails<OverflowException>(() => Prepare(DeliveryState.Fresh(sumRules), 1, new SpinOutcome(2, 3, 0), 1, 1, sumRules), "Combined contribution overflow accepted");
            Fails<OverflowException>(() => mixed.Apply(firstCommit.Delivery, long.MaxValue - 8, 8), "Wallet bonus overflow accepted");
            Fails<OverflowException>(() => mixed.Apply(firstCommit.Delivery, long.MaxValue, 8), "Wallet base overflow accepted");
            Require(mixed.Apply(firstCommit.Delivery, long.MaxValue - 13, 8).Wallet == long.MaxValue, "Exact wallet long boundary failed");
            var exactSumRules = new EffectRules(long.MaxValue - 1, 1, 1);
            Require(Prepare(DeliveryState.Fresh(exactSumRules), 1, new SpinOutcome(2, 3, 0), 1, 1, exactSumRules).TotalCoins == long.MaxValue, "Exact contribution sum boundary failed");
            Require(firstCommit.Delivery.PigeonProgress == 2 && firstCommit.Delivery.LastOperationSequence == 2, "Overflow mutated input checkpoint");
            Require(Prepare(fresh, long.MaxValue, new SpinOutcome(0, 1, 2)).OperationSequence == long.MaxValue, "Maximum operation ID unsupported");
            passed.Add("Checked multiplication/sum/base/wallet boundaries; overflow leaves inputs unchanged");

            var list = mixed.Contributions as IList<EffectContribution>;
            Require(list != null && list.IsReadOnly, "Contributions expose mutable collection");
            Fails<NotSupportedException>(() => list[0] = new EffectContribution(GameIds.RecyclerTrashCoins, 999), "Contribution list mutated");
            Require(mixed.TotalCoins == 5 && mixed.Before.SameForTest(firstCommit.Delivery), "Immutable result changed");
            passed.Add("Immutable outcome/config/checkpoints and read-only contributions retain no caller arrays");
            return passed;
        }
        static bool SameForTest(this DeliveryState a, DeliveryState b)
        {
            return a.LastOperationSequence == b.LastOperationSequence && a.PigeonProgress == b.PigeonProgress && a.Cadence == b.Cadence && a.LastProposalKey == b.LastProposalKey;
        }
    }
}
