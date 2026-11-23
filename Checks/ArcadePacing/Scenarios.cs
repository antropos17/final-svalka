using System;
using System.Collections.Generic;
using System.Linq;
using JunkToNeon.Core;

internal static partial class Probe
{
    internal static readonly List<object> NaturalRows = new List<object>(), ControlRows = new List<object>(), MarginalRows = new List<object>(), CapRows = new List<object>();
    internal static readonly Dictionary<uint, ModelState> FirstStates = new Dictionary<uint, ModelState>();
    internal static readonly List<string> Csv = new List<string> { "phase,seed,policy,spins,level1,level2,level3,diamond_credits,diamond_spend,part_marginal,potato_marginal,target_pass" };
    static object Sorting(Book book, bool perfect)
    {
        long before = book.Game.Arcade.Diamonds; int ticket = book.Game.Arcade.Tickets;
        Need(book.Game.Arcade.ActiveRun == null, "Natural policy tried to replace a run");
        book.Do(book.Quote("start", ArcadeCatalog.Sorting)); Need(book.Game.Arcade.Tickets == ticket - 1, "Start did not debit one earned ticket");
        long instance = book.Game.Arcade.ActiveRun.InstanceSequence; var transcript = new List<int>();
        for (int item = 0; item < 8; item++)
        {
            var run = book.Game.Arcade.ActiveRun;
            Need(!run.Ready && run.Attempts == item && run.CurrentSortingSymbol >= 0 && run.CurrentSortingSymbol <= 3, "Sorting visible cursor/item differs");
            int target = perfect || item < 3 ? run.CurrentSortingSymbol : (run.CurrentSortingSymbol + 1) % 4;
            transcript.Add(target); book.Do(book.Quote("sort", ArcadeCatalog.Sorting, target));
            Need(book.Game.Arcade.Diamonds == before, "Input/Ready paid without explicit Claim");
        }
        var ready = book.Game.Arcade.ActiveRun; int expected = perfect ? 3 : 1;
        Need(ready.Ready && ready.Attempts == 8 && ready.Correct == (perfect ? 8 : 3) && ready.ReadyReward == expected && ready.CurrentSortingSymbol == -1,
            "Independent Sorting literal3/8 or8/8 reward mismatch");
        book.Do(book.Quote("claim", ArcadeCatalog.Sorting));
        Need(book.Game.Arcade.ActiveRun == null && book.Game.Arcade.Diamonds == before + expected && book.Game.Arcade.LastClaim.Diamonds == expected,
            "Explicit Sorting claim differs");
        return new { spin = book.Spins, instance, correct = perfect ? 8 : 3, expectedDiamonds = expected, transcript = string.Join("", transcript), claimKey = book.Game.Arcade.LastClaim.Key };
    }
    static void Levels(Book book, int[] affordable, int[] purchases)
    {
        for (int tries = 0; tries < 3 && book.Game.Arcade.SlotLevel < 3; tries++)
        {
            int next = book.Game.Arcade.SlotLevel + 1; var quote = book.Quote("buy", "arcade.slot." + next, mayRefuse: true);
            if (quote == null) break;
            if (affordable[next - 1] < 0) affordable[next - 1] = book.Spins;
            book.Do(quote); purchases[next - 1] = book.Spins;
            Need(book.Game.Arcade.SlotLevel == next, "Sequential slot purchase did not apply");
        }
    }
    static void Natural(uint seed, bool perfect)
    {
        var book = new Book(seed); string policy = perfect ? "visible-perfect-reserve" : "one-diamond-pack-then-levels";
        int firstTicket = -1, firstStart = -1, firstDiamond = -1, firstSpend = -1;
        int[] affordability = { -1, -1, -1 }, bought = { -1, -1, -1 }, firstEffect = { -1, -1, -1 };
        bool packed = false; var games = new List<object>(); var checkpoints = new List<object>();
        for (int i = 1; i <= 140; i++)
        {
            int levelBefore = book.Game.Arcade.SlotLevel;
            var ev = book.Spin(); if (i == 1 && !FirstStates.ContainsKey(seed)) FirstStates[seed] = book.Game.State;
            if (ev.TicketsAdded > 0 && firstTicket < 0) firstTicket = i;
            if (levelBefore >= 1 && ev.SlotPartsIncluded > 0 && firstEffect[0] < 0) firstEffect[0] = i;
            if (levelBefore >= 2 && ev.SlotPotatoesIncluded > 0 && firstEffect[1] < 0) firstEffect[1] = i;
            if (levelBefore >= 3 && book.Game.Expansion.LastResourceReceipt.BonusSymbol >= 0 && firstEffect[2] < 0) firstEffect[2] = i;
            if (book.Game.Arcade.Tickets > 0)
            {
                if (firstStart < 0)
                { firstStart = i; Need(i == 8 && book.Game.Arcade.SlotLevel == 0 && book.Game.State.houseStage == 0 && !book.Game.Expansion.BonusUnlocked, "First game was economically gated"); }
                games.Add(Sorting(book, perfect)); if (firstDiamond < 0) firstDiamond = i;
            }
            if (!perfect && !packed && book.Game.Arcade.Diamonds >= 1)
            { var q = book.Quote("buy", ArcadeCatalog.PartsPack); book.Do(q); packed = true; firstSpend = i; Need(q.DiamondsCost == 1 && q.PartsCredit == 5, "First useful pack price/credit drift"); }
            long spent = book.DiamondsSpent; Levels(book, affordability, bought);
            if (firstSpend < 0 && book.DiamondsSpent > spent) firstSpend = i;
            if (book.Game.Arcade.SlotLevel >= 2 && !book.Game.Expansion.BonusUnlocked && book.Game.Expansion.Parts >= 20 && book.Game.Expansion.Potatoes >= 10)
                book.Old("buy", "bonus.unlock");
            Levels(book, affordability, bought); book.Boost();
            if (ev.TicketsAdded > 0 || i == 24 || i == 64 || i == 140 || book.Game.Arcade.SlotLevel == 3)
                checkpoints.Add(new { spin = i, book.Game.Expansion.Parts, book.Game.Expansion.Potatoes, book.Game.Expansion.Energy,
                    book.Game.Arcade.Diamonds, book.Game.Arcade.Tickets, book.Game.Arcade.SlotLevel, book.Game.Expansion.BonusUnlocked });
            if (book.Game.Arcade.SlotLevel == 3) break;
        }
        Need(firstTicket == 8 && firstStart == 8 && firstDiamond == 8, "Earned first access/claim differs from literal8");
        bool[] targets = { bought[0] >= 0 && bought[0] <= 24, bought[1] >= 0 && bought[1] <= 64, bought[2] >= 0 && bought[2] <= 140 };
        NaturalRows.Add(new { seed, policy, starting = "genuine Fresh37;0tickets/0Diamonds", firstTicket, firstStart, firstDiamond, firstSpend,
            affordability, purchases = bought, firstObservedIncludedEffects = firstEffect, stoppedAtIII = book.Game.Arcade.SlotLevel == 3,
            modestTargets = perfect ? null : targets, firstPackTargetAt8 = perfect ? (bool?)null : firstSpend == 8,
            effectBoundary = "Stops atIII purchase; its first effective bonus can therefore be unobserved. Included I/II event components are not added to total credits.",
            games, checkpoints, facts = book.Facts() });
        Csv.Add("natural," + seed + "," + policy + "," + book.Spins + "," + string.Join(",", bought) + "," + book.DiamondCredits + "," + book.DiamondsSpent + ",,," + targets.All(v => v));
        Console.WriteLine("DONE " + policy + " seed=" + seed + " levels=" + string.Join("/", bought) + " spin=" + book.Spins);
    }
    static object MemoryGame(Book book)
    {
        book.Do(book.Quote("start", ArcadeCatalog.Memory)); int[] known = { -1, -1, -1, -1, -1, -1 }; int first = -1;
        var transcript = new List<string>(); long before = book.Game.Arcade.Diamonds; int guard = 0;
        while (!book.Game.Arcade.ActiveRun.Ready)
        {
            Need(++guard <= 27, "Finite Memory transcript bound exceeded"); var r = book.Game.Arcade.ActiveRun;
            for (int card = 0; card < 6; card++) if (r.MemoryFaces[card] >= 0) known[card] = r.MemoryFaces[card];
            if (r.Phase == "mismatch") { book.Do(book.Quote("continue", r.Id)); transcript.Add("c"); first = -1; continue; }
            int[] available = Enumerable.Range(0, 6).Where(c => (r.MatchedMask & (1 << c)) == 0).ToArray();
            int target;
            if (r.Phase == "first")
            {
                var pair = available.SelectMany(a => available.Where(b => b > a && known[a] >= 0 && known[a] == known[b]).Select(b => a)).ToArray();
                target = pair.Length > 0 ? pair[0] : available.Where(c => known[c] < 0).DefaultIfEmpty(available[0]).First(); first = target;
            }
            else
            {
                Need(r.Phase == "second" && first >= 0, "Unknown Memory phase/first selection");
                int[] candidates = available.Where(c => c != first).ToArray();
                target = candidates.Where(c => known[c] >= 0 && known[c] == known[first]).DefaultIfEmpty(-1).First();
                if (target < 0) target = candidates.Where(c => known[c] < 0).DefaultIfEmpty(candidates[0]).First();
            }
            book.Do(book.Quote("pick", r.Id, target)); transcript.Add(target.ToString());
            var after = book.Game.Arcade.ActiveRun;
            for (int card = 0; card < 6; card++) if (after.MemoryFaces[card] >= 0) known[card] = after.MemoryFaces[card];
            Need(book.Game.Arcade.Diamonds == before, "Memory auto-claimed on a pick");
        }
        var ready = book.Game.Arcade.ActiveRun; int expected = ready.MatchedPairs != 3 ? 0 : ready.Attempts <= 4 ? 3 : ready.Attempts <= 6 ? 2 : 1;
        Need(ready.Attempts <= 9 && ready.ReadyReward == expected && book.Game.Arcade.Diamonds == before, "Independent Memory scoring/Ready mismatch");
        int attempts = ready.Attempts, pairs = ready.MatchedPairs; book.Do(book.Quote("claim", ArcadeCatalog.Memory));
        Need(book.Game.Arcade.Diamonds == before + expected, "Memory explicit claim mismatch");
        return new { attempts, pairs, expectedDiamonds = expected, transcript = string.Join("", transcript), knowledge = "Only masked revealed faces accumulated after committed inputs; no hidden board read" };
    }
    static void TicketControl(uint seed)
    {
        var book = new Book(seed); var rows = new List<object>();
        for (int spin = 1; spin <= 37; spin++)
        {
            book.Spin();
            if (spin == 24 || spin == 32 || spin == 37)
            { Need(book.Game.Arcade.Tickets == 3 && book.Game.Arcade.TicketProgress == spin % 8, "Ticket cap/cycle banked overflow"); rows.Add(new { spin, book.Game.Arcade.Tickets, book.Game.Arcade.TicketProgress }); }
        }
        Need(book.Game.Arcade.SlotLevel == 0 && !book.Game.Expansion.BonusUnlocked, "Memory control lost ungated baseline");
        var memory = MemoryGame(book);
        Need(book.Game.Arcade.Tickets == 2 && book.Game.Arcade.TicketProgress == 5, "Mini actions reset/refunded/banked ticket progress");
        for (int spin = 0; spin < 3; spin++) book.Spin();
        Need(book.Game.Arcade.Tickets == 3 && book.Game.Arcade.TicketProgress == 0, "Remaining partial cycle was not preserved");
        ControlRows.Add(new { seed, policy = "hold37-visible-Memory-then3", rows, memory, facts = book.Facts() });
        Console.WriteLine("DONE ticket/Memory seed=" + seed);
    }
    static Book Prepared(uint seed, ModelState source, int level)
    {
        var b = new Book(seed, Fund(source, 200, 200, 30, 30));
        for (int n = 1; n <= level; n++)
        {
            if (n == 3) b.Old("buy", "bonus.unlock");
            b.Do(b.Quote("buy", "arcade.slot." + n));
        }
        return b;
    }
    static void Pair(uint seed, int level)
    {
        var control = Prepared(seed, FirstStates[seed], level - 1); var subject = Prepared(seed, FirstStates[seed], level);
        if (level == 3 && !control.Game.Expansion.BonusUnlocked) control.Old("buy", "bonus.unlock");
        var paid = new { prerequisite = control.Facts(), subject = subject.Facts(), quote = ArcadeCatalog.Get("arcade.slot." + level) };
        control = new Book(seed, Fund(control.Game.State, 40, 40, 30)); subject = new Book(seed, Fund(subject.Game.State, 40, 40, 30));
        long extraParts = 0, extraPotatoes = 0; int first = -1, tripleBoxes = 0; var bonuses = new List<object>();
        for (int i = 1; i <= 40; i++)
        {
            control.Drain(); subject.Drain(); control.Boost(); subject.Boost();
            var a = control.Spin(); var b = subject.Spin(); var receipt = subject.Game.Expansion.LastResourceReceipt;
            var symbols = subject.Game.State.lastSpin.symbols;
            Need(control.Game.State.seed == subject.Game.State.seed && control.Game.State.lastSpin.symbols.SequenceEqual(symbols) &&
                control.Game.Expansion.BonusSeed == subject.Game.Expansion.BonusSeed && control.Game.Expansion.ArcadeData.ChallengeSeed == subject.Game.Expansion.ArcadeData.ChallengeSeed &&
                control.Game.Arcade.TicketProgress == subject.Game.Arcade.TicketProgress, "Paired level changed primary/bonus/challenge/ticket stream");
            int p = level == 1 && symbols.Contains(1) ? 1 : level == 3 && receipt.BonusSymbol >= 0 ? (receipt.Boosted ? 2 : 1) : 0;
            int potato = level == 2 && symbols.Contains(0) ? 1 : level == 3 && receipt.BonusSymbol >= 0 ? (receipt.Boosted ? 2 : 1) : 0;
            Need(b.PartsAdded - a.PartsAdded == p && b.PotatoesAdded - a.PotatoesAdded == potato && b.EnergyAdded == a.EnergyAdded &&
                b.CoinIncome == a.CoinIncome && b.ConstructionDebit == a.ConstructionDebit, "Exact once-per-presence/raw bonus marginal mismatch");
            extraParts += p; extraPotatoes += potato; if (first < 0 && p + potato > 0) first = i;
            if (level == 1 && symbols.All(x => x == 1)) { tripleBoxes++; Need(p == 1, "Box triple granted more than once"); }
            if (receipt.BonusSymbol >= 0) bonuses.Add(new { futureSpin = i, receipt.BonusSymbol, receipt.Boosted, p, potato });
        }
        MarginalRows.Add(new { seed, level, population = "synthetic funded actual purchases; equal40/40/30 after payment", paid,
            firstEffectSpin = first, extraParts, extraPotatoes, observedBoxTriples = tripleBoxes, controls = control.Facts(), subject = subject.Facts(), bonuses,
            roiBoundary = "40-spin credited resource component only; Diamond cost is not repaid by resource production, no full-price ROI claim" });
        Csv.Add("marginal," + seed + ",slot-" + level + ",40,,,,,," + extraParts + "," + extraPotatoes + ",True");
        Console.WriteLine("DONE marginal seed=" + seed + " level=" + level + " extra=" + extraParts + "/" + extraPotatoes);
    }
    static void CapControls()
    {
        var prepared = Prepared(1, FirstStates[1], 3); var baseState = prepared.Game.State;
        foreach (int parts in new[] { 200, 198, 195 })
        {
            var b = new Book(1, Fund(baseState, parts, 40, 30, 30)); var q = b.Quote("buy", ArcadeCatalog.PartsPack, mayRefuse: true);
            Need((parts <= 195) == (q != null), "Pack did not require all5headroom"); if (q != null) b.Do(q);
            CapRows.Add(new { kind = "pack-headroom", parts, accepted = q != null, facts = b.Facts() });
        }
        foreach (bool both in new[] { false, true })
        {
            var b = new Book(1, Fund(baseState, 200, both ? 200 : 0, 0)); var rows = new List<object>();
            for (int i = 1; i <= 5; i++)
            {
                var ev = b.Spin(); var r = b.Game.Expansion.LastResourceReceipt;
                Need(ev.PartsAdded == 0 && ev.SlotPartsIncluded == 0, "Full Parts cap did not suppress included slot credit");
                if (both) Need(ev.PotatoesAdded == 0 && ev.SlotPotatoesIncluded == 0, "Both-full case granted resources");
                rows.Add(new { i, ev.PartsAdded, ev.PotatoesAdded, ev.SlotPartsIncluded, ev.SlotPotatoesIncluded, r.BonusSymbol });
            }
            CapRows.Add(new { kind = both ? "both-full-no-credit" : "parts-full-lost-part-component", rows, facts = b.Facts() });
        }
        var diamond = new Book(1, Fund(baseState, 40, 40, 30, 30)); diamond.Do(diamond.Quote("buy", ArcadeCatalog.BonusBoost));
        Need(diamond.Game.Expansion.BoostArmed && diamond.Game.Expansion.Energy == 30 && diamond.DiamondsSpent == 1, "Diamond boost used another flag/Energy");
        int writes = diamond.Store.Writes; bool refused = false;
        try { diamond.Game.GetExpansionAction("boost", "boost.next-bonus"); } catch (InvalidOperationException) { refused = true; }
        Need(refused && diamond.Store.Writes == writes, "Old Energy boost stacked with the stored Diamond charge");
        CapRows.Add(new { kind = "one-Diamond-shared-boost", facts = diamond.Facts() });
    }
}
