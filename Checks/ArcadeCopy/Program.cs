using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JunkToNeon.Core;
using JunkToNeon.Content;

static class Program
{
    static int checks;
    static void Need(bool value, string why)
    { checks++; if (!value) throw new Exception(why); }
    static void Bounded(string id)
    {
        string title = ArcadeCopy.Label(id), body = ArcadeCopy.Description(id);
        Need(!string.IsNullOrWhiteSpace(title) && title.Length <= 40, "Missing/long label: " + id);
        Need(!string.IsNullOrWhiteSpace(body) && body.Length <= 160, "Missing/long description: " + id);
        Need(!body.Any(char.IsDigit), "Static copy duplicates numerical tuning: " + id);
    }

    static void Identities()
    {
        string[] offers = { "arcade.slot.1", "arcade.slot.2", "arcade.slot.3", "arcade.pack.parts", "arcade.pack.potatoes", "arcade.boost.next-bonus" };
        Need(ArcadeCatalog.Offers.Count == 6 && new HashSet<string>(offers).SetEquals(ArcadeCatalog.Offers.Select(o => o.Id)), "Actual six-offer contract changed");
        foreach (string id in offers) Bounded(id);
        foreach (string id in new[] { "arcade.memory", "arcade.sorting", "action.claim-zero", "status.ready", "status.paused", "status.resumed", "status.failed", "status.claimed", "status.uncertain", "reward.credited", "reward.included", "reward.overflow" }) Bounded(id);
        var projects = ChapterData.TwoEras().Eras.SelectMany(e => e.Catalog.Upgrades).ToArray();
        Need(projects.Length == 24 && projects.Select(p => ArcadeCopy.Label(p.Id)).Distinct().Count() == 24, "Existing chapter IDs lost distinct captions");
        foreach (var project in projects)
            Need(!string.IsNullOrWhiteSpace(ArcadeCopy.Label(project.Id)) && ArcadeCopy.Label(project.Id).Length <= 40, "Missing existing stage identity");
        foreach (string id in new[] { null, "", " ", "arcade.slot.4", "ARCADE.MEMORY", "arcade.attribute.parts", "arcade.boost.resources", "house.workshop-yard.upgrade-3", new string('x', 4096) })
            Need(ArcadeCopy.Label(id) == null && ArcadeCopy.Description(id) == null && ArcadeCopy.GameRules(id) == null, "Unknown/superseded identity invented copy");
        Need(Enumerable.Range(0, 4).Select(ArcadeCopy.SymbolLabel).SequenceEqual(new[] { "Potato", "Box", "Trash", "Pigeon" }), "Four named symbol bins changed");
        Need(ArcadeCopy.SymbolLabel(-1) == null && ArcadeCopy.SymbolLabel(4) == null, "Hidden/unknown symbol exposed a face");
        Need(typeof(ArcadeCopy).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Length == 0, "Copy retains mutable state");
        Console.WriteLine("PASS actual six offers,24 original identities, four bins, bounded copy and strict fallbacks");
    }

    static ArcadeView[] levelViews;
    static ArcadeView partialTickets, fullTickets;
    static void PrepareViews()
    {
        if (levelViews != null) return;
        levelViews = new ArcadeView[4];
        var state = ModelState.Fresh(1); levelViews[0] = ArcadeEconomy.Project(state);
        // One bounded private, unselected31-spin preparation produces real retained
        // history. Only the separate level affordability wallet is synthetic.
        for (int spin = 1; spin <= 31; spin++)
        {
            state = GameLogic.ApplyPending(GameLogic.Reserve(state, "spin", 0, state.sequence + 1));
            if (spin == 1)
            {
                var funded = state.Copy(); var expansion = new ExpansionDraft(funded.expansion);
                expansion.ArcadeData = new ArcadeDraft(expansion.ArcadeData) { Diamonds = 13 }.Freeze();
                expansion.Parts = 200; expansion.Potatoes = 200; funded.expansion = expansion.Freeze();
                GameLogic.Validate(funded);
                funded = GameLogic.ApplyPending(ExpansionEconomy.Reserve(funded, ExpansionEconomy.Quote(funded, "buy", "bonus.unlock")));
                string[] ids = { ArcadeCatalog.Slot1, ArcadeCatalog.Slot2, ArcadeCatalog.Slot3 };
                for (int level = 1; level <= 3; level++)
                {
                    funded = GameLogic.ApplyPending(ArcadeEconomy.Reserve(funded, ArcadeEconomy.Quote(funded, "buy", ids[level - 1])));
                    levelViews[level] = ArcadeEconomy.Project(funded);
                }
            }
            if (spin == 15) partialTickets = ArcadeEconomy.Project(state);
            if (spin == 31) fullTickets = ArcadeEconomy.Project(state);
        }
    }
    static ArcadeView View(int level, int tickets = 0, int progress = 0)
    {
        PrepareViews();
        if (tickets == 1 && progress == 7) return partialTickets;
        if (tickets == 3 && progress == 7) return fullTickets;
        return levelViews[level];
    }
    static void AttributesTicketsCosts()
    {
        int[,] expected = { { 0, 0, 0, 0 }, { 1, 0, 0, 0 }, { 1, 1, 0, 0 }, { 1, 1, 1, 1 } };
        for (int level = 0; level <= 3; level++)
        {
            string text = ArcadeCopy.CurrentAttributes(View(level));
            Need(text.Contains("Parts per spin with a Box: +" + expected[level, 0]), "Current box attribute not taken from actual view");
            Need(text.Contains("Potatoes per spin with a Potato: +" + expected[level, 1]), "Current potato attribute not taken from actual view");
            Need(text.Contains("Raw bonus: +" + expected[level, 2] + " Parts, +" + expected[level, 3] + " Potatoes"), "Raw bonus current attributes incorrect");
        }
        Need(ArcadeCopy.NextAttributes(View(0)).Contains("Slot Level I.") && ArcadeCopy.NextAttributes(View(0)).Contains("not per Box"), "Next box effect disconnected from actual next offer");
        Need(ArcadeCopy.NextAttributes(View(1)).Contains("Slot Level II.") && ArcadeCopy.NextAttributes(View(1)).Contains("adds Potatoes"), "Next potato effect disconnected from actual next offer");
        Need(ArcadeCopy.NextAttributes(View(2)).Contains("Slot Level III.") && ArcadeCopy.NextAttributes(View(2)).Contains("before focus and the booster"), "Next raw bonus order wrong");
        Need(ArcadeCopy.NextAttributes(View(3)) == "Max Level.", "Maximum slot promises an absent next level");
        Need(ArcadeCopy.TicketSummary(View(0)).Contains("until a Ticket: 8"), "Fresh future ticket counter wrong");
        Need(ArcadeCopy.TicketSummary(View(0, 1, 7)).Contains("until a Ticket: 1"), "Partial ticket progress was reset by display");
        Need(ArcadeCopy.TicketSummary(View(0, 3, 7)) == "Tickets: 3/3. No new Tickets can be added now.", "Full-cap display promises an uncreditable ticket");
        var quote = new ArcadeQuote(0, "buy", "arcade.slot.1", 0, 0, "none", -1, 20, 0, 2, 0, 0, 0, 0, new string('a', 64));
        Need(ArcadeCopy.QuoteCosts(quote) == "Diamonds: −2.\nParts: −20.", "Captured quote costs mislabeled as earned/current catalog data");
        Need(ArcadeCopy.CurrentAttributes(null) == null && ArcadeCopy.NextAttributes(null) == null && ArcadeCopy.TicketSummary(null) == null && ArcadeCopy.QuoteCosts(null) == null, "Null typed projection fabricated facts");
        Console.WriteLine("PASS typed current attributes, partial/full tickets and captured cost projection");
    }

    static ArcadeRunView SortingView(bool ready, bool perfect)
    {
        int[] board = ArcadeCatalog.Shuffle(ArcadeCatalog.Sorting, 1, out uint after);
        string token = string.Concat(board);
        string transcript = ready ? string.Concat(board.Select(x => (char)('0' + (perfect ? x : (x + 1) % 4)))) : "";
        return new ArcadeRunView(new ArcadeRun(ArcadeCatalog.Sorting, 1, 1, after, token, transcript));
    }
    static void RulesAndReady()
    {
        string memory = ArcadeCopy.GameRules("arcade.memory"), sorting = ArcadeCopy.GameRules("arcade.sorting");
        Need(memory.Contains("Attempt limit: 9") && memory.Contains("up to 4 — 3; 5–6 — 2; 7–9 — 1") && memory.Contains("Not all pairs: 0"), "Displayed Memory bands differ from independent selected profile");
        Need(sorting.Contains("Items: 8") && sorting.Contains("0–2 — 0; 3–4 — 1; 5–6 — 2; 7–8 — 3"), "Displayed Sorting bands differ from independent selected profile");
        Need(memory.Contains("even when it is zero") && sorting.Contains("even when it is zero"), "Zero terminal claim omitted from rules");
        var open = SortingView(false, false); var zero = SortingView(true, false); var full = SortingView(true, true);
        Need(ArcadeCopy.ClaimLabel(open) == null && ArcadeCopy.ReadyReward(open) == null, "Unpaid active run exposes a claim/result early");
        Need(ArcadeCopy.ClaimLabel(zero) == "Finish Without Reward" && ArcadeCopy.ReadyReward(zero) == "To Claim: Diamonds 0.", "Ready0 claim lost or announced paid");
        Need(ArcadeCopy.ClaimLabel(full) == "Claim Reward" && ArcadeCopy.ReadyReward(full) == "To Claim: Diamonds 3.", "Ready positive reward announced paid");
        Need(ArcadeCopy.ClaimLabel(null) == null && ArcadeCopy.ReadyReward(null) == null, "Absent run invented a reward");
        Console.WriteLine("PASS derived reward bands and masked active/Ready0/Ready3 display distinctions");
    }

    static void CommittedEventProjection()
    {
        var spin = new ArcadeEvent(1, "spin", 0, 12, 3, 1, 1, 0, 8, 40, 2, 1, 37, 5);
        string text = ArcadeCopy.EventSummary(spin);
        Need(text.Contains("Coin Income: +8.") && text.Contains("Construction Cost: −40.") && !text.Contains("−32"), "Net wallet delta mislabeled as coin income");
        Need(text.Contains("Parts: +12.") && !text.Contains("Parts: +14.") && text.Contains("Slot level already included in total: Parts 2, Potatoes 1."), "Included slot contribution added twice");
        Need(text.Contains("Tickets: +1.") && !text.Contains("Diamonds:"), "Spin invented a Diamond reward");
        var zero = new ArcadeEvent(2, "claim", 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 5);
        Need(ArcadeCopy.EventSummary(zero) == "Diamonds: 0.", "Zero claim fabricated a positive gain");
        var huge = new ArcadeEvent(3, "spin", 0, 0, 0, 0, 0, 0, long.MaxValue, 0, 0, 0, 0, long.MaxValue);
        Need(ArcadeCopy.EventSummary(huge) == "Coin Income: +9223372036854775807.", "Long CoinIncome truncated or overflowed in text");
        Need(ArcadeCopy.EventSummary(null) == null, "Null event invented credited facts");
        Console.WriteLine("PASS exact actual totals, income/debit separation, included components, zero and long boundary");
    }

    static void SelectedOfferAttributes()
    {
        // Literal cumulative outcomes from the accepted profile, independently
        // expected; deliberately inspect III, then owned-lower I, then II.
        string[] ids = { "arcade.slot.3", "arcade.slot.1", "arcade.slot.2" };
        int[,] expected = { { 3, 1, 1, 1, 1 }, { 1, 1, 0, 0, 0 }, { 2, 1, 1, 0, 0 } };
        for (int i = 0; i < ids.Length; i++)
        {
            string text = ArcadeCopy.OfferAttributes(ArcadeCatalog.Get(ids[i]));
            Need(text != null && text.Length <= 256 && text.Split('\n').Length == 4, "Selected target rows are unbounded or missing");
            Need(text.StartsWith("Viewed level: " + expected[i, 0] + ".\n", StringComparison.Ordinal), "Target depends on current+1 rather than selected offer");
            Need(text.Contains("with a Box: Parts +" + expected[i, 1] + "; with a Potato: Potatoes +" + expected[i, 2] + "."), "Selected target omits retained presence effects");
            Need(text.Contains("Raw bonus: Parts +" + expected[i, 3] + ", Potatoes +" + expected[i, 4] + "."), "Selected target omits cumulative raw bonus effects");
            Need(text.EndsWith("On presence; bonus before focus and booster, then storage caps.", StringComparison.Ordinal), "Presence/order/cap semantics missing");
            Need(!text.Contains("bought") && !text.Contains("Current") && !text.Contains("Next"), "Viewed lower/locked level is presented as ownership or a purchase promise");
        }
        foreach (string id in new[] { "arcade.pack.parts", "arcade.pack.potatoes", "arcade.boost.next-bonus" })
            Need(ArcadeCopy.OfferAttributes(ArcadeCatalog.Get(id)) == null, "Non-slot offer invents slot attributes");
        Need(ArcadeCopy.OfferAttributes(null) == null, "Null offer invents attributes");
        Need(ArcadeCopy.Label("house.repair") == "House · Repair" && ArcadeCopy.Label("arcade.slot.1") == "Slot Level I", "Existing stage/slot labels changed");
        Console.WriteLine("PASS selected I/II/III cumulative rows, owned-lower wording, non-slot/null and preserved labels");
    }

    static int Main(string[] args)
    {
        try
        {
            if (args.Length != 0)
            {
                Need(args.Length == 1 && args[0] == "offer-attributes", "Unknown focused copy group");
                SelectedOfferAttributes();
                Console.WriteLine("PASS ArcadeCopy selected-offer group/" + checks + "checks; no spins/session/storage/Unity."); return 0;
            }
            Identities(); AttributesTicketsCosts(); RulesAndReady(); CommittedEventProjection();
            SelectedOfferAttributes();
            Console.WriteLine("PASS ArcadeCopy:5groups/" + checks + "checks.31 private pure spins prepare valid display history; separate level funding is synthetic. No session/storage/Unity or pacing acceptance."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL ArcadeCopy: " + error); return 1; }
    }
}
