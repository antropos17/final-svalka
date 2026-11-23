#if EXPANSION_CANDIDATE
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Linq;
using JunkToNeon.Contracts;
using JunkToNeon.Core;

namespace ExpansionPacing
{
    // Compile only against a frozen03 packet implementing its published API.
    internal sealed class CandidateExpansionDriver : ExpansionDriver
    {
        static readonly string[] SalvagePlan = { "module.salvage", "bonus.unlock", "socket.2", "module.combo",
            "equipment.workshop.1", "equipment.recycler.1", "equipment.cart.1", "socket.3", "module.packing", "potato.yield.1" };
        static readonly string[] HarvestPlan = { "module.harvest", "potato.yield.1", "bonus.unlock", "socket.2", "module.dynamo",
            "potato.yield.2", "equipment.power-source.1", "socket.3", "module.combo", "equipment.power-source.2", "module.salvage" };
        readonly HashSet<string> purchased = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, int> firstAffordable = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, int> firstEffect = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, int> refusalReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly List<int> armedAt = new List<int>(), consumedAt = new List<int>();
        readonly List<object> pairedDiagnostics = new List<object>();
        sealed class EnergyPhase
        {
            public int Spins, BonusRolls, BoostedRolls;
            public long Nominal, Credited, Wasted;
        }
        readonly Dictionary<string, EnergyPhase> energyPhases = new Dictionary<string, EnergyPhase>(StringComparer.Ordinal);
        ExpansionState beforeSpin;
        int handledSpin = -1, checks, zeroEnergySpins, partsAtCap, potatoesAtCap, energyAtCap;
        long creditedParts, creditedPotatoes, creditedEnergy, lostEnergy;
        long spentParts, spentPotatoes, spentEnergy;
        long observedBoostCost = 10;
        sealed class Memory : ISnapshotStorage
        {
            public string Raw;
            public string ReadPrimary() => Raw;
            public string ReadBackup() => null;
            public void Write(string raw) { Raw = raw; }
        }
        public override string Label => "frozen-production-expansion";
        static bool Functional(string id) => !id.StartsWith("theme.", StringComparison.Ordinal) && !id.StartsWith("trade.", StringComparison.Ordinal);
        void Need(bool ok, string why) { checks++; if (!ok) throw new InvalidOperationException("Expansion pacing: " + why); }
        ExpansionQuote Offer(GameSession game, string id)
        {
            try { return game.GetExpansionOffer(id); }
            catch (InvalidOperationException error)
            { refusalReasons[error.Message] = refusalReasons.TryGetValue(error.Message, out int n) ? n + 1 : 1; return null; }
        }
        ExpansionQuote Boost(GameSession game)
        {
            try { return game.GetExpansionAction("boost", "boost.next-bonus"); }
            catch (InvalidOperationException) { return null; }
        }
        void Execute(GameSession game, RunResult result, ExpansionQuote quote, long at)
        {
            var before = game.Expansion; var state = game.State;
            var afterState = quote.Action == "buy" ? game.BuyExpansion(quote, at) : game.ExecuteExpansion(quote, at);
            var after = game.Expansion;
            Need(after.Parts == before.Parts - quote.PartsCost && after.Potatoes == before.Potatoes - quote.PotatoesCost &&
                after.Energy == before.Energy - quote.EnergyCost, "quoted resource debit differs: " + quote.Id);
            Need(afterState.coins == state.coins - quote.CoinsCost + quote.CoinsReward && afterState.seed == state.seed,
                "quoted coin/RNG result differs: " + quote.Id);
            Need(quote.CoinsCost == 0, "Current utility requires resource-only catalog costs");
            result.ExchangeIncome += quote.CoinsReward;
            spentParts += quote.PartsCost; spentPotatoes += quote.PotatoesCost; spentEnergy += quote.EnergyCost;
            if (quote.PotatoesCost > 0 && result.FirstPotatoSpendSpin < 0) result.FirstPotatoSpendSpin = result.Spins;
            result.ExpansionEvents.Add(new ExpansionEvent { Action = quote.Action, Id = quote.Id, Spin = result.Spins,
                PartsCost = quote.PartsCost, PotatoesCost = quote.PotatoesCost, EnergyCost = quote.EnergyCost,
                CoinsCost = quote.CoinsCost, CoinsReward = quote.CoinsReward });
            if (quote.Action == "boost") { result.BoostArms++; armedAt.Add(result.Spins); observedBoostCost = quote.EnergyCost; }
            if (quote.Action == "buy" && Functional(quote.Id))
            {
                purchased.Add(quote.Id);
                if (result.FirstUsefulShopPurchaseSpin < 0) result.FirstUsefulShopPurchaseSpin = result.Spins;
            }
            if (quote.Id == "potato.yield.1" && result.Policy == "skills-first") PotatoPayback(state, quote, at);
        }
        void PotatoPayback(ModelState before, ExpansionQuote quote, long at)
        {
            // One reachable earned purchase per seed. Both forks use the actual
            // production journal/codec and same base symbol stream, with no extra
            // shop/selection actions during this40-spin marginal comparison.
            var codec = new JsonStateCodec(); string raw = codec.Encode(before);
            GameSession Open() => GameSession.Open(new GameStore(new Memory { Raw = raw }, codec), before.seed);
            var control = Open(); var investment = Open();
            Need(!control.RecoveryRequired && !investment.RecoveryRequired, "Payback fork did not open");
            investment.BuyExpansion(quote, at);
            long controlStart = control.Expansion.Potatoes, investmentStart = investment.Expansion.Potatoes;
            int potatoPayback = -1, combinedPayback = -1; var rows = new List<object>();
            for (int i = 1; i <= 40; i++)
            {
                Program.AccountDiagnosticSpin(); control.ExecuteChapterSpin(control.State.sequence + 1, at + i * 4, manualSpin: true);
                Program.AccountDiagnosticSpin(); investment.ExecuteChapterSpin(investment.State.sequence + 1, at + i * 4, manualSpin: true);
                Need(control.State.seed == investment.State.seed && control.State.lastSpin.symbols.SequenceEqual(investment.State.lastSpin.symbols) &&
                    control.State.coins == investment.State.coins, "Payback comparison changed primary outcome/coins");
                long marginal = (investment.Expansion.Potatoes - investmentStart) - (control.Expansion.Potatoes - controlStart);
                if (marginal >= quote.PotatoesCost && potatoPayback < 0) potatoPayback = i;
                if (marginal >= quote.PartsCost + quote.PotatoesCost && combinedPayback < 0) combinedPayback = i;
                rows.Add(new { futureSpin = i, marginalPotatoes = marginal,
                    controlPotatoes = control.Expansion.Potatoes, investedPotatoes = investment.Expansion.Potatoes });
            }
            pairedDiagnostics.Add(new { kind = "reachable-potato-yield1-purchase-vs-defer", sourceStateSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))),
                quote.PartsCost, quote.PotatoesCost, futureSpinsPerFork = 40, potatoCostRecoverySpin = potatoPayback,
                combinedResourceUnitRecoverySpin = combinedPayback,
                boundary = "Actual earned initial state and production commands; potato cost payback is distinct from full Parts+Potatoes opportunity cost. Combined units use existing1:1 exchange value only as an analytical yardstick, not an executed conversion.", rows });
        }
        void Equip(GameSession game, RunResult result, string module, int slot, long at)
        {
            var before = game.Expansion;
            if (slot > before.SocketCount || !before.OwnsModule(module) || before.ModuleAt(slot) == module) return;
            var quote = game.GetExpansionAction("equip", module, slot);
            Execute(game, result, quote, at);
            Need(game.Expansion.ModuleAt(slot) == module, "Module was not equipped");
        }
        public override void Decisions(GameSession game, RunResult result, long at)
        {
            if (handledSpin == result.Spins) return;
            handledSpin = result.Spins;
            if (result.FirstUsefulShopAffordabilitySpin < 0)
            {
                foreach (var offer in ExpansionCatalog.Offers.Where(o => Functional(o.Id)))
                {
                    var quote = Offer(game, offer.Id);
                    if (quote == null) continue;
                    firstAffordable[offer.Id] = result.Spins;
                    result.FirstUsefulShopAffordabilitySpin = result.Spins;
                    break;
                }
            }
            var boost = Boost(game);
            if (boost != null && result.FirstBoostAffordabilitySpin < 0) result.FirstBoostAffordabilitySpin = result.Spins;
            // House-first is the deliberate hold-resources control. Its base
            // construction path must remain exact while optional caps can fill.
            if (result.Policy == "house-first") return;
            string[] plan = result.Policy == "skills-first" ? HarvestPlan : SalvagePlan;
            int buys = 0;
            foreach (string id in plan)
            {
                if (purchased.Contains(id)) continue;
                var quote = Offer(game, id);
                // Save for the next chosen specialization item. Skip only
                // equipment whose required family is still unowned.
                if (quote == null)
                {
                    if (id.StartsWith("equipment.", StringComparison.Ordinal)) continue;
                    break;
                }
                Execute(game, result, quote, at);
                if (++buys == 4) break;
            }
            if (result.Policy == "skills-first")
            {
                Equip(game, result, "module.harvest", 1, at);
                bool sustainableWithoutDynamo = (1 + game.Expansion.EquipmentLevel(FamilyIds.PowerSource)) * game.Expansion.BonusCadence >= observedBoostCost;
                Equip(game, result, sustainableWithoutDynamo && purchased.Contains("module.salvage") ? "module.salvage" : "module.dynamo", 2, at);
                Equip(game, result, "module.combo", 3, at);
            }
            else
            { Equip(game, result, "module.salvage", 1, at); Equip(game, result, "module.combo", 2, at); Equip(game, result, "module.packing", 3, at); }
            boost = Boost(game);
            if (boost != null)
            {
                if (result.FirstBoostAffordabilitySpin < 0) result.FirstBoostAffordabilitySpin = result.Spins;
                Execute(game, result, boost, at);
            }
            // Harvest exchanges only surplus above40 after its initial three
            // functional purchases. No salvage exchange keeps a resource-only
            // matched construction control beside the income-changing policy.
            if (result.Policy == "skills-first" && purchased.Contains("potato.yield.1") && purchased.Contains("bonus.unlock") &&
                game.Expansion.Potatoes >= 60)
            {
                var trade = Offer(game, "trade.potatoes");
                if (trade != null) Execute(game, result, trade, at);
            }
        }
        public override void BeforeSpin(GameSession game, RunResult result)
        {
            beforeSpin = game.Expansion;
            if (beforeSpin.Energy == 0) zeroEnergySpins++;
        }
        public override void AfterSpin(GameSession game, RunResult result)
        {
            var after = game.Expansion; var receipt = after.LastResourceReceipt;
            var spin = game.State.lastSpin;
            int potatoes = spin.symbols.Count(s => s == 0), boxes = spin.symbols.Count(s => s == 1), trash = spin.symbols.Count(s => s == 2);
            bool anyPigeon = spin.symbols.Any(s => s == 3), match = spin.symbols.Distinct().Count() < 3;
            string[] modules = Enumerable.Range(1, beforeSpin.SocketCount).Select(beforeSpin.ModuleAt).ToArray();
            Need(receipt != null && receipt.Sequence == game.State.lastSpin.sequence && after.ForegroundSpins == beforeSpin.ForegroundSpins + 1,
                "Foreground resource receipt/count differs");
            Need(after.Parts - beforeSpin.Parts == receipt.Parts && after.Potatoes - beforeSpin.Potatoes == receipt.Potatoes &&
                after.Energy - beforeSpin.Energy == receipt.Energy, "Actual resource credits differ from receipt");
            creditedParts += receipt.Parts; creditedPotatoes += receipt.Potatoes; creditedEnergy += receipt.Energy;
            long nominalEnergy = 1 + beforeSpin.EquipmentLevel(FamilyIds.PowerSource) + (anyPigeon && modules.Contains("module.dynamo") ? 1 : 0);
            Need(after.Energy == Math.Min(after.EnergyCap, beforeSpin.Energy + nominalEnergy), "Energy cadence/cap differs from published contract");
            lostEnergy += nominalEnergy - receipt.Energy;
            string phaseKey = "power=" + beforeSpin.EquipmentLevel(FamilyIds.PowerSource) + ";dynamo=" + modules.Contains("module.dynamo");
            if (!energyPhases.TryGetValue(phaseKey, out var phase)) energyPhases.Add(phaseKey, phase = new EnergyPhase());
            phase.Spins++; phase.Nominal += nominalEnergy; phase.Credited += receipt.Energy; phase.Wasted += nominalEnergy - receipt.Energy;
            if (receipt.BonusSymbol >= 0) phase.BonusRolls++;
            if (receipt.Boosted) phase.BoostedRolls++;
            if (after.Parts == after.PartsCap) partsAtCap++;
            if (after.Potatoes == after.PotatoesCap) potatoesAtCap++;
            if (after.Energy == after.EnergyCap) energyAtCap++;
            if (receipt.Boosted) { result.BoostConsumptions++; consumedAt.Add(result.Spins); }
            void Effect(string id, bool condition)
            {
                if (!condition || !purchased.Contains(id) || firstEffect.ContainsKey(id)) return;
                firstEffect[id] = result.Spins;
                if (result.Spins <= 50) result.UsefulShopPurchasesBy50++;
            }
            Effect("module.salvage", modules.Contains("module.salvage") && trash > 0 && receipt.Parts > 0);
            Effect("module.packing", modules.Contains("module.packing") && boxes > 0 && receipt.Parts > 0);
            Effect("module.harvest", modules.Contains("module.harvest") && potatoes > 0 && receipt.Potatoes > 0);
            Effect("module.combo", modules.Contains("module.combo") && match && (receipt.Parts > 0 || receipt.Potatoes > 0));
            Effect("module.dynamo", modules.Contains("module.dynamo") && anyPigeon && receipt.Energy > 0);
            Effect("bonus.unlock", receipt.BonusSymbol >= 0 && (receipt.BonusParts > 0 || receipt.BonusPotatoes > 0));
            Effect("socket.2", beforeSpin.SocketCount >= 2 && modules[1] != null && modules[1] != "" && firstEffect.ContainsKey(modules[1]));
            Effect("socket.3", beforeSpin.SocketCount >= 3 && modules[2] != null && modules[2] != "" && firstEffect.ContainsKey(modules[2]));
            for (int level = 1; level <= 2; level++)
            {
                Effect("potato.yield." + level, beforeSpin.PotatoYieldLevel >= level && potatoes > 0 && receipt.Potatoes > 0);
                Effect("equipment.workshop." + level, beforeSpin.EquipmentLevel(FamilyIds.Workshop) >= level && receipt.Parts > 0);
                Effect("equipment.recycler." + level, beforeSpin.EquipmentLevel(FamilyIds.Recycler) >= level && trash > 0 && receipt.Parts > 0);
                Effect("equipment.cart." + level, beforeSpin.EquipmentLevel(FamilyIds.Cart) >= level && boxes > 0 && receipt.Parts > 0);
                Effect("equipment.power-source." + level, beforeSpin.EquipmentLevel(FamilyIds.PowerSource) >= level && receipt.Energy > 0);
            }
            if (result.Spins <= 120) result.ExpansionSpinTrace.Add(new { result.Spins, symbols = string.Join("", spin.symbols),
                coinIncome = spin.reward + spin.bonus + spin.chapter.Effects.TotalCoins,
                partsBefore = beforeSpin.Parts, potatoesBefore = beforeSpin.Potatoes, energyBefore = beforeSpin.Energy,
                parts = after.Parts, potatoes = after.Potatoes, energy = after.Energy,
                creditedParts = receipt.Parts, creditedPotatoes = receipt.Potatoes, creditedEnergy = receipt.Energy,
                receipt.BonusSymbol, receipt.BonusParts, receipt.BonusPotatoes, receipt.Boosted,
                loadout = string.Join("|", modules), potatoYield = beforeSpin.PotatoYieldLevel,
                workshopEquipment = beforeSpin.EquipmentLevel(FamilyIds.Workshop), recyclerEquipment = beforeSpin.EquipmentLevel(FamilyIds.Recycler),
                cartEquipment = beforeSpin.EquipmentLevel(FamilyIds.Cart), powerEquipment = beforeSpin.EquipmentLevel(FamilyIds.PowerSource) });
            Need(after.Parts == creditedParts - spentParts && after.Potatoes == creditedPotatoes - spentPotatoes && after.Energy == creditedEnergy - spentEnergy,
                "Independent cumulative resource ledger differs");
        }
        public override object Snapshot(GameSession game)
        {
            var state = game.Expansion;
            return new { available = true, state.Parts, state.Potatoes, state.Energy, state.PartsCap, state.PotatoesCap, state.EnergyCap,
                state.SocketCount, state.PotatoYieldLevel, state.BonusUnlocked, state.BoostArmed, state.BonusProgress, state.BonusCadence,
                state.BonusRolls, state.ForegroundSpins, state.ThemeId,
                modules = Enumerable.Range(1, state.SocketCount).Select(state.ModuleAt).ToArray(),
                equipment = FamilyIds.All.ToDictionary(id => id, state.EquipmentLevel),
                sourceChecks = checks, zeroEnergySpins, partsAtCapSpins = partsAtCap, potatoesAtCapSpins = potatoesAtCap,
                energyAtCapSpins = energyAtCap, wastedEnergyAtCap = lostEnergy,
                creditedParts, creditedPotatoes, creditedEnergy, spentParts, spentPotatoes, spentEnergy,
                firstAffordable = firstAffordable.ToDictionary(p => p.Key, p => p.Value),
                firstObservedEffect = firstEffect.ToDictionary(p => p.Key, p => p.Value),
                pairedDiagnostics = pairedDiagnostics.ToArray(),
                energyPhases = energyPhases.Select(p => new { loadout = p.Key, p.Value.Spins, p.Value.Nominal, p.Value.Credited,
                    p.Value.Wasted, p.Value.BonusRolls, p.Value.BoostedRolls }).ToArray(),
                boostArmedAt = armedAt.ToArray(), boostConsumedAt = consumedAt.ToArray(),
                refusalReasons = refusalReasons.ToDictionary(p => p.Key, p => p.Value),
                shortfalls = ExpansionCatalog.Offers.Where(o => Functional(o.Id) && !purchased.Contains(o.Id)).Select(o => new {
                    o.Id, missingParts = Math.Max(0, o.PartsCost - state.Parts), missingPotatoes = Math.Max(0, o.PotatoesCost - state.Potatoes),
                    missingEnergy = Math.Max(0, o.EnergyCost - state.Energy),
                    note = "Currency shortfall only; family/ownership/socket prerequisites may still lock this offer." }).ToArray() };
        }
    }
}
#endif
