using System;
using System.Collections.Generic;
using JunkToNeon.Contracts;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Tests.Upgrades
{
    public static class UpgradeRegression
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Fails<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        static UpgradePurchaseRequest Request(long sequence, long wallet, int house = 1, long revision = 0, string offer = GameIds.SlotCoinBoost)
        { return new UpgradePurchaseRequest(offer, sequence, revision, wallet, house); }
        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>();
            var empty = UpgradeOwnership.Empty;
            string freshBytes = UpgradeOwnershipCodec.Encode(empty);
            var offer = CoinBoostShop.Offer;
            Require(offer.OfferId == "slot.coin-boost" && offer.UpgradeId == "slot.coin-boost" && offer.CurrencyId == "coins" &&
                offer.Price == 120 && offer.AdditiveCoinsPerSpin == 1 && offer.LevelCap == 1 && offer.RequiredHouseStage == 1 &&
                offer.PrerequisiteId == "house.repair" && offer.Provisional && !offer.LiveEconomyEnabled, "Offer contract changed");
            Require(CoinBoostShop.Preview(500, 0, empty).Status == OfferStatus.Locked &&
                CoinBoostShop.Preview(119, 1, empty).Status == OfferStatus.InsufficientFunds &&
                CoinBoostShop.Preview(157, 1, empty).Status == OfferStatus.Available &&
                !CoinBoostShop.Preview(157, 1, empty).Owned && UpgradeOwnershipCodec.Encode(empty) == freshBytes,
                "Preview spent coins or advanced ownership");
            passed.Add("Stable provisional offer, locked/available/insufficient preview and no ownership mutation");

            var request = Request(11, 157);
            var purchase = CoinBoostShop.Propose(10, 157, 1, empty, request);
            Require(purchase.Status == PurchaseStatus.Proposed && purchase.Proposal != null, "Explicit request did not propose");
            var proposal = purchase.Proposal; var owned = proposal.OwnershipAfter;
            Require(proposal.WalletBefore == 157 && proposal.WalletAfter == 37 && proposal.FrozenPrice == 120 &&
                proposal.LevelBefore == 0 && proposal.LevelAfter == 1 && proposal.ExpectedGlobalSequence == 10 &&
                owned.PurchaseSequence == 11 && owned.Revision == 1 && UpgradeOwnershipCodec.Encode(empty) == freshBytes,
                "Expected independent 157-120=37 proposal, without live activation");
            Require(proposal.MatchesPreconditions(10, 157, 1, empty) && !proposal.MatchesPreconditions(11, 157, 1, empty) &&
                !proposal.MatchesPreconditions(10, 158, 1, empty) && !proposal.MatchesPreconditions(10, 157, 2, empty) &&
                !proposal.MatchesPreconditions(10, 157, 1, owned), "Proposal does not bind its preconditions");
            passed.Add("Explicit purchase freezes price/level and wallet157→37, binding every mutable precondition");

            var replay = CoinBoostShop.Propose(30, 999, 2, owned, request);
            Require(replay.Status == PurchaseStatus.Replay && replay.Proposal == null, "Replay returned a stale replacement wallet");
            Require(CoinBoostShop.Propose(30, 999, 2, owned, Request(11, 158)).Status == PurchaseStatus.Conflict &&
                CoinBoostShop.Propose(30, 999, 2, owned, Request(11, 157, 2)).Status == PurchaseStatus.Conflict &&
                CoinBoostShop.Propose(30, 999, 2, owned, Request(11, 157, 1, 1)).Status == PurchaseStatus.Conflict &&
                CoinBoostShop.Propose(30, 999, 2, owned, Request(11, 157, 1, 0, "unknown.offer")).Status == PurchaseStatus.UnknownOffer,
                "Changed original request was treated as replay");
            passed.Add("Applied exact replay is a no-op without proposal; changed request/offer cannot reuse it");

            Require(CoinBoostShop.Preview(1000, 2, owned).Owned && CoinBoostShop.Preview(1000, 2, owned).Maxed &&
                CoinBoostShop.Preview(1000, 2, owned).Status == OfferStatus.Maxed, "Owned/maxed state missing");
            Require(CoinBoostShop.Propose(12, 1000, 2, owned, Request(13, 1000, 2, 1)).Status == PurchaseStatus.Maxed &&
                CoinBoostShop.Propose(30, 157, 1, empty, request).Status == PurchaseStatus.Stale &&
                CoinBoostShop.Propose(10, 157, 1, empty, Request(12, 157)).Status == PurchaseStatus.Conflict &&
                CoinBoostShop.Propose(10, 158, 1, empty, request).Status == PurchaseStatus.Conflict &&
                CoinBoostShop.Propose(0, 37, 1, owned, request).Status == PurchaseStatus.Conflict,
                "Cap, stale quote or mismatched aggregate sequence accepted");
            Require(CoinBoostShop.Propose(-1, 157, 1, empty, request).Status == PurchaseStatus.InvalidRequest &&
                CoinBoostShop.Propose(0, 157, 1, empty, Request(0, 157)).Status == PurchaseStatus.InvalidRequest,
                "Invalid sequence accepted");
            passed.Add("Cap1, stale/gapped sequence, stale wallet/revision and mismatched aggregate fail closed");

            Require(CoinBoostShop.Propose(0, 500, 0, empty, Request(1, 500, 0)).Status == PurchaseStatus.Locked &&
                CoinBoostShop.Propose(0, 119, 1, empty, Request(1, 119)).Status == PurchaseStatus.InsufficientFunds,
                "Locked/insufficient explicit request spent coins");
            Require(CoinBoostShop.Propose(0, 120, 1, empty, Request(1, 120)).Proposal.WalletAfter == 0 &&
                CoinBoostShop.Propose(100, long.MaxValue, 2, empty, Request(101, long.MaxValue, 2)).Proposal.WalletAfter == long.MaxValue - 120,
                "Exact/max wallet arithmetic failed");
            Require(CoinBoostShop.Propose(long.MaxValue, 120, 1, empty, Request(long.MaxValue, 120)).Status == PurchaseStatus.Overflow,
                "Exhausted global sequence failed to report overflow");
            Fails<ArgumentOutOfRangeException>(() => CoinBoostShop.Preview(-1, 1, empty));
            Fails<ArgumentNullException>(() => CoinBoostShop.Preview(157, 1, null));
            passed.Add("Unlock, insufficient/exact funds, maximum wallet and exhausted sequence boundaries");

            string ownedBytes = UpgradeOwnershipCodec.Encode(owned);
            for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) for (int c = 0; c < 4; c++)
            {
                var outcome = new SpinOutcome(a, b, c);
                var before = CoinBoostShop.Evaluate(outcome, empty); var after = CoinBoostShop.Evaluate(outcome, owned);
                Require(before.EffectId == "slot.coin-boost" && before.Coins == 0 && after.Coins == 1,
                    "Uniform additive effect changed with symbols or activated on acquisition spin");
            }
            var contribution = CoinBoostShop.Evaluate(new SpinOutcome(0, 0, 2), owned);
            Require(CoinEffects.Total(new[] { contribution, new EffectContribution("workshop.spin-coins", 1) }) == 2 &&
                UpgradeOwnershipCodec.Encode(owned) == ownedBytes, "Effect mixed workshop/base or mutated ownership");
            Fails<OverflowException>(() => CoinEffects.Total(new[] { contribution, new EffectContribution("test.limit", long.MaxValue) }));
            Fails<ArgumentException>(() => CoinEffects.Total(new[] { contribution, contribution }));
            passed.Add("All64 frozen outcomes give uniform0/1 pre-state contribution; workshop stays separate; fold overflow rejects");

            Require(UpgradeOwnershipCodec.DecodeOrDefault(null).Level == 0 && UpgradeOwnershipCodec.Encode(UpgradeOwnershipCodec.DecodeOrDefault(freshBytes)) == freshBytes &&
                UpgradeOwnershipCodec.Encode(UpgradeOwnershipCodec.DecodeOrDefault(ownedBytes)) == ownedBytes, "Legacy/roundtrip state changed");
            var reordered = UpgradeOwnershipCodec.DecodeOrDefault("{\"houseStage\":1,\"walletBefore\":157,\"sequence\":11,\"revision\":1,\"level\":1,\"version\":1}");
            Require(reordered.PurchaseWalletBefore == 157 && reordered.PurchaseSequence == 11, "JSON property order affected identity");
            foreach (string bad in new[] { "", "{}", freshBytes + "x", freshBytes.Replace("\"version\":1", "\"version\":2"),
                freshBytes.Replace("\"level\":0", "\"level\":2"), freshBytes.Replace("\"revision\":0", "\"revision\":1"),
                freshBytes.Replace("\"sequence\":0", "\"sequence\":-1"), ownedBytes.Replace("157", "119"),
                ownedBytes.Replace("\"houseStage\":1", "\"houseStage\":0"),
                freshBytes.Replace("\"version\":1", "\"version\":1,\"version\":1"), freshBytes.Replace("\"level\":0", "\"unknown\":0"),
                freshBytes.Replace("\"level\":0", "\"level\":1.0"), freshBytes.Replace("\"level\":0", "\"level\":1e0"),
                freshBytes.Replace("\"sequence\":0", "\"sequence\":9223372036854775808"), freshBytes.Replace(":", "\u00a0:"), new string(' ', 513) })
                Fails<FormatException>(() => UpgradeOwnershipCodec.DecodeOrDefault(bad));
            passed.Add("Bounded JSON roundtrip/property order, absent legacy defaults, malformed/unknown/duplicate/overflow rejection");

            var restored = UpgradeOwnershipCodec.DecodeOrDefault(ownedBytes);
            Require(CoinBoostShop.Propose(50, 37, 2, restored, request).Status == PurchaseStatus.Replay &&
                CoinBoostShop.Propose(50, 37, 2, restored, request).Proposal == null &&
                CoinBoostShop.Propose(100, 157, 1, empty, Request(101, 157)).Status == PurchaseStatus.Proposed,
                "Save roundtrip lost replay or assumed upgrade-only consecutive sequences");
            Require(CoinBoostShop.Evaluate(new SpinOutcome(3, 3, 3), UpgradeOwnershipCodec.DecodeOrDefault(null)).Coins == 0,
                "Legacy frozen state invented an upgrade effect");
            passed.Add("Roundtripped receipt preserves replay; global commands may interleave; missing old ownership adds no effect");
            return passed;
        }
    }
}
