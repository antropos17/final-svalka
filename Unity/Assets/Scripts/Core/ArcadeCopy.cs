using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JunkToNeon.Contracts;

namespace JunkToNeon.Core
{
    // Stateless captions only. A known identity is not proof of ownership, completion
    // or payment. Callers display actual state, effects, prerequisites and costs.
    public static class ArcadeCopy
    {
        public static string Label(string id)
        {
            switch (id)
            {
                case "arcade.slot.1": return "Slot Level I";
                case "arcade.slot.2": return "Slot Level II";
                case "arcade.slot.3": return "Slot Level III";
                case "arcade.pack.parts": return "Parts for Diamonds";
                case "arcade.pack.potatoes": return "Potatoes for Diamonds";
                case "arcade.boost.next-bonus": return "Diamond Booster";
                case "arcade.memory": return "Memory";
                case "arcade.sorting": return "Sorting";
                case "resource.coins": return "Coins";
                case "resource.parts": return "Parts";
                case "resource.potatoes": return "Potatoes";
                case "resource.energy": return "Energy";
                case "resource.tickets": return "Tickets";
                case "resource.diamonds": return "Diamonds";
                case "section.arcade": return "Mini-games";
                case "section.slot": return "Slot Levels";
                case "attribute.box-parts": return "Boxes: Parts";
                case "attribute.potato-potatoes": return "Potatoes: Bonus";
                case "attribute.bonus-resources": return "Bonus: Parts and Potatoes";
                case "caption.current": return "Current";
                case "caption.next": return "Next Level";
                case "caption.maximum": return "Max Level";
                case "caption.cost": return "Cost";
                case "caption.requires": return "Requires";
                case "caption.shortfall": return "Shortfall";
                case "reward.coin-income": return "Coin Income";
                case "reward.construction-debit": return "Construction Cost";
                case "reward.credited": return "Credited";
                case "reward.available": return "To Claim";
                case "reward.overflow": return "Over Capacity";
                case "reward.included": return "Already Included in Total";
                case "action.buy": return "Buy";
                case "action.start": return "Start for a Ticket";
                case "action.resume": return "Resume Round";
                case "action.pick": return "Reveal Card";
                case "action.sort": return "Choose Bin";
                case "action.continue": return "Continue";
                case "action.claim": return "Claim Reward";
                case "action.claim-zero": return "Finish Without Reward";
                case "action.abandon": return "Abandon Without Reward";
                case "status.paused": return "Paused";
                case "status.resumed": return "Round Resumed";
                case "status.ready": return "Result Ready";
                case "status.claimed": return "Claimed";
                case "status.failed": case "status.no-reward": return "No Diamonds This Round";
                case "status.refused": return "Action Unavailable";
                case "status.uncertain": return "Save Needs Checking";
                case "house.repair": return "House · Repair";
                case "house.improve": return "House · Expand";
                case "workshop.build": return "Workshop · Build";
                case "workshop.improve": return "Workshop · Upgrade";
                case "recycler.build": return "Recycler · Build";
                case "recycler.improve": return "Recycler · Upgrade";
                case "pigeon-loft.build": return "Pigeon Loft · Build";
                case "pigeon-loft.improve": return "Pigeon Loft · Upgrade";
                case "cart.build": return "Cart · Build";
                case "cart.improve": return "Cart · Upgrade";
                case "power-source.build": return "Power Source · Build";
                case "power-source.improve": return "Power Source · Upgrade";
                case "house.workshop-yard.upgrade-1": return "House · Yard I";
                case "house.workshop-yard.upgrade-2": return "House · Yard II";
                case "workshop.workshop-yard.upgrade-1": return "Workshop · Yard I";
                case "workshop.workshop-yard.upgrade-2": return "Workshop · Yard II";
                case "recycler.workshop-yard.upgrade-1": return "Recycler · Yard I";
                case "recycler.workshop-yard.upgrade-2": return "Recycler · Yard II";
                case "pigeon-loft.workshop-yard.upgrade-1": return "Pigeon Loft · Yard I";
                case "pigeon-loft.workshop-yard.upgrade-2": return "Pigeon Loft · Yard II";
                case "cart.workshop-yard.upgrade-1": return "Cart · Yard I";
                case "cart.workshop-yard.upgrade-2": return "Cart · Yard II";
                case "power-source.workshop-yard.upgrade-1": return "Power Source · Yard I";
                case "power-source.workshop-yard.upgrade-2": return "Power Source · Yard II";
                default: return null;
            }
        }

        public static string Description(string id)
        {
            switch (id)
            {
                case "arcade.slot.1": return "Adds Parts if a Box appears on the main reels. Once per spin, not per Box. Storage limits the credit.";
                case "arcade.slot.2": return "Keeps the previous effect and adds Potatoes if their symbol appears on the main reels. Once per spin; storage limits the credit.";
                case "arcade.slot.3": return "Keeps the previous effects and adds Parts and Potatoes to the raw bonus before focus and the booster. Requires the bonus reel; storage caps apply.";
                case "arcade.pack.parts": return "Exchanges Diamonds for the shown amount of Parts. The whole purchase must fit in storage or it is unavailable.";
                case "arcade.pack.potatoes": return "Exchanges Diamonds for the shown amount of Potatoes. The whole purchase must fit in storage or it is unavailable.";
                case "arcade.boost.next-bonus": return "Boosts the next resource bonus using Diamonds, saving Energy. You cannot buy another while a booster is armed.";
                case "arcade.memory": return "Reveal different closed cards and find pairs. Use Continue to close a mismatch. No timer; rewards depend on attempts.";
                case "arcade.sorting": return "Choose the bin named for the current symbol. A wrong choice ends the turn without a point. No timer; rewards depend on correct answers.";
                case "resource.tickets": return "Tickets start mini-games and are earned from new spins. Storage is capped; no Tickets arrive while away.";
                case "resource.diamonds": return "Diamonds depend on the mini-game result and are credited after you claim the reward. Use them for slot levels and the shop.";
                case "section.arcade": return "Choose a mini-game or resume your current round. Main spins remain free; mini-games are optional for the chapter.";
                case "section.slot": return "Slot levels grant fixed resource effects. They are optional and separate from main construction; reel odds and payouts stay the same.";
                case "attribute.box-parts": return "Parts from a Box are added once per spin, not per Box.";
                case "attribute.potato-potatoes": return "Potatoes from their symbol are added once per spin, not per symbol.";
                case "attribute.bonus-resources": return "The resource addition enters the raw bonus before focus, the single booster and storage caps.";
                case "reward.coin-income": return "Spin income before paying for the selected construction. Payment is shown separately.";
                case "reward.construction-debit": return "The selected construction debit is shown separately from spin income.";
                case "reward.credited": return "The amount actually credited. Included components must not be added again.";
                case "reward.available": return "The result is ready, but the reward is unclaimed. Claim it with a separate confirmation.";
                case "reward.overflow": return "This portion of the resource reward exceeded storage and was not credited.";
                case "reward.included": return "This is part of the actual total credited, not an extra reward.";
                case "action.start": return "Starting spends the shown Ticket and saves the round. Viewing the rules starts nothing.";
                case "action.resume": case "status.paused": return "Resumes the same round with its previous progress. Pausing and returning do not restart it.";
                case "action.continue": return "Closes the unmatched cards. The attempt is already counted; choose the next pair yourself.";
                case "action.claim": case "status.ready": return Description("reward.available");
                case "action.claim-zero": case "status.failed": case "status.no-reward": return "No Diamonds were earned. Confirm completion to close the result. Reopening the round will not change it.";
                case "action.abandon": return "Abandons this round without a Ticket refund or reward. Closing the window normally saves the round.";
                case "status.resumed": return "The saved round resumes. Returning costs no additional Ticket.";
                case "status.claimed": return "This result was already claimed in the current save. It cannot grant another reward.";
                case "status.refused": return "The action requirements are not met. Check the options available in the current state.";
                case "status.uncertain": return "The save outcome needs checking. Open recovery to inspect the saved result.";
                default: return null;
            }
        }

        public static string SymbolLabel(int symbol)
        {
            switch (symbol)
            {
                case SymbolIds.Potato: return "Potato";
                case SymbolIds.Box: return "Box";
                case SymbolIds.Trash: return "Trash";
                case SymbolIds.Pigeon: return "Pigeon";
                default: return null;
            }
        }

        public static string CurrentAttributes(ArcadeView view)
        {
            if (view == null) return null;
            return "From Slot Level " + N(view.SlotLevel) + ":\n" +
                "Parts per spin with a Box: +" + N(view.BoxPresencePartBonus) + ".\n" +
                "Potatoes per spin with a Potato: +" + N(view.PotatoPresencePotatoBonus) + ".\n" +
                "Raw bonus: +" + N(view.RawBonusPartsAddition) + " Parts, +" + N(view.RawBonusPotatoesAddition) + " Potatoes.";
        }

        // Legacy next-offer prose remains stable. A selected offer, including an
        // already-owned lower level, uses OfferAttributes instead of current + 1.
        public static string NextAttributes(ArcadeView view)
        {
            if (view == null) return null;
            foreach (var offer in ArcadeCatalog.Offers)
                if (offer.Kind == "slot" && offer.Level == view.SlotLevel + 1)
                {
                    string title = Label(offer.Id), body = Description(offer.Id);
                    return title == null || body == null ? null : Label("caption.next") + ": " + title + ".\n" + body;
                }
            return Label("caption.maximum") + ".";
        }

        public static string OfferAttributes(ArcadeOffer offer)
        {
            if (offer == null || offer.Kind != "slot") return null;
            ArcadeSlotAttributes target;
            try { target = ArcadeCatalog.SlotAttributes(offer.Level); }
            catch (ArgumentOutOfRangeException) { return null; }
            return "Viewed level: " + N(target.Level) + ".\n" +
                "Per spin with a Box: Parts +" + N(target.BoxPresencePartBonus) +
                "; with a Potato: Potatoes +" + N(target.PotatoPresencePotatoBonus) + ".\n" +
                "Raw bonus: Parts +" + N(target.RawBonusPartsAddition) +
                ", Potatoes +" + N(target.RawBonusPotatoesAddition) + ".\n" +
                "On presence; bonus before focus and booster, then storage caps.";
        }

        public static string TicketSummary(ArcadeView view)
        {
            if (view == null) return null;
            string balance = "Tickets: " + N(view.Tickets) + "/" + N(view.TicketCap) + ". ";
            return balance + (view.Tickets >= view.TicketCap ? "No new Tickets can be added now." :
                "New spins until a Ticket: " + N(view.TicketCadence - view.TicketProgress) + ".");
        }

        public static string QuoteCosts(ArcadeQuote quote)
        {
            if (quote == null) return null;
            var text = new StringBuilder();
            if (quote.DiamondsCost > 0) Line(text, Label("resource.diamonds"), quote.DiamondsCost, "−");
            if (quote.PartsCost > 0) Line(text, Label("resource.parts"), quote.PartsCost, "−");
            if (quote.PotatoesCost > 0) Line(text, Label("resource.potatoes"), quote.PotatoesCost, "−");
            if (quote.TicketsCost > 0) Line(text, Label("resource.tickets"), quote.TicketsCost, "−");
            return text.Length == 0 ? "No cost." : text.ToString();
        }

        public static string ClaimLabel(ArcadeRunView run)
        { return run == null || !run.Ready ? null : Label(run.ReadyReward == 0 ? "action.claim-zero" : "action.claim"); }

        public static string ReadyReward(ArcadeRunView run)
        { return run == null || !run.Ready ? null : Label("reward.available") + ": " + Label("resource.diamonds") + " " + N(run.ReadyReward) + "."; }

        // Derive reward bands from the public scoring rules, not a second tuning table.
        public static string GameRules(string id)
        {
            if (id == ArcadeCatalog.Memory)
                return "Attempt limit: " + N(EconomyRules.ArcadeMemoryAttempts) + ".\nDiamonds for all pairs: " +
                    Bands(EconomyRules.ArcadeMemoryAttempts, n => EconomyRules.ArcadeMemoryReward(n, true), true) +
                    ".\nNot all pairs: " + N(EconomyRules.ArcadeMemoryReward(EconomyRules.ArcadeMemoryAttempts, false)) + ".\n" + ZeroClaimRule;
            if (id == ArcadeCatalog.Sorting)
                return "Items: " + N(EconomyRules.ArcadeSortingItems) + ".\nCorrect answers → Diamonds: " +
                    Bands(EconomyRules.ArcadeSortingItems, EconomyRules.ArcadeSortingReward, false) + ".\n" + ZeroClaimRule;
            return null;
        }

        static string ZeroClaimRule => "Claim the reward separately, even when it is zero.";
        static string N(long value) { return value.ToString(CultureInfo.InvariantCulture); }
        static string Bands(int maximum, Func<int, int> score, bool ceilingFirst)
        {
            var bands = new List<string>();
            int start = 0, reward = score(0);
            for (int next = 1; next <= maximum + 1; next++)
            {
                int nextReward = next <= maximum ? score(next) : -1;
                if (nextReward == reward) continue;
                int end = next - 1;
                string range = start == 0 && ceilingFirst ? "up to " + N(end) :
                    start == end ? N(start) : N(start) + "–" + N(end);
                bands.Add(range + " — " + N(reward));
                start = next; reward = nextReward;
            }
            return string.Join("; ", bands);
        }

        // A retained event is not a new reward. The Runtime owner must establish a new
        // matching commit and ordinary reveal before using its values for feedback.
        public static string EventSummary(ArcadeEvent fact)
        {
            if (fact == null) return null;
            var text = new StringBuilder();
            if (fact.CoinIncome > 0) Line(text, Label("reward.coin-income"), fact.CoinIncome, "+");
            if (fact.ConstructionDebit > 0) Line(text, Label("reward.construction-debit"), fact.ConstructionDebit, "−");
            if (fact.PartsAdded > 0) Line(text, Label("resource.parts"), fact.PartsAdded, "+");
            if (fact.PotatoesAdded > 0) Line(text, Label("resource.potatoes"), fact.PotatoesAdded, "+");
            if (fact.EnergyAdded > 0) Line(text, Label("resource.energy"), fact.EnergyAdded, "+");
            if (fact.TicketsAdded > 0) Line(text, Label("resource.tickets"), fact.TicketsAdded, "+");
            if (fact.DiamondsAdded > 0 || fact.Kind == "claim") Line(text, Label("resource.diamonds"), fact.DiamondsAdded, fact.DiamondsAdded == 0 ? "" : "+");
            if (fact.SlotPartsIncluded > 0 || fact.SlotPotatoesIncluded > 0)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append("Slot level already included in total: Parts ").Append(N(fact.SlotPartsIncluded))
                    .Append(", Potatoes ").Append(N(fact.SlotPotatoesIncluded)).Append('.');
            }
            return text.Length == 0 ? "Nothing credited." : text.ToString();
        }

        static void Line(StringBuilder text, string label, long amount, string sign)
        {
            if (text.Length > 0) text.Append('\n');
            text.Append(label).Append(": ").Append(sign).Append(N(amount)).Append('.');
        }
    }
}
