using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JunkToNeon.Core;

internal static partial class Probe
{
    internal const string CoreCommit = "fa9f3f20df6fe71de64e6045ad7e09cb97d0b3cc";
    internal static readonly uint[] Seeds = { 1, 12305, 305419896 };
    internal static readonly JsonStateCodec Codec = new JsonStateCodec();
    internal static readonly Stopwatch Timer = Stopwatch.StartNew();
    internal static int Checks, ForegroundCalls, GameActions, OtherCommands;
    internal static long Clock;
    internal static Memory Active;
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static string StateHash(ModelState state) => Hash(Encoding.UTF8.GetBytes(Codec.Encode(state)));
    internal static void Need(bool ok, string why) { Checks++; if (!ok) throw new InvalidOperationException(why); }
    internal static void Limit()
    { Need(ForegroundCalls <= 1800 && GameActions <= 1400 && OtherCommands <= 3000 && Timer.Elapsed.TotalSeconds < 60, "Declared mode budget reached"); }
    internal sealed class Memory : ISnapshotStorage
    {
        internal string Raw; internal int Writes;
        public string ReadPrimary() => Raw;
        public string ReadBackup() => null;
        public void Write(string raw) { Raw = raw; Writes++; }
    }
    internal sealed class Book
    {
        internal readonly Memory Store;
        internal readonly GameSession Game;
        internal readonly string StartHash;
        internal int Spins, Starts, Claims;
        internal long PartCredits, PotatoCredits, EnergyCredits, DiamondCredits, TicketCredits;
        internal long PartsSpent, PotatoesSpent, EnergySpent, DiamondsSpent, TicketsSpent, CoinIncome, TradeCoins;
        internal readonly StringBuilder Primary = new StringBuilder(), Actions = new StringBuilder();
        readonly long initialParts, initialPotatoes, initialEnergy, initialDiamonds, initialTickets, initialCoins;
        uint expectedSeed;
        internal Book(uint seed, ModelState state = null)
        {
            Store = new Memory { Raw = state == null ? null : Codec.Encode(state) };
            Game = GameSession.Open(new GameStore(Store, Codec), seed, initializeJourney: true);
            Need(!Game.RecoveryRequired, "Private opening refused: " + Game.Error);
            var e = Game.Expansion; var a = Game.Arcade;
            initialParts = e.Parts; initialPotatoes = e.Potatoes; initialEnergy = e.Energy;
            initialDiamonds = a.Diamonds; initialTickets = a.Tickets; initialCoins = Game.State.coins; expectedSeed = Game.State.seed;
            StartHash = StateHash(Game.State);
            if (state == null) Need(initialCoins == 37 && !a.IsAdopted && initialTickets == 0 && initialDiamonds == 0, "Fresh grant/defaults differ");
        }
        internal ArcadeQuote Quote(string action, string id, int target = -1, bool mayRefuse = false)
        {
            var view = Game.Arcade; var run = view.ActiveRun; long instance = action == "buy" || action == "start" ? 0 : run.InstanceSequence;
            string raw = Store.Raw; int writes = Store.Writes; ArcadeQuote quote = null;
            try { quote = Game.GetArcadeQuote(action, id, view.Sequence, instance, target); }
            catch (InvalidOperationException error)
            {
                Need(mayRefuse && (error.Message == "Insufficient Arcade resources" || error.Message == "Slot level/prerequisite locked" ||
                    error.Message == "Resource pack requires full headroom"), "Unexpected Arcade refusal: " + error.Message);
            }
            Need(Store.Raw == raw && Store.Writes == writes, "Quote wrote private storage");
            if (quote != null) Need(quote.ExpectedSequence == view.Sequence && quote.ExpectedInstance == instance &&
                (run == null || action == "start" || action == "buy" || quote.ExpectedCursor == run.Cursor && quote.ExpectedPhase == run.Phase), "Quote did not bind displayed view");
            return quote;
        }
        internal void Do(ArcadeQuote quote)
        {
            Need(quote != null, "Missing retained Arcade quote"); Limit(); Active = Store;
            var before = Game.State; var e = Game.Expansion; uint bonus = e.BonusSeed, challenge = e.ArcadeData.ChallengeSeed;
            var a = Game.Arcade; int writes = Store.Writes;
            Need(a.Sequence == quote.ExpectedSequence, "Utility attempted fresh execution of a stale bound quote");
            Game.ExecuteArcade(quote, 1700000000 + ++Clock);
            if (quote.Action == "buy") OtherCommands++; else GameActions++;
            PartsSpent += quote.PartsCost; PotatoesSpent += quote.PotatoesCost; DiamondsSpent += quote.DiamondsCost; TicketsSpent += quote.TicketsCost;
            PartCredits += quote.PartsCredit; PotatoCredits += quote.PotatoesCredit; DiamondCredits += quote.DiamondsCredit;
            if (quote.Action == "start") Starts++; if (quote.Action == "claim") Claims++;
            var after = Game.State; var current = Game.Expansion; var view = Game.Arcade; var receipt = view.LastEvent;
            Need(Store.Writes == writes + 2 && after.seed == before.seed && current.BonusSeed == bonus &&
                (quote.Action == "start" || current.ArcadeData.ChallengeSeed == challenge), "Arcade command changed primary/bonus/nonstart challenge RNG or journal phases");
            Need(view.TicketProgress == a.TicketProgress && view.ForegroundSpins == a.ForegroundSpins && receipt.Sequence == quote.Sequence &&
                receipt.PartsAdded == quote.PartsCredit && receipt.PotatoesAdded == quote.PotatoesCredit && receipt.DiamondsAdded == quote.DiamondsCredit &&
                receipt.CoinIncome == 0 && receipt.ConstructionDebit == 0, "Arcade command actual credit/counter mismatch");
            Actions.Append(quote.Action).Append(':').Append(quote.Id).Append(':').Append(quote.Target).Append(':').Append(quote.Key).Append('|'); Ledger();
        }
        internal void Old(string action, string id)
        {
            Limit(); Active = Store; var before = Game.State; var e = Game.Expansion;
            uint bonus = e.BonusSeed, challenge = e.ArcadeData.ChallengeSeed; int writes = Store.Writes;
            var q = Game.GetExpansionAction(action, id); Game.ExecuteExpansion(q, 1700000000 + ++Clock); OtherCommands++;
            PartsSpent += q.PartsCost; PotatoesSpent += q.PotatoesCost; EnergySpent += q.EnergyCost; TradeCoins += q.CoinsReward;
            Need(q.CoinsCost == 0 && Store.Writes == writes + 2 && Game.State.seed == before.seed && Game.Expansion.BonusSeed == bonus &&
                Game.Expansion.ArcadeData.ChallengeSeed == challenge, "Old command changed randomness, price authority or journal phases"); Ledger();
        }
        internal ArcadeEvent Spin()
        {
            Limit(); Need(ForegroundCalls < 1800, "Foreground cap reached"); Active = Store;
            var before = Game.State; var e = Game.Expansion; var a = Game.Arcade; uint challenge = e.ArcadeData?.ChallengeSeed ?? EconomyRules.ArcadeInitialSeed;
            int writes = Store.Writes; Game.ExecuteChapterSpin(before.sequence + 1, 1700000000 + ++Clock, manualSpin: true); ForegroundCalls++; Spins++;
            var after = Game.State; var r = after.lastSpin; var ev = Game.Arcade.LastResourceReceipt; var total = Game.Expansion.LastResourceReceipt;
            var draw = GameLogic.Draw(expectedSeed, before.sequence + 1); expectedSeed = draw.seedAfter;
            int distinct = r.symbols.Distinct().Count(), payout = distinct == 1 ? 23 : distinct == 2 ? 8 : 3;
            Need(after.seed == expectedSeed && r.symbols.SequenceEqual(draw.symbols) && r.reward == payout && r.bonus == 0 && r.chapter.Effects.TotalCoins == 0,
                "Primary draw/literal payout/absent economic upgrades differ");
            int ticket = a.TicketProgress == 7 && a.Tickets < 3 ? 1 : 0;
            Need(Store.Writes == writes + 2 && ev.Sequence == after.lastSpin.sequence && ev.CoinIncome == payout && ev.ConstructionDebit == 0 &&
                ev.PartsAdded == total.Parts && ev.PotatoesAdded == total.Potatoes && ev.EnergyAdded == total.Energy && ev.DiamondsAdded == 0 &&
                ev.TicketsAdded == ticket && Game.Arcade.TicketProgress == (a.TicketProgress + 1) % 8 &&
                Game.Expansion.ArcadeData.ChallengeSeed == challenge, "TOTAL/included ticket/resource/coin receipt authority mismatch");
            Need(ev.SlotPartsIncluded <= ev.PartsAdded && ev.SlotPotatoesIncluded <= ev.PotatoesAdded, "Included slots exceed TOTAL credits");
            CoinIncome += ev.CoinIncome; PartCredits += ev.PartsAdded; PotatoCredits += ev.PotatoesAdded; EnergyCredits += ev.EnergyAdded; TicketCredits += ev.TicketsAdded;
            Primary.Append(string.Join("", r.symbols)).Append('/').Append(payout).Append('/').Append(after.seed).Append('|'); Ledger(); return ev;
        }
        internal void Ledger()
        {
            var e = Game.Expansion; var a = Game.Arcade;
            Need(!Game.RecoveryRequired && Game.State.pending == null && Game.State.coins == initialCoins + CoinIncome + TradeCoins &&
                e.Parts == initialParts + PartCredits - PartsSpent && e.Potatoes == initialPotatoes + PotatoCredits - PotatoesSpent &&
                e.Energy == initialEnergy + EnergyCredits - EnergySpent && a.Diamonds == initialDiamonds + DiamondCredits - DiamondsSpent &&
                a.Tickets == initialTickets + TicketCredits - TicketsSpent, "Independent coin/resource/Diamond/ticket ledger differs");
        }
        internal void Boost() { if (Game.Expansion.BonusUnlocked && !Game.Expansion.BoostArmed && Game.Expansion.Energy >= 15) Old("boost", "boost.next-bonus"); }
        internal void Drain() { while (Game.Expansion.Parts >= 60) Old("buy", "trade.parts"); while (Game.Expansion.Potatoes >= 60) Old("buy", "trade.potatoes"); }
        internal object Facts() => new { StartHash, finalHash = StateHash(Game.State), Spins, Starts, Claims, PartCredits, PotatoCredits, EnergyCredits,
            DiamondCredits, TicketCredits, PartsSpent, PotatoesSpent, EnergySpent, DiamondsSpent, TicketsSpent, CoinIncome, TradeCoins,
            primarySha256 = Hash(Encoding.UTF8.GetBytes(Primary.ToString())), actionSha256 = Hash(Encoding.UTF8.GetBytes(Actions.ToString())) };
    }
    internal static ModelState Fund(ModelState state, int parts, int potatoes, int energy, long? diamonds = null)
    {
        var copy = state.Copy(); var e = new ExpansionDraft(copy.expansion) { Parts = parts, Potatoes = potatoes, Energy = energy };
        if (diamonds.HasValue) { var a = new ArcadeDraft(e.ArcadeData) { Diamonds = diamonds.Value }; e.ArcadeData = a.Freeze(); }
        copy.expansion = e.Freeze(); GameLogic.Validate(copy); return Codec.Decode(Codec.Encode(copy));
    }
}
