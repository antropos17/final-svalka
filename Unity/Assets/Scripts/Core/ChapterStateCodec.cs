using System;
using System.Collections.Generic;
using System.Text;
using JunkToNeon.Contracts;
using JunkToNeon.Effects;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Core
{
    public sealed partial class JsonStateCodec
    {
        // Skill history remains one bounded strict module payload. Keeping it a
        // JSON string also avoids duplicating its historical-receipt parser here.
        static string EncodeChapterState(ChapterState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            string skills = SkillOwnershipCodec.Encode(state.Skills);
            if (skills.Length > SkillOwnershipCodec.MaximumJsonLength) throw new FormatException("Skill payload too large");
            var stages = ChapterCatalog.Rules.Stages(state.Progress);
            return "{\"format\":2,\"rule\":" + Q(state.RuleRevision) + ",\"era\":" + Q(stages.EraId) +
                ",\"stages\":" + EncodeChapterStages(stages) + ",\"skills\":" + Q(skills) +
                ",\"selected\":" + Q(state.SelectedProjectId) + ",\"entry\":" + N(state.EntrySequence) +
                ",\"seen\":" + N(state.TransitionSeen) +
                (state.ConstructionPriceRevision == JunkToNeon.Content.ConstructionPricing.LegacyRevision ? "" : ",\"pricing\":" + Q(state.ConstructionPriceRevision)) + "}";
        }

        static ChapterState DecodeChapterState(object raw)
        {
            var pricing = OptionalObject(raw, "pricing");
            var state = Object(raw, "format", "rule", "era", "stages", "skills", "selected", "entry", "seen");
            var stages = DecodeChapterStages(Text(state["era"]), state["stages"]);
            var skills = SkillOwnershipCodec.DecodeOrDefault(Text(state["skills"]));
            return new ChapterState(Int(state["format"]), Text(state["rule"]), ChapterCatalog.Rules.FromStages(stages), skills,
                state["selected"] == null ? null : Text(state["selected"]), Number(state["entry"]), Number(state["seen"]),
                pricing == null ? JunkToNeon.Content.ConstructionPricing.LegacyRevision : Text(pricing));
        }

        static string EncodeChapterStages(FamilyStages stages)
        {
            var output = new StringBuilder("[");
            for (int index = 0; index < FamilyIds.All.Count; index++)
            {
                if (index != 0) output.Append(',');
                output.Append(N(stages.AbsoluteLevel(FamilyIds.All[index])));
            }
            return output.Append(']').ToString();
        }

        static FamilyStages DecodeChapterStages(string eraId, object raw)
        {
            var levels = raw as List<object>;
            if (levels == null || levels.Count != FamilyIds.All.Count) throw new FormatException("Expected six absolute family stages");
            var stages = new List<FamilyStage>(FamilyIds.All.Count);
            for (int index = 0; index < FamilyIds.All.Count; index++)
                stages.Add(new FamilyStage(FamilyIds.All[index], Int(levels[index])));
            return new FamilyStages(eraId, stages);
        }

        static string EncodeChapterOperation(ChapterOperation operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            var symbols = operation.Symbols;
            ValidateChapterSymbols(symbols);
            return "{\"format\":2,\"kind\":" + Q(operation.Kind) + ",\"sequence\":" + N(operation.Sequence) +
                ",\"at\":" + N(operation.At) + ",\"action\":" + Q(operation.ActionId) + ",\"price\":" + N(operation.Price) +
                ",\"coinsBefore\":" + N(operation.CoinsBefore) + ",\"coinsAfter\":" + N(operation.CoinsAfter) +
                ",\"seedBefore\":" + N(operation.SeedBefore) + ",\"seedAfter\":" + N(operation.SeedAfter) +
                ",\"symbols\":" + Symbols(symbols) + ",\"base\":" + N(operation.BaseCoins) + ",\"workshop\":" + N(operation.WorkshopCoins) +
                ",\"before\":" + EncodeChapterState(operation.Before) + ",\"after\":" + EncodeChapterState(operation.After) +
                ",\"effects\":" + EncodeChapterEffects(operation.Effects) + ",\"skillRequest\":" + EncodeChapterSkillRequest(operation.SkillRequest) +
                ",\"settlement\":" + EncodeChapterSettlement(operation.Settlement) + "}";
        }

        static ChapterOperation DecodeChapterOperation(object raw)
        {
            var operation = Object(raw, "format", "kind", "sequence", "at", "action", "price", "coinsBefore", "coinsAfter",
                "seedBefore", "seedAfter", "symbols", "base", "workshop", "before", "after", "effects", "skillRequest", "settlement");
            var symbols = Reels(operation["symbols"]);
            ValidateChapterSymbols(symbols);
            return new ChapterOperation(Int(operation["format"]), Text(operation["kind"]), Number(operation["sequence"]), Number(operation["at"]),
                operation["action"] == null ? null : Text(operation["action"]), Number(operation["price"]),
                Number(operation["coinsBefore"]), Number(operation["coinsAfter"]), UInt(operation["seedBefore"]), UInt(operation["seedAfter"]),
                symbols, Int(operation["base"]), Int(operation["workshop"]), DecodeChapterState(operation["before"]), DecodeChapterState(operation["after"]),
                DecodeChapterEffects(operation["effects"], symbols), DecodeChapterSkillRequest(operation["skillRequest"]), DecodeChapterSettlement(operation["settlement"]));
        }

        static void ValidateChapterSymbols(int[] symbols)
        {
            if (symbols == null) return;
            if (symbols.Length != 3) throw new FormatException("Expected three frozen symbols");
            foreach (int symbol in symbols) if (!SymbolIds.IsValid(symbol)) throw new FormatException("Unknown frozen symbol");
        }

        static string EncodeChapterEffects(FrozenSpinEffectsV2 effects)
        {
            if (effects == null) return "null";
            var sources = new StringBuilder("[");
            bool first = true;
            foreach (var source in effects.Modifiers.Sources)
            {
                if (!first) sources.Append(','); first = false;
                sources.Append("{\"id\":").Append(Q(source.SourceId)).Append(",\"symbol\":").Append(N(source.SymbolId))
                    .Append(",\"coins\":").Append(N(source.CoinsPerSymbol)).Append('}');
            }
            sources.Append(']');
            return "{\"format\":2,\"baseline\":" + EncodeEffects(effects.Baseline) + ",\"rule\":" + Q(effects.RuleRevision) +
                ",\"sources\":" + sources + ",\"universal\":" + N(effects.UniversalCoinsPerSpin) +
                ",\"recycling\":" + N(effects.ExtraCoinsPerTrash) + ",\"delivery\":" + N(effects.ExtraCoinsPerDelivery) +
                ",\"amounts\":[" + N(effects.UniversalCoins) + "," + N(effects.RecyclingSkillCoins) + "," + N(effects.DeliverySkillCoins) +
                "," + N(effects.PotatoCoins) + "," + N(effects.BoxCoins) + "," + N(effects.TrashCoins) + "," + N(effects.PigeonCoins) + "]}";
        }

        static FrozenSpinEffectsV2 DecodeChapterEffects(object raw, int[] symbols)
        {
            if (raw == null) return null;
            var effects = Object(raw, "format", "baseline", "rule", "sources", "universal", "recycling", "delivery", "amounts");
            var sources = effects["sources"] as List<object>;
            if (sources == null || sources.Count > ChapterSymbolModifiers.MaximumSources) throw new FormatException("Invalid frozen symbol sources");
            var restored = new List<SymbolCoinSource>(sources.Count);
            foreach (var rawSource in sources)
            {
                var source = Object(rawSource, "id", "symbol", "coins");
                restored.Add(new SymbolCoinSource(Text(source["id"]), Int(source["symbol"]), Number(source["coins"])));
            }
            var amounts = effects["amounts"] as List<object>;
            if (amounts == null || amounts.Count != 7) throw new FormatException("Expected seven frozen chapter amounts");
            var baseline = DecodeEffects(effects["baseline"], symbols);
            if (baseline == null) throw new FormatException("Missing frozen baseline effects");
            return new FrozenSpinEffectsV2(Int(effects["format"]), baseline, Text(effects["rule"]), new ChapterSymbolModifiers(restored),
                Number(effects["universal"]), Number(effects["recycling"]), Number(effects["delivery"]),
                Number(amounts[0]), Number(amounts[1]), Number(amounts[2]), Number(amounts[3]), Number(amounts[4]), Number(amounts[5]), Number(amounts[6]));
        }

        static string EncodeChapterSkillRequest(SkillPurchaseRequest request)
        {
            if (request == null) return "null";
            ValidateChapterSkillRequest(request);
            var context = request.Context;
            return "{\"node\":" + Q(request.NodeId) + ",\"operation\":" + Q(request.OperationId) + ",\"sequence\":" + N(request.Sequence) +
                ",\"wallet\":" + N(request.ExpectedWallet) + ",\"ownership\":" + Q(request.ExpectedOwnership) +
                (request.PricingRevision==SkillPricing.LegacyRevision?"":",\"priceRevision\":"+Q(request.PricingRevision))+
                ",\"context\":{\"era\":" + Q(context.EraId) + ",\"house\":" + N(context.HouseStage) + ",\"workshop\":" + N(context.WorkshopLevel) +
                ",\"recycler\":" + N(context.RecyclerLevel) + ",\"pigeon\":" + N(context.PigeonLoftLevel) + ",\"cart\":" + N(context.CartStage) +
                ",\"power\":" + N(context.PowerSourceStage) + ",\"selected\":" + Q(context.SelectedConstruction) + "}}";
        }

        static SkillPurchaseRequest DecodeChapterSkillRequest(object raw)
        {
            if (raw == null) return null;
            var map=raw as Dictionary<string,object>;string pricing=SkillPricing.LegacyRevision;
            if(map!=null&&map.TryGetValue("priceRevision",out var storedPricing)){pricing=Text(storedPricing);map.Remove("priceRevision");}
            var request = Object(raw, "node", "operation", "sequence", "wallet", "ownership", "context");
            var context = Object(request["context"], "era", "house", "workshop", "recycler", "pigeon", "cart", "power", "selected");
            var stages = new FamilyStages(Text(context["era"]), new[] {
                new FamilyStage(FamilyIds.House, Int(context["house"])),
                new FamilyStage(FamilyIds.Workshop, Int(context["workshop"])),
                new FamilyStage(FamilyIds.Recycler, Int(context["recycler"])),
                new FamilyStage(FamilyIds.PigeonLoft, Int(context["pigeon"])),
                new FamilyStage(FamilyIds.Cart, Int(context["cart"])),
                new FamilyStage(FamilyIds.PowerSource, Int(context["power"])) });
            var restored = new SkillPurchaseRequest(Text(request["node"]), Text(request["operation"]), Number(request["sequence"]),
                Number(request["wallet"]), Text(request["ownership"]),
                new SkillContext(stages, context["selected"] == null ? null : Text(context["selected"])),pricing);
            ValidateChapterSkillRequest(restored);
            return restored;
        }

        static void ValidateChapterSkillRequest(SkillPurchaseRequest request)
        {
            if (!SkillPricing.IsKnown(request.PricingRevision) || request.Context == null || !StableId.IsValid(request.NodeId) || !StableId.IsValid(request.OperationId) ||
                request.Sequence < 1 || request.ExpectedWallet < 0 || string.IsNullOrEmpty(request.ExpectedOwnership) || request.ExpectedOwnership.Length > 2048)
                throw new FormatException("Invalid frozen skill request shape");
        }

        static string EncodeChapterSettlement(OfflineReceipt receipt)
        {
            if (receipt == null) return "null";
            return "{\"sequence\":" + N(receipt.sequence) + ",\"from\":" + N(receipt.from) + ",\"to\":" + N(receipt.to) +
                ",\"seconds\":" + N(receipt.seconds) + ",\"coins\":" + N(receipt.coins) + ",\"workshop\":" + N(receipt.workshop) + "}";
        }

        static OfflineReceipt DecodeChapterSettlement(object raw)
        {
            if (raw == null) return null;
            var receipt = Object(raw, "sequence", "from", "to", "seconds", "coins", "workshop");
            return new OfflineReceipt { sequence = Number(receipt["sequence"]), from = Number(receipt["from"]), to = Number(receipt["to"]),
                seconds = Number(receipt["seconds"]), coins = Number(receipt["coins"]), workshop = Int(receipt["workshop"]) };
        }
    }
}
