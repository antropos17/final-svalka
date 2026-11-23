using System;
using System.Collections.Generic;
using System.Globalization;
using JunkToNeon.Contracts;

namespace JunkToNeon.Upgrades
{
    // Primitive additive modifiers; the integration evaluates qualifying outcomes and delivery counts.
    public sealed class SkillModifiers
    {
        public long UniversalCoins { get; }
        public long CoinsPerTrash { get; }
        public long CoinsPerDelivery { get; }
        internal SkillModifiers(long universal, long trash, long delivery)
        { UniversalCoins = universal; CoinsPerTrash = trash; CoinsPerDelivery = delivery; }
    }

    public sealed class SkillContext
    {
        public FamilyStages Stages { get; }
        public int Era => EraId == EraIds.Junkyard ? 1 : 2;
        public int HouseStage => Stages.AbsoluteLevel(FamilyIds.House);
        public int WorkshopLevel => Stages.AbsoluteLevel(FamilyIds.Workshop);
        public int RecyclerLevel => Stages.AbsoluteLevel(FamilyIds.Recycler);
        public int PigeonLoftLevel => Stages.AbsoluteLevel(FamilyIds.PigeonLoft);
        public int CartStage => Stages.AbsoluteLevel(FamilyIds.Cart);
        public int PowerSourceStage => Stages.AbsoluteLevel(FamilyIds.PowerSource);
        public string EraId => Stages.EraId;
        public string SelectedConstruction { get; }
        public SkillContext(FamilyStages stages, string selectedConstruction = null)
        {
            Stages = stages ?? throw new ArgumentNullException(nameof(stages));
            if (selectedConstruction != null && !ChapterIds.IsProject(selectedConstruction, stages.EraId))
                throw new ArgumentException("Unknown selected construction for this era");
            SelectedConstruction = selectedConstruction;
        }
        internal string Token => Era + ":" + HouseStage + ":" + WorkshopLevel + ":" + RecyclerLevel + ":" + PigeonLoftLevel + ":" + CartStage + ":" + PowerSourceStage + ":" + (SelectedConstruction ?? "-");
        internal bool Same(SkillContext other) => other != null && Token == other.Token;
        internal bool AtLeast(SkillContext earlier)
        {
            foreach (string family in FamilyIds.All)
                if (Stages.AbsoluteLevel(family) < earlier.Stages.AbsoluteLevel(family)) return false;
            return true;
        }
    }

    public sealed class SkillNode
    {
        public string Id { get; }
        public int Branch { get; }
        public int Tier { get; }
        public long Price { get; }
        public string PricingRevision { get; }
        public int LevelCap => 1;
        public string CurrencyId => GameIds.Coins;
        public int RequiredEra => Tier;
        public string RequiredSkillId { get; }
        public string RequiredProjectId { get; }
        public int RequiredProjectLevel => Tier == 1 ? 1 : 4;
        public string RequiredEraId => Tier == 1 ? EraIds.Junkyard : EraIds.WorkshopYard;
        public string RequiredEraUnlockId => Tier == 1 ? null : ChapterIds.UnlockWorkshopYard;
        public string RequiredFamilyId => Branch == 0 ? FamilyIds.House : Branch == 1 ? FamilyIds.Recycler : FamilyIds.PigeonLoft;
        public long AdditiveEffect => 1;
        public bool Provisional => true;
        internal SkillNode(string id, int branch, int tier, long price, string prerequisite, string project, string pricingRevision)
        { Id = id; Branch = branch; Tier = tier; Price = price; RequiredSkillId = prerequisite; RequiredProjectId = project; PricingRevision = pricingRevision; }
    }

    public sealed class SkillQuote
    {
        public SkillNode Node { get; }
        public OfferStatus Status { get; }
        public bool Owned { get; }
        public SkillModifiers CurrentEffects { get; }
        public SkillModifiers NextEffects { get; }
        public IReadOnlyList<string> Unlocks { get; }
        public IReadOnlyList<SkillUnlock> NextUnlocks { get; }
        public SkillPurchaseRequest Request { get; }
        internal SkillQuote(SkillNode node, OfferStatus status, bool owned, SkillModifiers current, SkillModifiers next,
            IReadOnlyList<string> unlocks, IReadOnlyList<SkillUnlock> nextUnlocks, SkillPurchaseRequest request)
        { Node = node; Status = status; Owned = owned; CurrentEffects = current; NextEffects = next; Unlocks = unlocks; NextUnlocks = nextUnlocks; Request = request; }
    }

    public sealed class SkillUnlock
    {
        public SkillNode Node { get; }
        public bool PrerequisitesMetAfterPurchase { get; }
        internal SkillUnlock(SkillNode node, bool met) { Node = node; PrerequisitesMetAfterPurchase = met; }
    }

    public sealed class SkillPurchaseRequest
    {
        public string NodeId { get; }
        public string OperationId { get; }
        public long Sequence { get; }
        public long ExpectedWallet { get; }
        public string ExpectedOwnership { get; }
        public SkillContext Context { get; }
        public string PricingRevision { get; }
        public SkillPurchaseRequest(string nodeId, string operationId, long sequence, long expectedWallet,
            string expectedOwnership, SkillContext context, string pricingRevision = SkillPricing.LegacyRevision)
        { NodeId = nodeId; OperationId = operationId; Sequence = sequence; ExpectedWallet = expectedWallet;
            ExpectedOwnership = expectedOwnership; Context = context; PricingRevision = pricingRevision; }
        internal bool Same(SkillPurchaseRequest other) => other != null && NodeId == other.NodeId && OperationId == other.OperationId &&
            Sequence == other.Sequence && ExpectedWallet == other.ExpectedWallet && ExpectedOwnership == other.ExpectedOwnership &&
            PricingRevision == other.PricingRevision && Context.Same(other.Context);
    }

    public sealed class SkillReceipt
    {
        public SkillPurchaseRequest Request { get; }
        public bool Legacy { get; }
        public long FrozenPrice { get; }
        public string PricingRevision => Request.PricingRevision;
        public int LegacyHouseStage { get; }
        internal SkillReceipt(SkillPurchaseRequest request, bool legacy, long price, int legacyHouseStage = 0)
        { Request = request; Legacy = legacy; FrozenPrice = price; LegacyHouseStage = legacyHouseStage; }
        // Excludes the preceding ownership token, preventing recursively growing history.
        internal string Identity => Request.NodeId + "/" + Request.OperationId + "/" + Request.Sequence.ToString(CultureInfo.InvariantCulture) +
            "/" + Request.ExpectedWallet.ToString(CultureInfo.InvariantCulture) + "/" + (Legacy ? "legacy:" + LegacyHouseStage : Request.Context.Token) + "/" + (Legacy ? "1" : "0") +
            (PricingRevision == SkillPricing.LegacyRevision ? "" : "/" + PricingRevision);
    }

    public sealed class SkillOwnership
    {
        public const int MaximumReceipts = 6;
        public int Version { get; }
        public int Revision => Receipts.Count;
        public IReadOnlyList<SkillReceipt> Receipts { get; }
        public static SkillOwnership Empty { get; } = new SkillOwnership(new SkillReceipt[0]);
        public string Token { get; }
        internal SkillOwnership(IReadOnlyList<SkillReceipt> receipts)
        {
            if (receipts == null || receipts.Count > MaximumReceipts) throw new ArgumentException("Too many skill receipts");
            var copy = new List<SkillReceipt>();
            var nodes = new HashSet<string>(StringComparer.Ordinal);
            var operations = new HashSet<string>(StringComparer.Ordinal);
            long priorSequence = 0;
            foreach (var receipt in receipts)
            {
                if (receipt == null || receipt.Request == null) throw new ArgumentException("Missing skill receipt");
                var request = receipt.Request;
                var node = SkillPricing.IsKnown(request.PricingRevision) ? SkillTree.Find(request.NodeId, request.PricingRevision) : null;
                bool validRequest = receipt.Legacy ? request.PricingRevision == SkillPricing.LegacyRevision && StableId.IsValid(request.OperationId) && request.Sequence > 0 && request.ExpectedWallet >= 0 : SkillTree.ValidRequest(request);
                if (node == null || !validRequest || request.Sequence <= priorSequence ||
                    !nodes.Add(node.Id) || !operations.Add(request.OperationId) || receipt.FrozenPrice != node.Price || request.ExpectedWallet < node.Price)
                    throw new ArgumentException("Invalid skill receipt");
                string beforeToken = MakeToken(copy);
                if (receipt.Legacy)
                {
                    if (copy.Count != 0 || node.Id != GameIds.SlotCoinBoost || request.ExpectedOwnership != "empty" ||
                        request.OperationId != "legacy.slot-" + request.Sequence.ToString(CultureInfo.InvariantCulture) ||
                        request.Context != null || receipt.LegacyHouseStage < 1 || receipt.LegacyHouseStage > 2)
                        throw new ArgumentException("Invalid legacy skill receipt");
                }
                else if (receipt.LegacyHouseStage != 0 || request.ExpectedOwnership != beforeToken || !SkillTree.Unlocked(node, request.Context, copy))
                    throw new ArgumentException("Invalid skill purchase preconditions");
                if (!receipt.Legacy) foreach (var prior in copy)
                    if (!SkillTree.Unlocked(SkillTree.Find(prior.Request.NodeId), request.Context, copy) ||
                        (prior.Legacy ? request.Context.HouseStage < prior.LegacyHouseStage : !request.Context.AtLeast(prior.Request.Context)))
                        throw new ArgumentException("Receipt context contradicts prior owned skill prerequisites");
                copy.Add(receipt); priorSequence = request.Sequence;
            }
            Receipts = copy.AsReadOnly(); Token = MakeToken(copy); Version = 2;
            foreach (var receipt in copy) if (receipt.PricingRevision != SkillPricing.LegacyRevision) Version = 3;
        }
        static string MakeToken(IReadOnlyList<SkillReceipt> receipts)
        {
            if (receipts.Count == 0) return "empty";
            var parts = new string[receipts.Count];
            for (int i = 0; i < parts.Length; i++) parts[i] = receipts[i].Identity;
            return string.Join("|", parts);
        }
        public bool Owns(string nodeId)
        { foreach (var receipt in Receipts) if (receipt.Request.NodeId == nodeId) return true; return false; }
        public static SkillOwnership Migrate(UpgradeOwnership legacy)
        {
            if (legacy == null) throw new ArgumentNullException(nameof(legacy));
            if (legacy.Level == 0) return Empty;
            return new SkillOwnership(new[] { new SkillReceipt(new SkillPurchaseRequest(GameIds.SlotCoinBoost,
                "legacy.slot-" + legacy.PurchaseSequence.ToString(CultureInfo.InvariantCulture), legacy.PurchaseSequence,
                legacy.PurchaseWalletBefore, "empty", null), true, 120, legacy.PurchaseHouseStage) });
        }
    }

    public sealed class SkillPurchaseProposal
    {
        public int OperationFormat => 2;
        public string RuleRevision => SkillTree.RuleRevision;
        public string PricingRevision => Request.PricingRevision;
        public SkillPurchaseRequest Request { get; }
        public SkillOwnership OwnershipBefore { get; }
        public SkillOwnership OwnershipAfter { get; }
        public long WalletBefore => Request.ExpectedWallet;
        public long WalletAfter { get; }
        public long FrozenPrice { get; }
        public SkillModifiers EffectsBefore { get; }
        public SkillModifiers EffectsAfter { get; }
        internal SkillPurchaseProposal(SkillPurchaseRequest request, SkillOwnership before, SkillOwnership after, long price)
        { Request = request; OwnershipBefore = before; OwnershipAfter = after; FrozenPrice = price;
            WalletAfter = checked(WalletBefore - price); EffectsBefore = SkillTree.Modifiers(before); EffectsAfter = SkillTree.Modifiers(after); }
        public bool MatchesPreconditions(long currentSequence, long wallet, SkillContext context, SkillOwnership ownership) =>
            currentSequence == Request.Sequence - 1 && wallet == WalletBefore && Request.Context.Same(context) && ownership != null && ownership.Token == OwnershipBefore.Token;
    }
    public sealed class SkillPurchaseResult
    {
        public PurchaseStatus Status { get; }
        public SkillPurchaseProposal Proposal { get; }
        internal SkillPurchaseResult(PurchaseStatus status, SkillPurchaseProposal proposal = null) { Status = status; Proposal = proposal; }
    }

    public static class SkillTree
    {
        public const string RuleRevision = "chapter.rules.v1";
        public const string UniversalTier2 = SkillIds.Universal2;
        public const string RecyclingTier1 = SkillIds.Recycling1, RecyclingTier2 = SkillIds.Recycling2;
        public const string DeliveryTier1 = SkillIds.Delivery1, DeliveryTier2 = SkillIds.Delivery2;
        static IReadOnlyList<SkillNode> MakeNodes(string revision) => Array.AsReadOnly(new[] {
            new SkillNode(GameIds.SlotCoinBoost, 0, 1, SkillPricing.Price(GameIds.SlotCoinBoost, revision), null, GameIds.HouseRepair, revision),
            new SkillNode(UniversalTier2, 0, 2, SkillPricing.Price(UniversalTier2, revision), GameIds.SlotCoinBoost, ChapterIds.Project(EraIds.WorkshopYard, FamilyIds.House, 1), revision),
            new SkillNode(RecyclingTier1, 1, 1, SkillPricing.Price(RecyclingTier1, revision), null, GameIds.RecyclerBuild, revision),
            new SkillNode(RecyclingTier2, 1, 2, SkillPricing.Price(RecyclingTier2, revision), RecyclingTier1, ChapterIds.Project(EraIds.WorkshopYard, FamilyIds.Recycler, 1), revision),
            new SkillNode(DeliveryTier1, 2, 1, SkillPricing.Price(DeliveryTier1, revision), null, GameIds.PigeonLoftBuild, revision),
            new SkillNode(DeliveryTier2, 2, 2, SkillPricing.Price(DeliveryTier2, revision), DeliveryTier1, ChapterIds.Project(EraIds.WorkshopYard, FamilyIds.PigeonLoft, 1), revision) });
        public static IReadOnlyList<SkillNode> Nodes { get; } = MakeNodes(SkillPricing.CurrentRevision);
        static readonly IReadOnlyList<SkillNode> legacyNodes = MakeNodes(SkillPricing.LegacyRevision);
        static readonly IReadOnlyList<SkillNode> revision2Nodes = MakeNodes(SkillPricing.RevisionV2);
        public static SkillNode Find(string nodeId, string pricingRevision = SkillPricing.CurrentRevision)
        {
            if (!SkillPricing.IsKnown(pricingRevision)) throw new ArgumentException("Unknown skill pricing revision");
            var nodes = pricingRevision == SkillPricing.CurrentRevision ? Nodes :
                pricingRevision == SkillPricing.RevisionV2 ? revision2Nodes : legacyNodes;
            foreach (var node in nodes) if (node.Id == nodeId) return node;
            return null;
        }
        internal static bool ValidRequest(SkillPurchaseRequest request) => request != null && StableId.IsValid(request.NodeId) &&
            StableId.IsValid(request.OperationId) && request.Sequence > 0 && request.ExpectedWallet >= 0 && request.Context != null &&
            request.ExpectedOwnership != null && request.ExpectedOwnership.Length <= 2048 && SkillPricing.IsKnown(request.PricingRevision);
        internal static bool Unlocked(SkillNode node, SkillContext context, IReadOnlyList<SkillReceipt> receipts)
        {
            bool prior = node.RequiredSkillId == null;
            foreach (var receipt in receipts) if (receipt.Request.NodeId == node.RequiredSkillId) prior = true;
            int project = node.Branch == 0 ? context.HouseStage : node.Branch == 1 ? context.RecyclerLevel : context.PigeonLoftLevel;
            return prior && context.Era >= node.RequiredEra && project >= node.RequiredProjectLevel;
        }
        internal static SkillModifiers Modifiers(SkillOwnership preCommandOwnership)
        {
            if (preCommandOwnership == null) throw new ArgumentNullException(nameof(preCommandOwnership));
            long universal = 0, trash = 0, delivery = 0;
            foreach (var node in Nodes) if (preCommandOwnership.Owns(node.Id))
            { if (node.Branch == 0) universal++; else if (node.Branch == 1) trash++; else delivery++; }
            return new SkillModifiers(universal, trash, delivery);
        }
        public static SkillModifiers EvaluateModifiers(SkillContext preCommandContext, SkillOwnership preCommandOwnership)
        { ValidateOwnership(preCommandContext, preCommandOwnership); return Modifiers(preCommandOwnership); }
        public static SkillQuote Quote(string nodeId, string operationId, long nextSequence, long wallet, SkillContext context, SkillOwnership ownership,
            string pricingRevision = SkillPricing.CurrentRevision)
        {
            Validate(wallet, context, ownership);
            var node = Find(nodeId, pricingRevision) ?? throw new ArgumentException("Unknown skill node");
            bool owned = ownership.Owns(node.Id);
            var status = owned ? OfferStatus.Maxed : !Unlocked(node, context, ownership.Receipts) ? OfferStatus.Locked :
                wallet < node.Price ? OfferStatus.InsufficientFunds : OfferStatus.Available;
            var current = Modifiers(ownership);
            var next = new SkillModifiers(current.UniversalCoins + (!owned && node.Branch == 0 ? 1 : 0),
                current.CoinsPerTrash + (!owned && node.Branch == 1 ? 1 : 0), current.CoinsPerDelivery + (!owned && node.Branch == 2 ? 1 : 0));
            var unlocks = new List<string>();
            var nextUnlocks = new List<SkillUnlock>();
            if (!owned) foreach (var currentCandidate in Nodes) if (currentCandidate.RequiredSkillId == node.Id)
            {
                var candidate = Find(currentCandidate.Id, pricingRevision);
                unlocks.Add(candidate.Id);
                nextUnlocks.Add(new SkillUnlock(candidate, context.Era >= candidate.RequiredEra &&
                    context.Stages.AbsoluteLevel(candidate.RequiredFamilyId) >= candidate.RequiredProjectLevel));
            }
            return new SkillQuote(node, status, owned, current, next, unlocks.AsReadOnly(), nextUnlocks.AsReadOnly(),
                new SkillPurchaseRequest(nodeId, operationId, nextSequence, wallet, ownership.Token, context, pricingRevision));
        }
        static void Validate(long wallet, SkillContext context, SkillOwnership ownership)
        {
            if (wallet < 0 || context == null || ownership == null) throw new ArgumentException("Invalid skill context");
            ValidateOwnership(context, ownership);
        }
        public static void ValidateOwnership(SkillContext context, SkillOwnership ownership)
        {
            if (context == null || ownership == null) throw new ArgumentNullException(nameof(context));
            foreach (var receipt in ownership.Receipts)
                if (!Unlocked(Find(receipt.Request.NodeId), context, ownership.Receipts) ||
                    (receipt.Legacy ? context.HouseStage < receipt.LegacyHouseStage : !context.AtLeast(receipt.Request.Context)))
                    throw new ArgumentException("Owned skill lacks its construction, era or branch prerequisite");
        }
        public static SkillPurchaseResult Propose(long currentSequence, long wallet, SkillContext context, SkillOwnership ownership, SkillPurchaseRequest request)
        {
            Validate(wallet, context, ownership);
            if (!ValidRequest(request) || currentSequence < 0) return new SkillPurchaseResult(PurchaseStatus.InvalidRequest);
            var node = Find(request.NodeId, request.PricingRevision);
            if (node == null) return new SkillPurchaseResult(PurchaseStatus.UnknownOffer);
            foreach (var receipt in ownership.Receipts)
                if (receipt.Request.Sequence > currentSequence) return new SkillPurchaseResult(PurchaseStatus.Conflict);
            foreach (var receipt in ownership.Receipts)
            {
                if (receipt.Request.Sequence == request.Sequence || receipt.Request.OperationId == request.OperationId)
                    return new SkillPurchaseResult(!receipt.Legacy && receipt.Request.Same(request) ? PurchaseStatus.Replay : PurchaseStatus.Conflict);
            }
            if (request.PricingRevision != SkillPricing.CurrentRevision) return new SkillPurchaseResult(PurchaseStatus.Stale);
            if (currentSequence == long.MaxValue) return new SkillPurchaseResult(PurchaseStatus.Overflow);
            if (request.Sequence <= currentSequence) return new SkillPurchaseResult(PurchaseStatus.Stale);
            if (request.Sequence != currentSequence + 1 || request.ExpectedWallet != wallet || request.ExpectedOwnership != ownership.Token || !request.Context.Same(context))
                return new SkillPurchaseResult(PurchaseStatus.Conflict);
            var quote = Quote(request.NodeId, request.OperationId, request.Sequence, wallet, context, ownership);
            if (quote.Status != OfferStatus.Available)
                return new SkillPurchaseResult(quote.Status == OfferStatus.Locked ? PurchaseStatus.Locked : quote.Status == OfferStatus.Maxed ? PurchaseStatus.Maxed : PurchaseStatus.InsufficientFunds);
            var receipts = new List<SkillReceipt>(ownership.Receipts) { new SkillReceipt(request, false, node.Price) };
            var after = new SkillOwnership(receipts);
            return new SkillPurchaseResult(PurchaseStatus.Proposed, new SkillPurchaseProposal(request, ownership, after, node.Price));
        }
        // Recovery validation only: reconstruct a recorded transaction under its pinned price revision.
        // The caller validates its journal format/rule and applies the frozen result through the sole save queue.
        public static SkillPurchaseProposal RestorePurchase(long currentSequence, long wallet, SkillContext context,
            SkillOwnership ownership, SkillPurchaseRequest request, long frozenPrice)
        {
            Validate(wallet, context, ownership);
            if (!ValidRequest(request) || currentSequence < 0 || currentSequence == long.MaxValue ||
                request.Sequence != currentSequence + 1 || request.ExpectedWallet != wallet ||
                request.ExpectedOwnership != ownership.Token || !request.Context.Same(context))
                throw new ArgumentException("Invalid frozen skill preconditions");
            var node = Find(request.NodeId, request.PricingRevision);
            if (node == null || frozenPrice != node.Price || wallet < frozenPrice || ownership.Owns(request.NodeId) ||
                !Unlocked(node, context, ownership.Receipts)) throw new ArgumentException("Invalid frozen skill price or prerequisite");
            foreach (var receipt in ownership.Receipts)
                if (receipt.Request.Sequence >= request.Sequence || receipt.Request.OperationId == request.OperationId)
                    throw new ArgumentException("Frozen skill identity conflicts with history");
            var receipts = new List<SkillReceipt>(ownership.Receipts) { new SkillReceipt(request, false, frozenPrice) };
            return new SkillPurchaseProposal(request, ownership, new SkillOwnership(receipts), frozenPrice);
        }
        public static PurchaseStatus ReplayLegacy(long currentSequence, long currentWallet, int currentHouseStage, SkillOwnership ownership, UpgradePurchaseRequest request)
        {
            if (ownership == null) throw new ArgumentNullException(nameof(ownership));
            foreach (var item in ownership.Receipts) if (item.Request.Sequence > currentSequence) return PurchaseStatus.Conflict;
            foreach (var receipt in ownership.Receipts) if (receipt.Legacy)
            {
                var original = receipt.Request;
                var legacy = new UpgradeOwnership(1, 1, 1, original.Sequence, original.ExpectedWallet, receipt.LegacyHouseStage);
                var result = CoinBoostShop.Propose(currentSequence, currentWallet, currentHouseStage, legacy, request);
                return result.Status == PurchaseStatus.Replay ? PurchaseStatus.Replay : PurchaseStatus.Conflict;
            }
            return PurchaseStatus.Conflict;
        }
    }
}
