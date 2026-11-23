using System;
using System.Collections.Generic;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Effects;
using JunkToNeon.Guidance;
using JunkToNeon.Orders;
using JunkToNeon.Upgrades;

namespace JunkToNeon.SaveInspector
{
    // Explains an already validated ModelState through production projections only.
    // Sources: "stored" = present in the snapshot; "projected" = production's in-memory
    // projection of an absent aggregate (never persisted by inspection); "derived" = computed
    // here from stored/projected facts.
    public static class Facts
    {
        public const string Stored = "stored", Projected = "projected-not-persisted", Derived = "derived";
        public static readonly string[] Groups = { "format", "wallet", "progression", "milestones", "optionalAchievements", "selection", "skills", "orders", "guidance", "expansion", "arcade", "rngCarry", "pending", "clockSequence" };

        static readonly string[] GuideAckIds = { GuideIds.Welcome, GuideIds.ChooseProject, GuideIds.FirstRepair, GuideIds.Skills,
            GuideIds.EraReady, GuideIds.ExpandedR7, GuideIds.ChapterEnd, GuideIds.Orders };

        public static string Layer(ModelState s)
        {
            if (ExpansionEconomy.IsModern(s))
            {
                int version=ExpansionEconomy.Project(s).FormatVersion;
                return version>=3?"expansion-r15":version==2?"expansion-r14":"expansion-r13";
            }
            if (JourneyEconomy.IsModern(s)) return "journey-r9";
            if (ChapterEconomy.IsModern(s)) return "chapter-r8";
            if (s.wave != null || s.pending?.wave != null || s.lastSpin?.wave != null) return "legacy-wave-r7";
            return "legacy-pre-wave";
        }

        public static Obj Extract(ModelState s)
        {
            var chapter = ChapterEconomy.Project(s);
            var journey = JourneyEconomy.Project(s);
            var root = new Obj();
            root.Add("format", Format(s));
            root.Add("wallet", new Obj().Add("source", Stored).Add("coins", s.coins));
            root.Add("progression", Progression(s, chapter));
            root.Add("milestones", Milestones(chapter));
            root.Add("optionalAchievements", OptionalFacts(chapter, journey));
            root.Add("selection", Selection(s, chapter));
            root.Add("skills", Skills(s, chapter));
            root.Add("orders", Orders(s, chapter, journey));
            root.Add("guidance", Guidance(s, journey));
            root.Add("expansion", Expansion(s));
            root.Add("arcade", Arcade(s));
            root.Add("rngCarry", RngCarry(s));
            root.Add("pending", Pending(s));
            root.Add("clockSequence", Clock(s));
            return root;
        }

        static Obj Expansion(ModelState s)
        {
            var x=ExpansionEconomy.Project(s);
            var modules=new List<object>();
            foreach(var id in new[]{"module.salvage","module.packing","module.harvest","module.combo","module.dynamo"})
                modules.Add(new Obj().Add("id",id).Add("owned",x.OwnsModule(id)));
            var sockets=new List<object>();
            for(int slot=1;slot<=x.SocketCount;slot++)sockets.Add(new Obj().Add("slot",slot).Add("module",x.ModuleAt(slot)));
            var equipment=new List<object>();
            foreach(string family in FamilyIds.All)equipment.Add(new Obj().Add("family",family).Add("level",x.EquipmentLevel(family)));
            var themes=new List<object>();
            foreach(string id in new[]{"theme.neon","theme.amber","theme.mint","theme.violet","theme.frost","theme.rose"})
                themes.Add(new Obj().Add("id",id).Add("owned",x.OwnsTheme(id)));
            var r=x.LastResourceReceipt;var command=x.LastCommand;
            return new Obj().Add("source",s.expansion==null?Projected:Stored).Add("adoptedSequence",x.AdoptedSequence)
                .Add("payloadFormatVersion",x.FormatVersion).Add("customizationSource",x.FormatVersion>=2?Stored:Projected)
                .Add("bonusWorkbenchLevel",x.BonusWorkbenchLevel).Add("bonusFocus",x.BonusFocusId)
                .Add("stateSha256",InputGuard.HashText(ExpansionCodec.EncodeState(x)))
                .Add("parts",x.Parts).Add("partsCap",x.PartsCap).Add("potatoes",x.Potatoes).Add("potatoesCap",x.PotatoesCap)
                .Add("energy",x.Energy).Add("energyCap",x.EnergyCap).Add("baseSpinEnergyCost",0)
                .Add("socketCount",x.SocketCount).Add("modules",modules).Add("sockets",sockets).Add("equipment",equipment)
                .Add("potatoYieldLevel",x.PotatoYieldLevel).Add("themes",themes).Add("selectedTheme",x.ThemeId).Add("presentationSpeed",x.SpeedId)
                .Add("bonusUnlocked",x.BonusUnlocked).Add("bonusProgress",x.BonusProgress).Add("bonusCadence",x.BonusCadence)
                .Add("bonusRolls",x.BonusRolls).Add("bonusSeed",x.BonusSeed).Add("boostArmed",x.BoostArmed).Add("foregroundSpins",x.ForegroundSpins)
                .Add("lastResourceReceipt",r==null?null:new Obj().Add("status","retained-current-save-already-credited")
                    .Add("sequence",r.Sequence).Add("parts",r.Parts).Add("potatoes",r.Potatoes).Add("energy",r.Energy)
                    .Add("bonusSymbol",r.BonusSymbol).Add("bonusParts",r.BonusParts).Add("bonusPotatoes",r.BonusPotatoes).Add("boosted",r.Boosted))
                .Add("lastCommand",command==null?null:new Obj().Add("status","retained-historical-command")
                    .Add("sequence",command.Sequence).Add("action",command.Action).Add("id",command.Id).Add("slot",command.Slot).Add("key",command.Key));
        }

        static Obj Arcade(ModelState s)
        {
            // Only the public masked projection is exposed: never the challenge
            // seed, full shuffled board, transcript or future Sorting items.
            var a=ArcadeEconomy.Project(s);var run=a.ActiveRun;var claim=a.LastClaim;
            var faces=new List<object>();if(run!=null)foreach(int face in run.MemoryFaces)faces.Add(face);
            return new Obj().Add("source",a.IsAdopted?Stored:Projected).Add("adoptedSequence",a.AdoptedSequence)
                .Add("foregroundSpins",a.ForegroundSpins).Add("diamonds",a.Diamonds)
                .Add("tickets",a.Tickets).Add("ticketCap",a.TicketCap).Add("ticketProgress",a.TicketProgress).Add("ticketCadence",a.TicketCadence)
                .Add("slotLevel",a.SlotLevel).Add("boxPresencePartBonus",a.BoxPresencePartBonus).Add("potatoPresencePotatoBonus",a.PotatoPresencePotatoBonus)
                .Add("rawBonusPartsAddition",a.RawBonusPartsAddition).Add("rawBonusPotatoesAddition",a.RawBonusPotatoesAddition)
                .Add("activeRun",run==null?null:new Obj().Add("status",run.Ready?"ready-unpaid":"active-saved").Add("gameId",run.Id)
                    .Add("instanceSequence",run.InstanceSequence).Add("phase",run.Phase).Add("cursor",run.Cursor).Add("attempts",run.Attempts)
                    .Add("correct",run.Correct).Add("matchedMask",run.MatchedMask).Add("matchedPairs",run.MatchedPairs)
                    .Add("memoryFaces",faces).Add("currentSortingSymbol",run.CurrentSortingSymbol).Add("readyReward",run.Ready?(object)run.ReadyReward:null))
                .Add("latestClaim",claim==null?null:new Obj().Add("status","retained-historical-already-credited").Add("sequence",claim.Sequence)
                    .Add("instanceSequence",claim.InstanceSequence).Add("gameId",claim.GameId).Add("diamonds",claim.Diamonds).Add("key",claim.Key))
                .Add("lastResourceReceipt",ArcadeEventFacts(a.LastResourceReceipt)).Add("lastEvent",ArcadeEventFacts(a.LastEvent));
        }
        static Obj ArcadeEventFacts(ArcadeEvent e)
        {
            return e==null?null:new Obj().Add("status","retained-current-save-already-credited").Add("sequence",e.Sequence).Add("kind",e.Kind)
                .Add("instanceSequence",e.InstanceSequence).Add("parts",e.PartsAdded).Add("potatoes",e.PotatoesAdded).Add("energy",e.EnergyAdded)
                .Add("tickets",e.TicketsAdded).Add("diamonds",e.DiamondsAdded).Add("grossCoinIncome",e.CoinIncome).Add("constructionDebit",e.ConstructionDebit)
                .Add("slotPartsIncluded",e.SlotPartsIncluded).Add("slotPotatoesIncluded",e.SlotPotatoesIncluded).Add("slotComponents","included-in-total-credits");
        }
        internal static string ExpansionWalletAuthority(string kind)
        {return kind=="arcade"?"dedicated-frozen-arcade-command":kind=="expansion"?"dedicated-frozen-expansion-command":"lower-frozen-command";}

        static Obj Milestones(ChapterState chapter)
        {
            var snapshot = ChapterMilestones.Project(chapter);
            var entries = new List<object>();
            foreach (var milestone in snapshot.Milestones)
                entries.Add(new Obj().Add("id", milestone.Definition.Id).Add("reached", milestone.Reached));
            return new Obj().Add("source", Derived).Add("scope", "current-save-facts")
                .Add("constructionCount", snapshot.ConstructionCount).Add("totalConstructions", snapshot.TotalConstructions)
                .Add("reachedCount", snapshot.ReachedCount).Add("milestones", entries)
                .Add("nextAction", snapshot.NextAction.ToString()).Add("selectedProjectId", snapshot.SelectedProjectId);
        }

        static Obj OptionalFacts(ChapterState chapter, JourneyState journey)
        {
            var snapshot = OptionalAchievements.Project(chapter, journey);
            var entries = new List<object>();
            foreach (var achievement in snapshot.Achievements)
                entries.Add(new Obj().Add("id", achievement.Definition.Id).Add("reached", achievement.Reached));
            return new Obj().Add("source", Derived).Add("scope", "current-save-facts").Add("achievements", entries);
        }

        static Obj Format(ModelState s)
        {
            return new Obj().Add("schemaVersion", s.schemaVersion).Add("contentVersion", s.contentVersion).Add("layer", Layer(s))
                .Add("waveStored", s.wave != null).Add("chapterStored", s.chapter != null).Add("journeyStored", s.journey != null).Add("expansionStored", s.expansion != null);
        }

        static List<object> Families(FamilyStages stages)
        {
            var list = new List<object>();
            foreach (string family in FamilyIds.All)
                list.Add(new Obj().Add("family", family).Add("absolute", stages.AbsoluteLevel(family)).Add("local", stages.LocalLevel(family)));
            return list;
        }

        static Obj Progression(ModelState s, ChapterState chapter)
        {
            var rules = ChapterCatalog.Rules;
            var stages = rules.Stages(chapter.Progress);
            var wave = WaveProgress.Of(s);
            var o = new Obj().Add("source", s.chapter != null ? Stored : Projected)
                .Add("ruleRevision", chapter.RuleRevision).Add("era", stages.EraId)
                .Add("families", Families(stages))
                .Add("completedProjects", chapter.Progress.CompletedUpgradeIds.Count)
                .Add("eraEntryAvailable", rules.CanUnlockWorkshopYard(chapter.Progress))
                .Add("eraEntrySequence", chapter.EntrySequence).Add("eraTransitionSeen", chapter.TransitionSeen)
                .Add("eraTransitionPending", chapter.TransitionPending)
                .Add("completeUnderChapterRule", rules.IsChapterComplete(chapter.Progress));
            // The r7 predicate is reported separately and labelled; it is never shown as chapter completion.
            o.Add("legacyFields", new Obj().Add("source", s.chapter != null ? Derived : Stored)
                .Add("houseStage", s.houseStage).Add("workshopBuilt", s.workshopBuilt)
                .Add("recyclerBuilt", wave.RecyclerBuilt).Add("pigeonLoftBuilt", wave.PigeonLoftBuilt)
                .Add("completeUnderR7Rule", ProgressionCatalog.Complete(s.houseStage, s.workshopBuilt, wave.RecyclerBuilt, wave.PigeonLoftBuilt)));
            return o;
        }

        static Obj Selection(ModelState s, ChapterState chapter)
        {
            var o = new Obj().Add("source", s.chapter != null ? Stored : Projected).Add("selectedProjectId", chapter.SelectedProjectId);
            o.Add("constructionPriceRevision", chapter.ConstructionPriceRevision)
                .Add("constructionPriceRevisionSource", s.chapter == null ? Projected : chapter.ConstructionPriceRevision == ConstructionPricing.LegacyRevision ? "stored-aggregate-implicit-legacy-default" : Stored)
                .Add("currentOfferRevision", ConstructionPricing.CurrentRevision);
            if (chapter.SelectedProjectId != null)
            {
                long price = ChapterEconomy.SelectedProjectPrice(chapter);
                o.Add("selectedPrice", price).Add("affordableNow", s.coins >= price)
                    .Add("currentReselectionOfferPrice", ChapterEconomy.ProjectPrice(chapter.SelectedProjectId));
            }
            else o.Add("selectedPrice", null).Add("affordableNow", null);
            o.Add("legacySelectedStage", s.selectedStage);
            return o;
        }

        static Obj Skills(ModelState s, ChapterState chapter)
        {
            var owned = chapter.Skills;
            var receipts = new List<object>();
            foreach (var r in owned.Receipts)
            {
                var req = r.Request;
                receipts.Add(new Obj().Add("node", req.NodeId).Add("legacyReceipt", r.Legacy).Add("priceRevision", r.PricingRevision)
                    .Add("frozenPrice", r.FrozenPrice).Add("sequence", req.Sequence).Add("operationId", req.OperationId)
                    .Add("expectedWallet", req.ExpectedWallet).Add("expectedOwnershipSha256", Short(req.ExpectedOwnership)));
            }
            return new Obj().Add("source", s.chapter != null ? Stored : Projected).Add("ownershipCodecVersion", owned.Version)
                .Add("receiptCount", owned.Receipts.Count).Add("ownershipTokenSha256", Short(owned.Token)).Add("receipts", receipts);
        }

        static Obj Orders(ModelState s, ChapterState chapter, JourneyState journey)
        {
            var orders = journey.Orders;
            var context = new OrderContext(ChapterCatalog.Rules.Stages(chapter.Progress));
            var o = new Obj().Add("source", s.journey != null ? Stored : Projected).Add("unlocked", context.Unlocked);
            if (orders.ActiveOrderId == null) o.Add("active", null);
            else
            {
                var def = OrderCatalog.Get(orders.ActiveOrderId);
                string metric=def.Metric==OrderMetric.TrashAppearances?"trash-appearances":def.Metric==OrderMetric.CompletedDeliveries?"completed-deliveries":
                    def.Metric==OrderMetric.PotatoAppearances?"potato-appearances":def.Metric==OrderMetric.BoxAppearances?"box-appearances":throw new InvalidOperationException("Unknown order metric.");
                o.Add("active", new Obj().Add("orderId", def.Id).Add("metric", metric)
                    .Add("instanceSequence", orders.InstanceSequence).Add("progress", orders.Progress).Add("target", def.Target)
                    .Add("ready", orders.Ready).Add("rewardOnExplicitClaim", def.RewardCoins).Add("claimed", false));
            }
            o.Add("cursor", orders.Cursor).Add("lastCommandSequence", orders.LastCommandSequence)
                .Add("lastEventSequence", orders.LastEvent == null ? (object)null : orders.LastEvent.OperationSequence);
            var claim = orders.LatestClaim;
            o.Add("latestClaim", claim == null ? null : new Obj().Add("status", "retained-historical-already-credited")
                .Add("orderId", claim.OrderId).Add("instanceSequence", claim.InstanceSequence).Add("claimSequence", claim.ClaimSequence).Add("rewardCoins", claim.RewardCoins));
            // A projected adoption sequence is only "next write"; report it when actually stored.
            o.Add("journeyMarker", new Obj().Add("adoptedSequence", s.journey != null ? (object)journey.AdoptedSequence : null).Add("commandSequence", journey.CommandSequence)
                .Add("commandKind", journey.CommandKind).Add("commandId", journey.CommandId).Add("commandInstance", journey.CommandInstance)
                .Add("commandCompletion", journey.CommandCompletion));
            return o;
        }

        static Obj Guidance(ModelState s, JourneyState journey)
        {
            var g = journey.Guidance;
            var acks = new List<object>();
            foreach (string id in GuideAckIds) if (g.IsAcknowledged(id)) acks.Add(id);
            var steps = new List<object>();
            foreach (TutorialSteps step in new[] { TutorialSteps.Previewed, TutorialSteps.Selected, TutorialSteps.ManualSpin, TutorialSteps.VisibleImprovement, TutorialSteps.AutoInformed })
                if ((g.TutorialBits & (int)step) != 0) steps.Add(step.ToString());
            return new Obj().Add("source", s.journey != null ? Stored : Projected)
                .Add("origin", g.Origin.ToString())
                .Add("acknowledged", acks).Add("tutorialSteps", steps)
                .Add("lastObservedSequence", g.LastObservedSequence)
                .Add("latestCompletedProject", g.LastCompletion == null ? null : new Obj().Add("projectId", g.LastCompletion.ProjectId).Add("sequence", g.LastCompletion.Sequence))
                .Add("completionAcknowledgedSequence", g.CompletionAcknowledgedSequence)
                .Add("lastAcknowledgement", g.LastAckSequence == 0 ? null : new Obj().Add("sequence", g.LastAckSequence).Add("hintId", g.LastAckHintId))
                .Add("autoHintSeen", s.autoHintSeen);
        }

        static List<object> SymbolNames(int[] symbols)
        {
            if (symbols == null) return null;
            var list = new List<object>();
            foreach (int symbol in symbols) list.Add(symbol >= 0 && symbol < EconomyRules.SymbolNames.Length ? EconomyRules.SymbolNames[symbol] : "unknown");
            return list;
        }

        static Obj Delivery(DeliveryState d)
        {
            return new Obj().Add("carry", d.PigeonProgress).Add("lastOperationSequence", d.LastOperationSequence);
        }

        static Obj RngCarry(ModelState s)
        {
            var wave = WaveProgress.Of(s);
            var o = new Obj().Add("seed", s.seed)
                .Add("deliveryCarry", wave.Delivery.PigeonProgress)
                .Add("deliveryCarrySource", s.wave != null ? Stored : "default-zero-absent-field")
                .Add("deliveryLastOperationSequence", wave.Delivery.LastOperationSequence);
            o.Add("lastSpin", s.lastSpin == null ? null : SpinReceiptFacts(s.lastSpin));
            return o;
        }

        static Obj SpinReceiptFacts(SpinReceipt r)
        {
            var o = new Obj().Add("status", "retained-historical-receipt").Add("sequence", r.sequence).Add("symbols", SymbolNames(r.symbols))
                .Add("baseReward", r.reward).Add("workshopBonus", r.bonus);
            if (r.chapter != null) o.Add("components", ChapterSpinFacts(r.chapter));
            else if (r.wave != null) o.Add("components", WaveFacts(r.wave));
            else o.Add("components", new Obj().Add("descriptor", "legacy-reward-and-bonus-only"));
            if (r.journey != null)
                o.Add("journey", new Obj().Add("manualSpin", r.journey.ManualSpin).Add("orderObserved", r.journey.OrderProposalKey.Length != 0)
                    .Add("orderReward", r.journey.Reward));
            return o;
        }

        static string CompletedProject(ChapterOperation op)
        {
            var a = ChapterCatalog.Rules.Stages(op.Before.Progress); var b = ChapterCatalog.Rules.Stages(op.After.Progress);
            foreach (string family in FamilyIds.All)
                if (b.AbsoluteLevel(family) > a.AbsoluteLevel(family)) return ChapterIds.Project(a.EraId, family, b.LocalLevel(family));
            return null;
        }

        static Obj ChapterSpinFacts(ChapterOperation op)
        {
            var e = op.Effects;
            return new Obj().Add("descriptor", "chapter-format2").Add("at", op.At).Add("coinsBefore", op.CoinsBefore).Add("coinsAfter", op.CoinsAfter)
                .Add("base", op.BaseCoins).Add("workshop", op.WorkshopCoins)
                .Add("baselineRecycler", e.Baseline.RecyclerCoins).Add("baselineDelivery", e.Baseline.DeliveryCoins).Add("deliveries", e.Baseline.Deliveries)
                .Add("universalSkill", e.UniversalCoins).Add("recyclingSkill", e.RecyclingSkillCoins).Add("deliverySkill", e.DeliverySkillCoins)
                .Add("potatoSources", e.PotatoCoins).Add("boxSources", e.BoxCoins).Add("trashSources", e.TrashCoins).Add("pigeonSources", e.PigeonCoins)
                .Add("carryBefore", e.Before.PigeonProgress).Add("carryAfter", e.After.PigeonProgress)
                .Add("selectedAtSpin", op.ActionId).Add("selectedPrice", op.Price)
                .Add("constructionPriceRevisionBefore", op.Before.ConstructionPriceRevision)
                .Add("constructionPriceRevisionAfter", op.After.ConstructionPriceRevision).Add("completedProject", CompletedProject(op));
        }

        static Obj WaveFacts(WaveOperation w)
        {
            var o = new Obj().Add("descriptor", "wave-format1").Add("target", w.Target).Add("price", w.Price).Add("base", w.BaseCoins)
                .Add("workshop", w.WorkshopCoins).Add("slot", w.SlotCoins);
            if (w.Effects != null)
                o.Add("recycler", w.Effects.RecyclerCoins).Add("delivery", w.Effects.DeliveryCoins).Add("deliveries", w.Effects.Deliveries)
                    .Add("carryBefore", w.Effects.Before.PigeonProgress).Add("carryAfter", w.Effects.After.PigeonProgress);
            if (w.Purchase != null) o.Add("purchasedSlotLevel", w.Purchase.Level);
            return o;
        }

        static Obj Pending(ModelState s)
        {
            var p = s.pending;
            if (p == null) return new Obj().Add("status", "absent");
            var descriptors = new List<object>();
            if (p.wave != null) descriptors.Add("wave");
            if (p.chapter != null) descriptors.Add("chapter");
            if (p.journey != null) descriptors.Add("journey");
            if (p.expansion != null) descriptors.Add("expansion");
            var o = new Obj().Add("status", "present-validated-by-production").Add("kind", p.kind).Add("sequence", p.sequence).Add("at", p.at)
                .Add("target", p.target).Add("symbols", SymbolNames(p.symbols)).Add("seedAfter", p.symbols == null ? (object)null : p.seedAfter)
                .Add("descriptors", descriptors);
            if (p.chapter != null)
            {
                var op = p.chapter;
                var c = new Obj().Add("actionId", op.ActionId).Add("price", op.Price).Add("coinsBefore", op.CoinsBefore).Add("coinsAfter", op.CoinsAfter);
                c.Add("constructionPriceRevisionBefore", op.Before.ConstructionPriceRevision)
                    .Add("constructionPriceRevisionAfter", op.After.ConstructionPriceRevision);
                if (op.SkillRequest != null)
                    c.Add("skill", new Obj().Add("node", op.SkillRequest.NodeId).Add("priceRevision", op.SkillRequest.PricingRevision)
                        .Add("price", SkillPricing.Price(op.SkillRequest.NodeId, op.SkillRequest.PricingRevision)).Add("expectedWallet", op.SkillRequest.ExpectedWallet)
                        .Add("expectedOwnershipSha256", Short(op.SkillRequest.ExpectedOwnership)));
                if (op.Settlement != null)
                    c.Add("settlement", new Obj().Add("from", op.Settlement.from).Add("to", op.Settlement.to).Add("coins", op.Settlement.coins));
                if (op.Effects != null) c.Add("spin", ChapterSpinFacts(op));
                o.Add("frozenChapter", c);
            }
            if (p.journey != null)
            {
                var j = p.journey;
                o.Add("frozenJourney", new Obj().Add("kind", j.Kind).Add("composedWithChapter", j.Composed).Add("actionId", j.ActionId)
                    .Add("instanceSequence", j.InstanceSequence).Add("completionSequence", j.CompletionSequence)
                    .Add("coinsBefore", j.CoinsBefore).Add("coinsAfter", j.CoinsAfter).Add("reward", j.Reward));
            }
            if(p.expansion!=null)
            {
                var x=p.expansion;
                o.Add("frozenExpansion",new Obj().Add("kind",x.Kind).Add("sequence",x.Sequence).Add("beforeWasAbsent",x.BeforeWasAbsent)
                    .Add("ruleRevision",x.RuleRevision).Add("beforeFormatVersion",x.Before.FormatVersion).Add("afterFormatVersion",x.After.FormatVersion)
                    .Add("bonusWorkbenchAfter",x.After.BonusWorkbenchLevel).Add("bonusFocusAfter",x.After.BonusFocusId)
                    .Add("walletAuthority",ExpansionWalletAuthority(x.Kind))
                    .Add("coinsBefore",x.Kind=="expansion"||x.Kind=="arcade"?(object)x.CoinsBefore:null).Add("coinsAfter",x.Kind=="expansion"||x.Kind=="arcade"?(object)x.CoinsAfter:null).Add("primarySeedBefore",x.SeedBefore).Add("primarySeedAfter",x.SeedAfter)
                    .Add("partsAfter",x.After.Parts).Add("potatoesAfter",x.After.Potatoes).Add("energyAfter",x.After.Energy)
                    .Add("boostArmedAfter",x.After.BoostArmed).Add("bonusRollsAfter",x.After.BonusRolls).Add("key",x.Key));
                var q=x.ArcadeQuote;
                if(q!=null)o.Add("frozenArcade",new Obj().Add("action",q.Action).Add("id",q.Id).Add("expectedSequence",q.ExpectedSequence)
                    .Add("instanceSequence",q.ExpectedInstance).Add("cursor",q.ExpectedCursor).Add("phase",q.ExpectedPhase).Add("target",q.Target)
                    .Add("partsCost",q.PartsCost).Add("potatoesCost",q.PotatoesCost).Add("diamondsCost",q.DiamondsCost).Add("ticketsCost",q.TicketsCost)
                    .Add("partsCredit",q.PartsCredit).Add("potatoesCredit",q.PotatoesCredit).Add("diamondsCredit",q.DiamondsCredit).Add("key",q.Key));
            }
            if (p.wave != null) o.Add("frozenWave", WaveFacts(p.wave));
            if (p.wave == null && p.chapter == null && p.journey == null && p.expansion == null)
                o.Add("frozenLegacy", "outcome-and-target-only; wallet effect is computed only by preview-pending replay");
            return o;
        }

        static Obj Clock(ModelState s)
        {
            var r = s.lastSettlement;
            return new Obj().Add("sequence", s.sequence).Add("revision", s.revision)
                .Add("lastSettledAt", s.lastSettledAt).Add("lastSettlementSeen", s.lastSettlementSeen)
                .Add("lastSettlement", r == null ? null : new Obj().Add("status", "retained-historical-receipt").Add("sequence", r.sequence)
                    .Add("from", r.from).Add("to", r.to).Add("seconds", r.seconds).Add("coins", r.coins).Add("workshop", r.workshop));
        }

        // Ownership tokens and expected-ownership strings are long validated identities; a
        // short SHA-256 prefix is enough to show equality without echoing them.
        public static string Short(string value)
        {
            if (value == null) return null;
            return InputGuard.HashText(value).Substring(0, 16);
        }
    }
}
