using System;
using JunkToNeon.Contracts;

namespace JunkToNeon.Upgrades
{
    public sealed class UpgradeOffer
    {
        public string OfferId => GameIds.SlotCoinBoost;
        public string UpgradeId => GameIds.SlotCoinBoost;
        public string CurrencyId => GameIds.Coins;
        public string PrerequisiteId => GameIds.HouseRepair;
        public int RequiredHouseStage => 1;
        public long Price => 120;
        public int LevelCap => 1;
        public long AdditiveCoinsPerSpin => 1;
        public bool Provisional => true;
        public bool LiveEconomyEnabled => false;
        internal UpgradeOffer() { }
    }

    // Fixed-size state for the sole registered prototype upgrade and its one purchase receipt.
    [Serializable]
    public sealed class UpgradeOwnership
    {
        public int Version { get; }
        public int Level { get; }
        public long Revision { get; }
        public long PurchaseSequence { get; }
        public long PurchaseWalletBefore { get; }
        public int PurchaseHouseStage { get; }
        public static UpgradeOwnership Empty { get; } = new UpgradeOwnership(1, 0, 0, 0, 0, 0);
        internal UpgradeOwnership(int version, int level, long revision, long sequence, long walletBefore, int houseStage)
        {
            if (version != 1 || level < 0 || level > 1 || revision != level || sequence < 0 || walletBefore < 0)
                throw new ArgumentException("Invalid bounded upgrade ownership");
            if (level == 0 ? sequence != 0 || walletBefore != 0 || houseStage != 0
                : sequence == 0 || walletBefore < 120 || houseStage < 1 || houseStage > 2)
                throw new ArgumentException("Invalid upgrade purchase receipt");
            Version = version; Level = level; Revision = revision; PurchaseSequence = sequence;
            PurchaseWalletBefore = walletBefore; PurchaseHouseStage = houseStage;
        }
    }

    public enum OfferStatus { Locked, Available, InsufficientFunds, Maxed }
    public sealed class UpgradePreview
    {
        public UpgradeOffer Offer { get; }
        public OfferStatus Status { get; }
        public int Level { get; }
        public bool Owned => Level > 0;
        public bool Maxed => Level == Offer.LevelCap;
        internal UpgradePreview(UpgradeOffer offer, OfferStatus status, int level)
        { Offer = offer; Status = status; Level = level; }
    }

    public sealed class UpgradePurchaseRequest
    {
        public string OfferId { get; }
        public long Sequence { get; }
        public long ExpectedOwnershipRevision { get; }
        public long ExpectedWallet { get; }
        public int ExpectedHouseStage { get; }
        public UpgradePurchaseRequest(string offerId, long sequence, long expectedOwnershipRevision, long expectedWallet, int expectedHouseStage)
        {
            OfferId = offerId; Sequence = sequence; ExpectedOwnershipRevision = expectedOwnershipRevision;
            ExpectedWallet = expectedWallet; ExpectedHouseStage = expectedHouseStage;
        }
    }

    public sealed class UpgradePurchaseProposal
    {
        public UpgradePurchaseRequest Request { get; }
        public long ExpectedGlobalSequence => Request.Sequence - 1;
        public UpgradeOwnership OwnershipBefore { get; }
        public UpgradeOwnership OwnershipAfter { get; }
        public long WalletBefore => Request.ExpectedWallet;
        public long WalletAfter { get; }
        public long FrozenPrice { get; }
        public int LevelBefore => OwnershipBefore.Level;
        public int LevelAfter => OwnershipAfter.Level;
        public string UpgradeId => GameIds.SlotCoinBoost;
        internal UpgradePurchaseProposal(UpgradePurchaseRequest request, UpgradeOwnership before, UpgradeOwnership after, long price)
        {
            Request = request; OwnershipBefore = before; OwnershipAfter = after; FrozenPrice = price;
            WalletAfter = checked(request.ExpectedWallet - price);
        }
        public bool MatchesPreconditions(long sequence, long wallet, int houseStage, UpgradeOwnership ownership)
        {
            return ownership != null && sequence == ExpectedGlobalSequence && wallet == WalletBefore &&
                houseStage == Request.ExpectedHouseStage && ownership.Revision == OwnershipBefore.Revision &&
                ownership.Level == OwnershipBefore.Level && ownership.PurchaseSequence == OwnershipBefore.PurchaseSequence;
        }
    }

    public enum PurchaseStatus { Proposed, Replay, Locked, InsufficientFunds, Maxed, Stale, Conflict, InvalidRequest, UnknownOffer, Overflow }
    public sealed class UpgradePurchaseResult
    {
        public PurchaseStatus Status { get; }
        public UpgradePurchaseProposal Proposal { get; }
        internal UpgradePurchaseResult(PurchaseStatus status, UpgradePurchaseProposal proposal = null)
        { Status = status; Proposal = proposal; }
    }

    public static class CoinBoostShop
    {
        public static UpgradeOffer Offer { get; } = new UpgradeOffer();
        static void ValidateContext(long wallet, int houseStage, UpgradeOwnership ownership)
        {
            if (ownership == null) throw new ArgumentNullException(nameof(ownership));
            if (wallet < 0 || houseStage < 0 || houseStage > 2) throw new ArgumentOutOfRangeException(nameof(wallet));
        }
        public static UpgradePreview Preview(long wallet, int houseStage, UpgradeOwnership ownership)
        {
            ValidateContext(wallet, houseStage, ownership);
            var status = ownership.Level == Offer.LevelCap ? OfferStatus.Maxed : houseStage < Offer.RequiredHouseStage
                ? OfferStatus.Locked : wallet < Offer.Price ? OfferStatus.InsufficientFunds : OfferStatus.Available;
            return new UpgradePreview(Offer, status, ownership.Level);
        }
        public static UpgradePurchaseResult Propose(long currentSequence, long wallet, int houseStage,
            UpgradeOwnership ownership, UpgradePurchaseRequest request)
        {
            ValidateContext(wallet, houseStage, ownership);
            if (request == null || request.Sequence < 1 || request.ExpectedOwnershipRevision < 0 ||
                request.ExpectedWallet < 0 || request.ExpectedHouseStage < 0 || request.ExpectedHouseStage > 2 || currentSequence < 0)
                return new UpgradePurchaseResult(PurchaseStatus.InvalidRequest);
            if (!string.Equals(request.OfferId, Offer.OfferId, StringComparison.Ordinal))
                return new UpgradePurchaseResult(PurchaseStatus.UnknownOffer);
            if (ownership.PurchaseSequence > currentSequence) return new UpgradePurchaseResult(PurchaseStatus.Conflict);
            if (ownership.PurchaseSequence == request.Sequence)
            {
                bool same = request.ExpectedOwnershipRevision == 0 && request.ExpectedWallet == ownership.PurchaseWalletBefore &&
                    request.ExpectedHouseStage == ownership.PurchaseHouseStage;
                return new UpgradePurchaseResult(same ? PurchaseStatus.Replay : PurchaseStatus.Conflict);
            }
            if (currentSequence == long.MaxValue) return new UpgradePurchaseResult(PurchaseStatus.Overflow);
            if (request.Sequence < ownership.PurchaseSequence || request.Sequence <= currentSequence)
                return new UpgradePurchaseResult(PurchaseStatus.Stale);
            if (request.Sequence != currentSequence + 1 || request.ExpectedWallet != wallet ||
                request.ExpectedHouseStage != houseStage || request.ExpectedOwnershipRevision != ownership.Revision)
                return new UpgradePurchaseResult(PurchaseStatus.Conflict);
            var preview = Preview(wallet, houseStage, ownership);
            if (preview.Status != OfferStatus.Available)
                return new UpgradePurchaseResult(preview.Status == OfferStatus.Locked ? PurchaseStatus.Locked :
                    preview.Status == OfferStatus.Maxed ? PurchaseStatus.Maxed : PurchaseStatus.InsufficientFunds);
            var after = new UpgradeOwnership(1, 1, 1, request.Sequence, wallet, houseStage);
            return new UpgradePurchaseResult(PurchaseStatus.Proposed, new UpgradePurchaseProposal(request, ownership, after, Offer.Price));
        }
        // Always evaluate PRE-command ownership. A proposed purchase does not activate this effect.
        public static EffectContribution Evaluate(SpinOutcome frozenOutcome, UpgradeOwnership preCommandOwnership)
        {
            if (preCommandOwnership == null) throw new ArgumentNullException(nameof(preCommandOwnership));
            return new EffectContribution(GameIds.SlotCoinBoost, checked(preCommandOwnership.Level * Offer.AdditiveCoinsPerSpin));
        }
    }
}
