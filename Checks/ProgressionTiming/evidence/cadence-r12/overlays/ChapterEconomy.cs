using System;
using System.Linq;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Effects;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Core
{
    public static class ChapterEconomy
    {
        static void Need(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
        public static bool IsModern(ModelState s) { return s?.chapter != null || s?.pending?.chapter != null || s?.lastSpin?.chapter != null; }
        public static ChapterState Project(ModelState s)
        {
            if (s.chapter != null) return s.chapter;
            var legacy = ChapterCatalog.ProjectR7(s);
            return new ChapterState(2, ChapterContentIds.RuleRevision, legacy.Progress, SkillOwnership.Migrate(WaveProgress.Of(s).Slot), legacy.SelectedUpgradeId, 0, 0);
        }
        public static SkillContext Context(ChapterState c) { return new SkillContext(ChapterCatalog.Rules.Stages(c.Progress), c.SelectedProjectId); }
        public static bool SameContext(SkillContext a, SkillContext b)
        {
            if (a == null || b == null || a.EraId != b.EraId || a.SelectedConstruction != b.SelectedConstruction) return false;
            return FamilyIds.All.All(id => a.Stages.AbsoluteLevel(id) == b.Stages.AbsoluteLevel(id));
        }
        static bool SameProgress(ChapterProgress a, ChapterProgress b)
        {
            var x = ChapterCatalog.Rules.Stages(a); var y = ChapterCatalog.Rules.Stages(b);
            return x.EraId == y.EraId && FamilyIds.All.All(id => x.AbsoluteLevel(id) == y.AbsoluteLevel(id));
        }
        public static bool Same(ChapterState a, ChapterState b)
        {
            return a != null && b != null && a.RuleRevision == b.RuleRevision && a.ConstructionPriceRevision == b.ConstructionPriceRevision && SameProgress(a.Progress, b.Progress) &&
                a.Skills.Token == b.Skills.Token && a.SelectedProjectId == b.SelectedProjectId && a.EntrySequence == b.EntrySequence && a.TransitionSeen == b.TransitionSeen;
        }
        public static bool SameDelivery(DeliveryState a, DeliveryState b)
        { return a != null && b != null && a.LastOperationSequence == b.LastOperationSequence && a.PigeonProgress == b.PigeonProgress && a.Cadence == b.Cadence && a.LastProposalKey == b.LastProposalKey; }
        public static SkillQuote QuoteSkill(ModelState s, string node)
        {
            var c = Project(s);
            return SkillTree.Quote(node, "skillbuy." + (s.sequence + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), s.sequence + 1, s.coins, Context(c), c.Skills);
        }
        static ChapterState State(ChapterState old, ChapterProgress progress = null, SkillOwnership skills = null, string selected = null, long? entry = null, long? seen = null, string pricing = null)
        { return new ChapterState(2, old.RuleRevision, progress ?? old.Progress, skills ?? old.Skills, selected, entry ?? old.EntrySequence, seen ?? old.TransitionSeen, pricing ?? old.ConstructionPriceRevision); }
        static ChapterState BuyProject(ChapterState before, string id, string pricing = null)
        { return State(before, new ChapterProgress(before.Progress.EraId, before.Progress.CompletedUpgradeIds.Concat(new[] { id })), pricing: pricing); }
        // New selection offers use current prices; active goals retain their saved quote.
        public static long ProjectPrice(string id)
        { return ConstructionPricing.Price(id, ConstructionPricing.CurrentRevision); }
        public static long SelectedProjectPrice(ChapterState state)
        {
            if (state == null || state.SelectedProjectId == null) throw new InvalidOperationException("No selected construction project");
            return ConstructionPricing.Price(state.SelectedProjectId, state.ConstructionPriceRevision);
        }
        public static UpgradeOwnership LegacySlot(SkillOwnership skills)
        {
            var r = skills.Receipts.FirstOrDefault(x => x.Request.NodeId == SkillIds.Universal1);
            if (r == null) return UpgradeOwnership.Empty;
            int house = r.Legacy ? r.LegacyHouseStage : Math.Min(r.Request.Context.HouseStage, 2);
            return UpgradeOwnershipCodec.DecodeOrDefault("{\"version\":1,\"level\":1,\"revision\":1,\"sequence\":" + r.Request.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"walletBefore\":" + r.Request.ExpectedWallet.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"houseStage\":" + house + "}");
        }
        static void ProjectLegacy(ModelState s, ChapterState c, DeliveryState delivery)
        {
            var stages = ChapterCatalog.Rules.Stages(c.Progress);
            s.houseStage = Math.Min(stages.AbsoluteLevel(FamilyIds.House), 2);
            s.workshopBuilt = stages.AbsoluteLevel(FamilyIds.Workshop) >= 1 ? 1 : 0;
            s.selectedStage = ChapterCatalog.LegacyTarget(stages.EraId, c.SelectedProjectId);
            s.wave = new WaveProgress(stages.AbsoluteLevel(FamilyIds.Recycler) >= 1 ? 1 : 0,
                stages.AbsoluteLevel(FamilyIds.PigeonLoft) >= 1 ? 1 : 0, delivery, LegacySlot(c.Skills));
            s.chapter = c;
        }
        static bool SameRequest(SkillPurchaseRequest a, SkillPurchaseRequest b)
        {
            return a != null && b != null && a.NodeId == b.NodeId && a.OperationId == b.OperationId && a.Sequence == b.Sequence &&
                a.ExpectedWallet == b.ExpectedWallet && a.ExpectedOwnership == b.ExpectedOwnership && a.PricingRevision == b.PricingRevision && SameContext(a.Context, b.Context);
        }
        public static void ValidateFrozen(ChapterOperation op)
        {
            var before = op.Before; var rules = ChapterCatalog.Rules; var stages = rules.Stages(before.Progress);
            Need(before.EntrySequence < op.Sequence && before.Skills.Receipts.All(r => r.Request.Sequence < op.Sequence), "Future before-context receipt");
            Need(op.After.EntrySequence <= op.Sequence && op.After.Skills.Receipts.All(r => r.Request.Sequence <= op.Sequence), "Future after-context receipt");
            Need(!before.TransitionPending || (op.Kind != "spin" && op.Kind != "select" && op.Kind != "skill" && op.Kind != "enter-era"), "Transition acknowledgement required");
            ChapterState expected = before; long coins = op.CoinsBefore, price = 0; int baseCoins = 0, workshop = 0; uint seed = op.SeedBefore;
            if (op.Kind == "spin")
            {
                var symbols = op.Symbols; var draw = GameLogic.Draw(op.SeedBefore, op.Sequence);
                Need(symbols != null && symbols.SequenceEqual(draw.symbols) && op.SeedAfter == draw.seedAfter, "Frozen chapter RNG mismatch");
                Need(op.Effects != null && op.SkillRequest == null && op.Settlement == null && op.ActionId == before.SelectedProjectId, "Invalid spin descriptor");
                var e = op.Effects; baseCoins = GameLogic.Payout(symbols); workshop = stages.AbsoluteLevel(FamilyIds.Workshop) >= 1 ? 1 : 0;
                Need(e.OperationSequence == op.Sequence && e.RuleRevision == before.RuleRevision && e.Baseline.Outcome.Left == symbols[0] && e.Baseline.Outcome.Middle == symbols[1] && e.Baseline.Outcome.Right == symbols[2], "Frozen chapter outcome mismatch");
                Need(e.Baseline.Ownership.RecyclerLevel == (stages.AbsoluteLevel(FamilyIds.Recycler) >= 1 ? 1 : 0) && e.Baseline.Ownership.PigeonLoftLevel == (stages.AbsoluteLevel(FamilyIds.PigeonLoft) >= 1 ? 1 : 0), "Frozen pre-owned building mismatch");
                rules.ValidateSymbolModifiers(before.Progress, e.RuleRevision, e.Modifiers);
                var m = SkillTree.EvaluateModifiers(Context(before), before.Skills);
                Need(e.UniversalCoinsPerSpin == m.UniversalCoins && e.ExtraCoinsPerTrash == m.CoinsPerTrash && e.ExtraCoinsPerDelivery == m.CoinsPerDelivery, "Frozen skill modifier mismatch");
                coins = checked(checked(checked(coins + baseCoins) + workshop) + e.TotalCoins); seed = draw.seedAfter;
                if (before.SelectedProjectId != null)
                {
                    price = SelectedProjectPrice(before);
                    if (coins >= price) { coins -= price; expected = BuyProject(before, before.SelectedProjectId); }
                }
            }
            else
            {
                Need(op.Effects == null && op.Symbols == null, "Non-spin has effects or symbols");
                if (op.Kind == "select")
                {
                    Need(op.SkillRequest == null && op.Settlement == null && rules.AvailableUpgrades(before.Progress).Contains(op.ActionId), "Unknown/locked project selection");
                    string pricing = op.After.ConstructionPriceRevision;
                    Need(ConstructionPricing.CanAdvance(before.ConstructionPriceRevision, pricing), "Construction price revision regressed");
                    price = ConstructionPricing.Price(op.ActionId, pricing);
                    expected = coins >= price ? BuyProject(before, op.ActionId, pricing) : State(before, selected: op.ActionId, pricing: pricing);
                    if (coins >= price) coins -= price;
                }
                else if (op.Kind == "skill")
                {
                    var request = op.SkillRequest; var node = request == null ? null : SkillTree.Find(request.NodeId);
                    Need(node != null && request.NodeId == op.ActionId && request.Sequence == op.Sequence && request.ExpectedWallet == coins && request.ExpectedOwnership == before.Skills.Token && SameContext(request.Context, Context(before)), "Invalid frozen skill quote");
                    var receipts = op.After.Skills.Receipts;
                    Need(receipts.Count == before.Skills.Receipts.Count + 1 && !receipts[receipts.Count - 1].Legacy && SameRequest(receipts[receipts.Count - 1].Request, request), "Invalid frozen skill ownership transition");
                    price = SkillPricing.Price(request.NodeId,request.PricingRevision); Need(coins >= price && op.Settlement == null, "Invalid skill debit"); coins -= price;
                    expected = State(before, skills: op.After.Skills, selected: before.SelectedProjectId);
                }
                else if (op.Kind == "enter-era")
                {
                    Need(op.ActionId == ChapterIds.UnlockWorkshopYard && rules.CanUnlockWorkshopYard(before.Progress), "Era entry unavailable");
                    expected = State(before, new ChapterProgress(EraIds.WorkshopYard, before.Progress.CompletedUpgradeIds), entry: op.Sequence, seen: 0);
                }
                else if (op.Kind == "era-seen")
                {
                    Need(op.ActionId == ChapterIds.UnlockWorkshopYard && before.TransitionPending, "Era transition already acknowledged");
                    expected = State(before, selected: before.SelectedProjectId, seen: before.EntrySequence);
                }
                else if (op.Kind == "settle")
                {
                    var r = op.Settlement;
                    Need(op.ActionId == null && r != null && r.sequence == op.Sequence && r.to == op.At && r.to > r.from && r.from > 0 && r.workshop == (stages.AbsoluteLevel(FamilyIds.Workshop) >= 1 ? 1 : 0) &&
                        r.seconds == OfflineIncome.Seconds(r.from, r.to) && r.coins == OfflineIncome.Coins(r.from, r.to, r.workshop) && r.coins > 0, "Invalid frozen offline settlement");
                    coins = checked(coins + r.coins);
                }
                else Need((op.Kind == "clock" || op.Kind == "hint" || op.Kind == "seen") && op.ActionId == null && op.Settlement == null, "Unknown protocol descriptor");
                if (op.Kind != "skill") Need(op.SkillRequest == null, "Unexpected skill request");
                if (op.Kind != "settle") Need(op.Settlement == null, "Unexpected settlement receipt");
            }
            Need(op.Price == price && op.BaseCoins == baseCoins && op.WorkshopCoins == workshop && op.SeedAfter == seed && op.CoinsAfter == coins && Same(expected, op.After), "Frozen chapter delta mismatch");
        }
        public static void Validate(ModelState s)
        {
            if(JourneyEconomy.IsModern(s)){JourneyEconomy.Validate(s);return;}
            Need(s != null && s.chapter != null && s.wave != null && s.schemaVersion == 1 && s.contentVersion == 1 && s.coins >= 0 && s.seed != 0 && s.sequence >= 0 && s.sequence <= (long.MaxValue - 2) / 2, "Invalid modern save envelope");
            Need(s.revision == s.sequence * 2 + (s.pending == null ? 0 : 1) && s.autoHintSeen >= 0 && s.autoHintSeen <= 1 && (s.houseStage != 0 || s.autoHintSeen == 0), "Invalid modern revision/hint");
            var c = s.chapter; var stages = ChapterCatalog.Rules.Stages(c.Progress); var w = s.wave;
            Need(c.EntrySequence <= s.sequence && c.Skills.Receipts.All(r => r.Request.Sequence <= s.sequence), "Future chapter history");
            Need(s.houseStage == Math.Min(stages.AbsoluteLevel(FamilyIds.House), 2) && s.workshopBuilt == (stages.AbsoluteLevel(FamilyIds.Workshop) >= 1 ? 1 : 0) &&
                w.RecyclerBuilt == (stages.AbsoluteLevel(FamilyIds.Recycler) >= 1 ? 1 : 0) && w.PigeonLoftBuilt == (stages.AbsoluteLevel(FamilyIds.PigeonLoft) >= 1 ? 1 : 0) &&
                s.selectedStage == ChapterCatalog.LegacyTarget(c.Progress.EraId, c.SelectedProjectId) && UpgradeOwnershipCodec.Encode(w.Slot) == UpgradeOwnershipCodec.Encode(LegacySlot(c.Skills)), "Legacy/chapter projection conflict");
            Need(w.Delivery.LastOperationSequence <= s.sequence && s.lastSettledAt >= 0 && s.lastSettledAt <= OfflineIncome.MaxUtcSeconds && s.lastSettlementSeen >= 0 && s.lastSettlementSeen <= (s.lastSettlement?.sequence ?? 0), "Invalid delivery/clock marker");
            if (s.lastSettlement != null)
            {
                var r = s.lastSettlement;
                Need(r.sequence >= 1 && r.sequence <= s.sequence && r.to >= r.from && r.to > 0 && r.to <= s.lastSettledAt && r.workshop <= s.workshopBuilt && r.seconds == OfflineIncome.Seconds(r.from, r.to) && r.coins == OfflineIncome.Coins(r.from, r.to, r.workshop), "Invalid offline receipt");
            }
            if (s.lastSpin?.chapter != null)
            {
                var r = s.lastSpin; var op = r.chapter; ValidateFrozen(op);
                Need(op.Kind == "spin" && r.wave == null && r.sequence == op.Sequence && r.sequence <= s.sequence && r.reward == op.BaseCoins && r.bonus == op.WorkshopCoins && r.symbols.SequenceEqual(op.Symbols) && s.seed == op.SeedAfter && SameDelivery(w.Delivery, op.Effects.After), "Invalid modern spin receipt/checkpoint");
                var oldStages = ChapterCatalog.Rules.Stages(op.After.Progress);
                Need(ConstructionPricing.CanAdvance(op.After.ConstructionPriceRevision, c.ConstructionPriceRevision) &&
                    FamilyIds.All.All(id => stages.AbsoluteLevel(id) >= oldStages.AbsoluteLevel(id)) && op.After.Skills.Receipts.All(rp => c.Skills.Receipts.Any(now => SameRequest(now.Request, rp.Request) || now.Legacy && rp.Legacy && now.Request.Sequence == rp.Request.Sequence && now.Request.ExpectedWallet == rp.Request.ExpectedWallet && now.LegacyHouseStage == rp.LegacyHouseStage)), "Chapter history regressed after spin");
            }
            else
            {
                // Literal r6/r7 receipt validation remains intact until a new spin replaces it.
                var legacy = s.Copy(); legacy.chapter = null; legacy.pending = null; legacy.revision = legacy.sequence * 2;
                GameLogic.Validate(legacy);
            }
            if (s.pending != null)
            {
                var p = s.pending; var op = p.chapter;
                Need(op != null && p.wave == null && p.target == 0 && p.sequence == s.sequence + 1 && op.Sequence == p.sequence && op.Kind == p.kind && op.At == p.at && op.CoinsBefore == s.coins && op.SeedBefore == s.seed && Same(c, op.Before), "Missing/conflicting modern operation descriptor");
                ValidateFrozen(op);
                Need(op.Kind == "spin" ? p.seedAfter == op.SeedAfter && p.symbols != null && p.symbols.SequenceEqual(op.Symbols) && SameDelivery(w.Delivery, op.Effects.Before) : p.seedAfter == 0 && p.symbols == null, "Pending outcome/checkpoint mismatch");
                if (p.kind == "clock" || p.kind == "settle") Need(p.at > s.lastSettledAt, "Clock did not advance");
                if (p.kind == "settle") Need(op.Settlement.from == s.lastSettledAt, "Settlement cursor changed");
                if (p.kind == "hint") Need(s.houseStage >= 1 && s.autoHintSeen == 0, "Hint already acknowledged");
                if (p.kind == "seen") Need(s.lastSettlement != null && s.lastSettlement.coins > 0 && s.lastSettlementSeen != s.lastSettlement.sequence, "Offline receipt already acknowledged");
            }
        }
        public static ModelState Reserve(ModelState s, string kind, string actionId, long sequence, long at = 0, SkillPurchaseRequest skillRequest = null)
        {
            if(JourneyEconomy.IsModern(s))return JourneyEconomy.ReserveChapter(s,kind,actionId,sequence,at,skillRequest);
            GameLogic.Validate(s); Need(s.pending == null, "Pending recovery required"); var before = Project(s);
            SkillPurchaseProposal skill = null;
            if (kind == "skill")
            {
                var result = SkillTree.Propose(s.sequence, s.coins, Context(before), before.Skills, skillRequest);
                if (result.Status == PurchaseStatus.Replay) return s;
                Need(result.Status == PurchaseStatus.Proposed && result.Proposal.MatchesPreconditions(s.sequence, s.coins, Context(before), before.Skills), "Skill quote unavailable or stale");
                skill = result.Proposal; actionId = skillRequest.NodeId;
            }
            else if (sequence <= s.sequence && sequence >= 1) return s;
            Need(sequence == s.sequence + 1 && sequence <= (long.MaxValue - 2) / 2, "Chapter sequence gap/exhaustion");
            Need(!before.TransitionPending || (kind != "spin" && kind != "select" && kind != "skill" && kind != "enter-era"), "Acknowledge era transition first");
            var after = before; long coins = s.coins, price = 0; int baseCoins = 0, workshop = 0; uint seedAfter = s.seed; int[] symbols = null;
            FrozenSpinEffectsV2 effects = null; OfflineReceipt settlement = null;
            if (kind == "spin")
            {
                var draw = GameLogic.Draw(s.seed, sequence); symbols = draw.symbols; seedAfter = draw.seedAfter;
                var w = WaveProgress.Of(s); var m = SkillTree.EvaluateModifiers(Context(before), before.Skills);
                effects = ChapterSpinEffects.Prepare(new SpinOutcome(symbols[0], symbols[1], symbols[2]), new EffectOwnership(w.RecyclerBuilt, w.PigeonLoftBuilt), w.Delivery, sequence,
                    EffectRules.Provisional, before.RuleRevision, ChapterCatalog.Rules.SymbolModifiers(before.Progress), m.UniversalCoins, m.CoinsPerTrash, m.CoinsPerDelivery);
                baseCoins = GameLogic.Payout(symbols); workshop = s.workshopBuilt; actionId = before.SelectedProjectId;
                coins = checked(checked(checked(coins + baseCoins) + workshop) + effects.TotalCoins);
                if (actionId != null) { price = SelectedProjectPrice(before); if (coins >= price) { coins -= price; after = BuyProject(before, actionId); } }
            }
            else if (kind == "select")
            {
                Need(ChapterCatalog.Rules.AvailableUpgrades(before.Progress).Contains(actionId), "Project unavailable"); price = ProjectPrice(actionId);
                if (coins >= price) { coins -= price; after = BuyProject(before, actionId, ConstructionPricing.CurrentRevision); }
                else after = State(before, selected: actionId, pricing: ConstructionPricing.CurrentRevision);
            }
            else if (kind == "skill") { price = skill.FrozenPrice; coins = skill.WalletAfter; after = State(before, skills: skill.OwnershipAfter, selected: before.SelectedProjectId); }
            else if (kind == "enter-era")
            {
                Need(ChapterCatalog.Rules.CanUnlockWorkshopYard(before.Progress), "Era unavailable/already entered"); actionId = ChapterIds.UnlockWorkshopYard;
                after = State(before, new ChapterProgress(EraIds.WorkshopYard, before.Progress.CompletedUpgradeIds), entry: sequence, seen: 0);
            }
            else if (kind == "era-seen")
            {
                if (!before.TransitionPending) return s;
                actionId = ChapterIds.UnlockWorkshopYard; after = State(before, selected: before.SelectedProjectId, seen: before.EntrySequence);
            }
            else if (kind == "settle")
            {
                settlement = new OfflineReceipt { sequence = sequence, from = s.lastSettledAt, to = at, workshop = s.workshopBuilt, seconds = OfflineIncome.Seconds(s.lastSettledAt, at), coins = OfflineIncome.Coins(s.lastSettledAt, at, s.workshopBuilt) };
                coins = checked(coins + settlement.coins);
            }
            var op = new ChapterOperation(2, kind, sequence, at, actionId, price, s.coins, coins, s.seed, seedAfter, symbols, baseCoins, workshop, before, after, effects, skillRequest, settlement);
            ValidateFrozen(op);
            var next = s.Copy(); ProjectLegacy(next, before, WaveProgress.Of(s).Delivery);
            next.pending = new PendingOperation { kind = kind, sequence = sequence, at = at, seedAfter = kind == "spin" ? seedAfter : 0, symbols = symbols, chapter = op };
            next.revision = checked(s.revision + 1); Validate(next); return next;
        }
        public static ModelState ReserveLegacy(ModelState s, string kind, int target, long sequence, long at)
        {
            if (kind == "select") { Need(Project(s).Progress.EraId == EraIds.Junkyard, "Legacy target does not name a Workshop Yard project"); return Reserve(s, kind, ChapterCatalog.LegacyProject(target), sequence, at); }
            if (kind == "upgrade")
            {
                if (sequence >= 1 && sequence <= s.sequence) return s;
                return Reserve(s, "skill", SkillIds.Universal1, sequence, at, QuoteSkill(s, SkillIds.Universal1).Request);
            }
            return Reserve(s, kind, null, sequence, at);
        }
        public static ModelState ApplyPending(ModelState s)
        {
            if(JourneyEconomy.IsModern(s))return JourneyEconomy.ApplyPending(s);
            Validate(s); var p = s.pending; if (p == null) return s;
            var op = p.chapter; var next = s.Copy(); var delivery = WaveProgress.Of(s).Delivery;
            if (op.Kind == "spin")
            {
                var credit = op.Effects.Apply(delivery, s.coins, checked(op.BaseCoins + op.WorkshopCoins));
                Need(credit.Applied, "Pending spin was already applied");
                long debit = SameProgress(op.Before.Progress, op.After.Progress) ? 0 : op.Price;
                Need(checked(credit.Wallet - debit) == op.CoinsAfter, "Frozen credit/debit mismatch"); delivery = credit.Delivery;
                next.seed = op.SeedAfter; next.lastSpin = new SpinReceipt { sequence = p.sequence, symbols = op.Symbols, reward = op.BaseCoins, bonus = op.WorkshopCoins, chapter = op };
            }
            else if (op.Kind == "hint") next.autoHintSeen = 1;
            else if (op.Kind == "seen") next.lastSettlementSeen = s.lastSettlement.sequence;
            else if (op.Kind == "settle") next.lastSettlement = op.Settlement;
            next.coins = op.CoinsAfter; ProjectLegacy(next, op.After, delivery);
            if (p.at != 0) next.lastSettledAt = Math.Max(s.lastSettledAt, p.at);
            next.sequence = p.sequence; next.revision = checked(s.revision + 1); next.pending = null; Validate(next); return next;
        }
    }
}
