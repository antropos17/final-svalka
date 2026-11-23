using System;

namespace JunkToNeon.Core
{
    // Ported from this project's TypeScript model/slot/progression implementation.
    // No code or economy was imported from an unrelated game.
    public static partial class EconomyRules
    {
        public const int ContentVersion = 1;
        public const long InitialCoins = 37;
        public const int TripleReward = 23, PairReward = 8, DifferentReward = 3;
        public static readonly int[] Symbols = { 0, 1, 2, 3 };
        public static readonly string[] SymbolNames = { "potato", "box", "trash", "pigeon" };
        public static readonly long[] Prices = { 40, 600 };
        // Provisional two-project prototype tuning, not a measured/final balance.
        public const int WorkshopTarget = 3, WorkshopCost = 120, WorkshopBonus = 1;
        public const int RecyclerTarget = 4, RecyclerCost = 180, PigeonLoftTarget = 5, PigeonLoftCost = 240;
        public const long OfflineCapSeconds = 8 * 60 * 60, OfflineSecondsPerCoin = 60;
        public const float SpinDurationSeconds = 3.5f, AutoGapSeconds = 0.25f;
        public static long Price(int stage) { return stage >= 0 && stage < Prices.Length ? Prices[stage] : throw new ArgumentOutOfRangeException(nameof(stage)); }
    }

    [Serializable] public sealed class SpinReceipt
    {
        public long sequence;
        public int[] symbols;
        public int reward, bonus;
        public WaveOperation wave;
        public ChapterOperation chapter;
        public JourneyOperation journey;
        public ExpansionOperation expansion;
        public SpinReceipt Copy() { return new SpinReceipt { sequence = sequence, symbols = symbols == null ? null : (int[])symbols.Clone(), reward = reward, bonus = bonus, wave = wave, chapter = chapter, journey = journey, expansion=expansion }; }
    }
    [Serializable] public sealed class PendingOperation
    {
        public long sequence;
        public string kind;
        public int[] symbols;
        public uint seedAfter;
        public int target;
        public long at;
        public WaveOperation wave;
        public ChapterOperation chapter;
        public JourneyOperation journey;
        public ExpansionOperation expansion;
        public PendingOperation Copy() { return new PendingOperation { sequence = sequence, kind = kind, symbols = symbols == null ? null : (int[])symbols.Clone(), seedAfter = seedAfter, target = target, at = at, wave = wave, chapter = chapter, journey = journey, expansion=expansion }; }
    }
    [Serializable] public sealed class OfflineReceipt
    {
        public long sequence, from, to, seconds, coins;
        public int workshop;
        public OfflineReceipt Copy() { return new OfflineReceipt { sequence=sequence,from=from,to=to,seconds=seconds,coins=coins,workshop=workshop }; }
    }
    public static class OfflineIncome
    {
        public const long MaxUtcSeconds = 253402300799;
        public static long Seconds(long from,long to)
        {
            if(from<0||to<0||from>MaxUtcSeconds||to>MaxUtcSeconds)throw new InvalidOperationException("Invalid UTC timestamp");
            return from==0?0:Math.Min(EconomyRules.OfflineCapSeconds,Math.Max(0,to-from));
        }
        public static long Coins(long from,long to,int workshop)
        {
            if(workshop<0||workshop>1)throw new InvalidOperationException("Invalid offline upgrade");
            return Seconds(from,to)/EconomyRules.OfflineSecondsPerCoin*workshop;
        }
    }
    [Serializable] public sealed class ModelState
    {
        public int schemaVersion = 1, contentVersion = EconomyRules.ContentVersion;
        public long revision, sequence, coins;
        public int houseStage, selectedStage, autoHintSeen, workshopBuilt;
        public uint seed;
        public SpinReceipt lastSpin;
        public long lastSettledAt;
        public long lastSettlementSeen;
        public OfflineReceipt lastSettlement;
        public PendingOperation pending;
        public WaveProgress wave;
        public ChapterState chapter;
        public JourneyState journey;
        public ExpansionState expansion;
        public static ModelState Fresh(uint seed) { return new ModelState { coins = EconomyRules.InitialCoins, seed = seed == 0 ? 1u : seed }; }
        public ModelState Copy() { return new ModelState { schemaVersion = schemaVersion, contentVersion = contentVersion, revision = revision, sequence = sequence, coins = coins, houseStage = houseStage, selectedStage = selectedStage, autoHintSeen = autoHintSeen, workshopBuilt = workshopBuilt, seed = seed, wave = wave, chapter = chapter, journey = journey, expansion=expansion, lastSettledAt=lastSettledAt,lastSettlementSeen=lastSettlementSeen,lastSettlement=lastSettlement?.Copy(),lastSpin = lastSpin == null ? null : lastSpin.Copy(), pending = pending == null ? null : pending.Copy() }; }
    }

    public static class GameLogic
    {
        public static bool Available(ModelState s, int target)
        {
            var w = WaveProgress.Of(s);
            return ProgressionCatalog.Available(target, s.houseStage, s.workshopBuilt, w.RecyclerBuilt, w.PigeonLoftBuilt);
        }
        public static long TargetPrice(int target) { return ProgressionCatalog.Price(target); }
        public static PendingOperation Draw(uint seed, long sequence)
        {
            if (seed == 0) throw new InvalidOperationException("Zero RNG seed");
            var symbols = new int[3];
            for (var i = 0; i < symbols.Length; i++)
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                symbols[i] = EconomyRules.Symbols[seed >> 30];
            }
            return new PendingOperation { kind = "spin", sequence = sequence, symbols = symbols, seedAfter = seed };
        }
        public static int Payout(int[] symbols)
        {
            ValidateSymbols(symbols);
            return symbols[0] == symbols[1] && symbols[1] == symbols[2] ? EconomyRules.TripleReward :
                symbols[0] == symbols[1] || symbols[0] == symbols[2] || symbols[1] == symbols[2] ? EconomyRules.PairReward : EconomyRules.DifferentReward;
        }
        static void ValidateSymbols(int[] symbols)
        {
            if (symbols == null || symbols.Length != 3) throw new InvalidOperationException("Invalid reel count");
            foreach (var symbol in symbols) if (Array.IndexOf(EconomyRules.Symbols, symbol) < 0) throw new InvalidOperationException("Unknown reel symbol");
        }
        public static void Validate(ModelState s)
        {
            if(ExpansionEconomy.IsModern(s)){ExpansionEconomy.Validate(s);return;}
            if(JourneyEconomy.IsModern(s)){JourneyEconomy.Validate(s);return;}
            if(ChapterEconomy.IsModern(s)){ChapterEconomy.Validate(s);return;}
            if (s == null || s.schemaVersion != 1 || s.contentVersion != EconomyRules.ContentVersion || s.revision < 0 || s.sequence < 0 || s.sequence > (long.MaxValue - 2) / 2 || s.coins < 0 || s.houseStage < 0 || s.houseStage > 2 || s.seed == 0) throw new InvalidOperationException("Invalid save format or version");
            if (s.workshopBuilt < 0 || s.workshopBuilt > 1 || (s.workshopBuilt == 1 && s.houseStage < 1)) throw new InvalidOperationException("Invalid workshop state");
            // Offline income may fund a target; only a later foreground spin/selection purchases it.
            if (s.selectedStage != 0 && !Available(s, s.selectedStage)) throw new InvalidOperationException("Invalid selected project");
            if(s.lastSettledAt<0||s.lastSettledAt>OfflineIncome.MaxUtcSeconds)throw new InvalidOperationException("Invalid settlement clock");
            if(s.lastSettlementSeen<0||s.lastSettlementSeen>(s.lastSettlement?.sequence??0))throw new InvalidOperationException("Invalid settlement presentation marker");
            if(s.lastSettlement!=null)
            {
                var r=s.lastSettlement;
                if(r.sequence<1||r.sequence>s.sequence||r.to<r.from||r.to==0||r.to>s.lastSettledAt||r.workshop>s.workshopBuilt||
                    r.seconds!=OfflineIncome.Seconds(r.from,r.to)||r.coins!=OfflineIncome.Coins(r.from,r.to,r.workshop))throw new InvalidOperationException("Invalid offline receipt");
            }
            if (s.autoHintSeen < 0 || s.autoHintSeen > 1 || (s.houseStage == 0 && s.autoHintSeen != 0)) throw new InvalidOperationException("Invalid AUTO hint state");
            if (s.lastSpin != null && (s.lastSpin.sequence < 1 || s.lastSpin.sequence > s.sequence || s.lastSpin.reward != Payout(s.lastSpin.symbols))) throw new InvalidOperationException("Invalid spin receipt");
            if (s.lastSpin != null && (s.lastSpin.bonus < 0 || s.lastSpin.bonus > EconomyRules.WorkshopBonus || (s.lastSpin.bonus != 0 && s.workshopBuilt != 1))) throw new InvalidOperationException("Invalid workshop bonus");
            if (s.pending != null)
            {
                var p = s.pending;
                if (p.sequence != s.sequence + 1||p.at<0||p.at>OfflineIncome.MaxUtcSeconds) throw new InvalidOperationException("Invalid pending sequence or clock");
                if (p.kind == "spin")
                {
                    var expected = Draw(s.seed, p.sequence);
                    ValidateSymbols(p.symbols);
                    if (p.target != 0 || p.seedAfter != expected.seedAfter) throw new InvalidOperationException("Invalid frozen result");
                    for (var i = 0; i < 3; i++) if (p.symbols[i] != expected.symbols[i]) throw new InvalidOperationException("Invalid frozen symbols");
                }
                else if (p.kind == "hint")
                {
                    if (s.houseStage < 1 || s.autoHintSeen != 0 || p.target != 0 || p.symbols != null || p.seedAfter != 0) throw new InvalidOperationException("Invalid pending hint");
                }
                else if(p.kind=="settle"||p.kind=="clock")
                {
                    if(p.at<=s.lastSettledAt||p.symbols!=null||p.target!=0||p.seedAfter!=0)throw new InvalidOperationException("Invalid pending settlement");
                }
                else if(p.kind=="seen")
                {
                    if(s.lastSettlement==null||s.lastSettlement.coins<=0||s.lastSettlementSeen==s.lastSettlement.sequence||p.target!=0||p.symbols!=null||p.seedAfter!=0)throw new InvalidOperationException("Invalid settlement acknowledgement");
                }
                else if(p.kind=="upgrade")
                { if(p.target!=0||p.symbols!=null||p.seedAfter!=0)throw new InvalidOperationException("Invalid pending upgrade"); }
                else if (p.kind != "select" || !Available(s, p.target) || p.symbols != null || p.seedAfter != 0) throw new InvalidOperationException("Invalid pending project");
            }
            WaveEconomy.Validate(s);
            if (s.revision != s.sequence * 2 + (s.pending == null ? 0 : 1)) throw new InvalidOperationException("Invalid revision sequence");
        }
        public static ModelState Reserve(ModelState state, string kind, int target, long sequence, long at=0)
        {return ExpansionEconomy.Compose(state,s=>ReserveHistorical(s,kind,target,sequence,at));}
        internal static ModelState ReserveHistorical(ModelState state, string kind, int target, long sequence, long at=0)
        {
            if(JourneyEconomy.IsModern(state))return JourneyEconomy.ReserveLegacyHistorical(state,kind,target,sequence,at);
            if(state?.chapter!=null)return ChapterEconomy.ReserveLegacyHistorical(state,kind,target,sequence,at);
            Validate(state);
            if (state.pending != null) throw new InvalidOperationException("Pending operation requires recovery");
            if (sequence <= state.sequence && sequence >= 1) return state;
            if (sequence != state.sequence + 1) throw new InvalidOperationException("Operation sequence gap");
            if (kind != "spin" && kind != "select" && kind != "hint" && kind!="settle"&&kind!="clock"&&kind!="seen"&&kind!="upgrade") throw new InvalidOperationException("Unknown operation");
            if (kind == "hint" && (target != 0 || state.houseStage < 1 || state.autoHintSeen != 0)) throw new InvalidOperationException("Hint unavailable or already acknowledged");
            if (kind == "select" && !Available(state, target)) throw new InvalidOperationException("Project stage mismatch");
            var next = state.Copy();
            next.pending = kind == "spin" ? Draw(state.seed, sequence) : new PendingOperation { kind = kind, sequence = sequence, target = target };
            next.pending.at=at;
            next.pending.wave=WaveEconomy.Freeze(state,next.pending);
            next.revision = checked(next.revision + 1);
            Validate(next); return next;
        }
        public static ModelState ApplyPending(ModelState state)
        {
            if(ExpansionEconomy.IsModern(state))return ExpansionEconomy.ApplyPending(state);
            if(JourneyEconomy.IsModern(state))return JourneyEconomy.ApplyPending(state);
            if(ChapterEconomy.IsModern(state))return ChapterEconomy.ApplyPending(state);
            Validate(state);
            if (state.pending == null) return state;
            var next = state.Copy(); var p = next.pending;
            if (p.kind == "spin")
            {
                var reward = Payout(p.symbols);
                int bonus = state.workshopBuilt == 1 ? EconomyRules.WorkshopBonus : 0;
                if(p.wave==null)next.coins = checked(checked(next.coins + reward) + bonus);
                else WaveEconomy.ApplySpin(state,next,p);
                next.seed = p.seedAfter;
                next.lastSpin = new SpinReceipt { sequence = p.sequence, symbols = (int[])p.symbols.Clone(), reward = reward, bonus = bonus, wave = p.wave };
            }
            else if(p.kind=="upgrade")WaveEconomy.ApplyUpgrade(state,next,p);
            else if (p.kind == "hint") next.autoHintSeen = 1;
            else if(p.kind=="seen")next.lastSettlementSeen=state.lastSettlement.sequence;
            else if(p.kind=="settle")
            {
                long to=Math.Max(state.lastSettledAt,p.at);
                var receipt=new OfflineReceipt {sequence=p.sequence,from=state.lastSettledAt,to=to,workshop=state.workshopBuilt,
                    seconds=OfflineIncome.Seconds(state.lastSettledAt,to),coins=OfflineIncome.Coins(state.lastSettledAt,to,state.workshopBuilt)};
                next.coins=checked(next.coins+receipt.coins);next.lastSettlement=receipt;
            }
            else if(p.kind=="clock") { /* Foreground checkpoint: no reward and no receipt replacement. */ }
            else { next.selectedStage = p.target; if(p.target>=4 && next.wave==null)next.wave=WaveProgress.Empty; }
            if((p.kind=="spin"||p.kind=="select")&&next.selectedStage != 0 && next.coins >= TargetPrice(next.selectedStage))
            {
                next.coins -= p.wave==null?TargetPrice(next.selectedStage):p.wave.Price;
                if (next.selectedStage == EconomyRules.WorkshopTarget) next.workshopBuilt = 1;
                else if(next.selectedStage>=4)WaveEconomy.Build(next,next.selectedStage);
                else next.houseStage++;
                next.selectedStage = 0;
            }
            if(p.at!=0)next.lastSettledAt=Math.Max(next.lastSettledAt,p.at);
            next.sequence = p.sequence; next.pending = null; next.revision = checked(next.revision + 1);
            Validate(next); return next;
        }
    }

    // Derived presentation: viewing a goal never selects or purchases it.
    public static class HouseGoal
    {
        public static bool Complete(ModelState s) { if(s.chapter!=null)return ChapterCatalog.Rules.IsChapterComplete(s.chapter.Progress);var w=WaveProgress.Of(s);return ProgressionCatalog.Complete(s.houseStage,s.workshopBuilt,w.RecyclerBuilt,w.PigeonLoftBuilt); }
        public static bool NeedsAutoHint(ModelState s) { return s.houseStage == 1 && s.autoHintSeen == 0; }
        public static string Marker(ModelState s)
        {
            if (Complete(s)) return "CHAPTER COMPLETE · ALL BUILDINGS READY";
            if (s.houseStage == 2) return "HOUSE COMPLETE · CHOOSE ANOTHER BUILDING";
            var goal = s.houseStage == 0 ? "REPAIR" : "HOUSE UPGRADE";
            return (s.selectedStage == s.houseStage + 1 ? "IN PROGRESS: " : "SELECT: ") + goal + " · " + Math.Min(s.coins, EconomyRules.Price(s.houseStage)) + " / " + EconomyRules.Price(s.houseStage);
        }
        public static string WorkshopMarker(ModelState s)
        {
            if (s.workshopBuilt == 1) return $"WORKSHOP READY\n+{EconomyRules.WorkshopBonus} COIN PER SPIN";
            if (s.houseStage == 0) return $"WORKSHOP · {EconomyRules.WorkshopCost}\nAFTER HOUSE REPAIR";
            return (s.selectedStage == EconomyRules.WorkshopTarget ? "IN PROGRESS" : "SELECT") + " · " + Math.Min(s.coins, EconomyRules.WorkshopCost) + $" / {EconomyRules.WorkshopCost}\nWORKSHOP: +{EconomyRules.WorkshopBonus} PER SPIN";
        }
    }
}
