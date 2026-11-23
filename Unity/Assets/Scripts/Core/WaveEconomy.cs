using System;
using JunkToNeon.Contracts;
using JunkToNeon.Effects;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Core
{
    // Optional bounded aggregate. Immutable children can safely be shared by snapshot copies.
    public sealed class WaveProgress
    {
        public int RecyclerBuilt { get; }
        public int PigeonLoftBuilt { get; }
        public DeliveryState Delivery { get; }
        public UpgradeOwnership Slot { get; }
        public WaveProgress(int recyclerBuilt, int pigeonLoftBuilt, DeliveryState delivery, UpgradeOwnership slot)
        {
            if (recyclerBuilt < 0 || recyclerBuilt > 1 || pigeonLoftBuilt < 0 || pigeonLoftBuilt > 1 || delivery == null || slot == null)
                throw new InvalidOperationException("Invalid wave progress");
            if (delivery.Cadence != 3 || (pigeonLoftBuilt == 0 && delivery.PigeonProgress != 0)) throw new InvalidOperationException("Invalid pigeon progress");
            RecyclerBuilt = recyclerBuilt; PigeonLoftBuilt = pigeonLoftBuilt; Delivery = delivery; Slot = slot;
        }
        public static WaveProgress Empty { get; } = new WaveProgress(0, 0, DeliveryState.Fresh(EffectRules.Provisional), UpgradeOwnership.Empty);
        public static WaveProgress Of(ModelState state) { return state.wave ?? Empty; }
    }

    // Format1 is a frozen operation descriptor, not a request to re-run a live catalog.
    public sealed class WaveOperation
    {
        public int Format { get; }
        public int Target { get; }
        public long Price { get; }
        public long BaseCoins { get; }
        public long WorkshopCoins { get; }
        public long SlotCoins { get; }
        public FrozenSpinEffects Effects { get; }
        public UpgradeOwnership Purchase { get; }
        public WaveOperation(int format, int target, long price, long baseCoins, long workshopCoins, long slotCoins,
            FrozenSpinEffects effects, UpgradeOwnership purchase)
        {
            if (format != 1 || target < 0 || target > 5 || price < 0 || baseCoins < 0 || workshopCoins < 0 || slotCoins < 0)
                throw new InvalidOperationException("Invalid frozen wave format");
            Format = format; Target = target; Price = price; BaseCoins = baseCoins; WorkshopCoins = workshopCoins;
            SlotCoins = slotCoins; Effects = effects; Purchase = purchase;
        }
    }

    public static class WaveEconomy
    {
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static bool Same(DeliveryState a, DeliveryState b)
        { return a.LastOperationSequence == b.LastOperationSequence && a.PigeonProgress == b.PigeonProgress && a.Cadence == b.Cadence && a.LastProposalKey == b.LastProposalKey; }
        static void PinnedRules(FrozenSpinEffects e)
        {
            // Format1 stays pinned if a later catalog introduces different tuning.
            Require(e.Rules.RecyclerCoinsPerTrash == 1 && e.Rules.PigeonsPerDelivery == 3 && e.Rules.CoinsPerDelivery == 3, "Unknown frozen effect rules");
        }
        public static WaveOperation Freeze(ModelState s, PendingOperation p)
        {
            var owned = WaveProgress.Of(s);
            if (p.kind == "spin")
            {
                var outcome = new SpinOutcome(p.symbols[0], p.symbols[1], p.symbols[2]);
                return new WaveOperation(1, s.selectedStage, s.selectedStage == 0 ? 0 : GameLogic.TargetPrice(s.selectedStage),
                    GameLogic.Payout(p.symbols), s.workshopBuilt == 1 ? 1 : 0, CoinBoostShop.Evaluate(outcome, owned.Slot).Coins,
                    SpinEffects.Prepare(outcome, new EffectOwnership(owned.RecyclerBuilt, owned.PigeonLoftBuilt), owned.Delivery, p.sequence, ProgressionCatalog.Rules), null);
            }
            if (p.kind == "select") return new WaveOperation(1, p.target, GameLogic.TargetPrice(p.target), 0, 0, 0, null, null);
            if (p.kind == "upgrade")
            {
                var request = new UpgradePurchaseRequest(GameIds.SlotCoinBoost, p.sequence, owned.Slot.Revision, s.coins, s.houseStage);
                var result = CoinBoostShop.Propose(s.sequence, s.coins, s.houseStage, owned.Slot, request);
                Require(result.Status == PurchaseStatus.Proposed && result.Proposal.MatchesPreconditions(s.sequence, s.coins, s.houseStage, owned.Slot), "Slot upgrade is not available");
                return new WaveOperation(1, 0, result.Proposal.FrozenPrice, 0, 0, 0, null, result.Proposal.OwnershipAfter);
            }
            return null;
        }
        public static void Validate(ModelState s)
        {
            var owned = WaveProgress.Of(s);
            Require(s.houseStage >= 1 || (owned.RecyclerBuilt == 0 && owned.PigeonLoftBuilt == 0 && owned.Slot.Level == 0), "Wave ownership prerequisites missing");
            Require(owned.Delivery.LastOperationSequence <= s.sequence && owned.Slot.PurchaseSequence <= s.sequence, "Future wave receipt");
            if (s.lastSpin?.wave != null)
            {
                var r = s.lastSpin; var w = r.wave; var e = w.Effects;
                Require(e != null && w.Purchase == null && e.OperationSequence == r.sequence && w.BaseCoins == r.reward && w.WorkshopCoins == r.bonus && w.SlotCoins <= owned.Slot.Level, "Invalid wave spin receipt");
                PinnedRules(e);
                Require(e.Outcome.Left == r.symbols[0] && e.Outcome.Middle == r.symbols[1] && e.Outcome.Right == r.symbols[2] &&
                    e.Ownership.RecyclerLevel <= owned.RecyclerBuilt && e.Ownership.PigeonLoftLevel <= owned.PigeonLoftBuilt && Same(e.After, owned.Delivery), "Spin/counter receipt mismatch");
                Require(w.Target == 0 ? w.Price == 0 : w.Price == GameLogic.TargetPrice(w.Target), "Unknown receipt price");
            }
            else Require(owned.Delivery.LastOperationSequence == 0, "Missing effect receipt");
            var p = s.pending;
            if (p == null) return;
            var frozen = p.wave;
            if (frozen == null)
            {
                Require(p.kind != "upgrade" && !(p.kind == "select" && (p.target >= 4 || s.wave != null)), "Missing new operation descriptor");
                Require(p.kind != "spin" || s.wave == null, "Missing frozen spin effects");
                return;
            }
            if (p.kind == "spin")
            {
                var e = frozen.Effects;
                Require(e != null && frozen.Purchase == null && e.OperationSequence == p.sequence && Same(e.Before, owned.Delivery), "Invalid pending effect checkpoint");
                PinnedRules(e);
                Require(e.Outcome.Left == p.symbols[0] && e.Outcome.Middle == p.symbols[1] && e.Outcome.Right == p.symbols[2] && e.Ownership.RecyclerLevel == owned.RecyclerBuilt && e.Ownership.PigeonLoftLevel == owned.PigeonLoftBuilt, "Effect ownership/outcome mismatch");
                Require(frozen.BaseCoins == GameLogic.Payout(p.symbols) && frozen.WorkshopCoins == s.workshopBuilt && frozen.SlotCoins == owned.Slot.Level && frozen.Target == s.selectedStage, "Frozen credit mismatch");
            }
            else
            {
                Require(frozen.Effects == null && frozen.BaseCoins == 0 && frozen.WorkshopCoins == 0 && frozen.SlotCoins == 0, "Unexpected non-spin credits");
                if (p.kind == "upgrade")
                {
                    var purchase = frozen.Purchase;
                    Require(p.target == 0 && frozen.Target == 0 && frozen.Price == 120 && owned.Slot.Level == 0 && s.houseStage >= 1 && s.coins >= 120 &&
                        purchase != null && purchase.Level == 1 && purchase.PurchaseSequence == p.sequence && purchase.PurchaseWalletBefore == s.coins && purchase.PurchaseHouseStage == s.houseStage, "Invalid frozen slot purchase");
                    return;
                }
                Require(p.kind == "select" && frozen.Purchase == null && frozen.Target == p.target, "Unexpected wave descriptor");
            }
            Require(frozen.Target == 0 ? frozen.Price == 0 : frozen.Price == GameLogic.TargetPrice(frozen.Target), "Invalid frozen project price");
        }
        public static void ApplySpin(ModelState before, ModelState next, PendingOperation p)
        {
            var owned = WaveProgress.Of(before); var w = p.wave;
            var result = w.Effects.Apply(owned.Delivery, before.coins, checked(checked(w.BaseCoins + w.WorkshopCoins) + w.SlotCoins));
            Require(result.Applied, "Pending effect was already applied");
            next.coins = result.Wallet;
            next.wave = new WaveProgress(owned.RecyclerBuilt, owned.PigeonLoftBuilt, result.Delivery, owned.Slot);
        }
        public static void ApplyUpgrade(ModelState before, ModelState next, PendingOperation p)
        {
            var owned = WaveProgress.Of(before);
            next.coins = checked(before.coins - p.wave.Price);
            next.wave = new WaveProgress(owned.RecyclerBuilt, owned.PigeonLoftBuilt, owned.Delivery, p.wave.Purchase);
        }
        public static void Build(ModelState next, int target)
        {
            var owned = WaveProgress.Of(next);
            next.wave = new WaveProgress(target == 4 ? 1 : owned.RecyclerBuilt, target == 5 ? 1 : owned.PigeonLoftBuilt, owned.Delivery, owned.Slot);
        }
    }
}
