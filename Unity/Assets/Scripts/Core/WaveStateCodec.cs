using System;
using System.Collections.Generic;
using JunkToNeon.Contracts;
using JunkToNeon.Effects;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Core
{
    public sealed partial class JsonStateCodec
    {
        static object OptionalObject(object value, string key)
        {
            var map = value as Dictionary<string, object>;
            if (map == null || !map.TryGetValue(key, out var raw)) return null;
            map.Remove(key);
            if (raw == null) throw new FormatException("Present optional object cannot be null: " + key);
            return raw;
        }
        static string Q(string value)
        {
            if (value == null) return "null";
            var b = new System.Text.StringBuilder("\"");
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') b.Append('\\').Append(c);
                else if (c < 32) b.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                else b.Append(c);
            }
            return b.Append('"').ToString();
        }
        static string EncodeDelivery(DeliveryState d)
        { return "{\"sequence\":" + N(d.LastOperationSequence) + ",\"carry\":" + N(d.PigeonProgress) + ",\"cadence\":" + N(d.Cadence) + ",\"key\":" + Q(d.LastProposalKey) + "}"; }
        static DeliveryState DecodeDelivery(object raw)
        {
            var d = Object(raw, "sequence", "carry", "cadence", "key");
            return new DeliveryState(Number(d["sequence"]), Int(d["carry"]), Int(d["cadence"]), d["key"] == null ? null : Text(d["key"]));
        }
        static UpgradeOwnership DecodeOwnership(object raw)
        {
            var o = Object(raw, "version", "level", "revision", "sequence", "walletBefore", "houseStage");
            return UpgradeOwnershipCodec.DecodeOrDefault("{\"version\":" + N(Number(o["version"])) + ",\"level\":" + N(Number(o["level"])) +
                ",\"revision\":" + N(Number(o["revision"])) + ",\"sequence\":" + N(Number(o["sequence"])) + ",\"walletBefore\":" + N(Number(o["walletBefore"])) + ",\"houseStage\":" + N(Number(o["houseStage"])) + "}");
        }
        static string EncodeWave(WaveProgress w)
        { return "{\"format\":1,\"recycler\":" + N(w.RecyclerBuilt) + ",\"pigeonLoft\":" + N(w.PigeonLoftBuilt) + ",\"delivery\":" + EncodeDelivery(w.Delivery) + ",\"slot\":" + UpgradeOwnershipCodec.Encode(w.Slot) + "}"; }
        static WaveProgress DecodeWave(object raw)
        {
            if (raw == null) return null;
            var w = Object(raw, "format", "recycler", "pigeonLoft", "delivery", "slot");
            if (Int(w["format"]) != 1) throw new FormatException("Unknown wave progress version");
            return new WaveProgress(Int(w["recycler"]), Int(w["pigeonLoft"]), DecodeDelivery(w["delivery"]), DecodeOwnership(w["slot"]));
        }
        static string EncodeEffects(FrozenSpinEffects e)
        {
            if (e == null) return "null";
            return "{\"format\":1,\"sequence\":" + N(e.OperationSequence) + ",\"recyclerOwned\":" + N(e.Ownership.RecyclerLevel) + ",\"pigeonOwned\":" + N(e.Ownership.PigeonLoftLevel) +
                ",\"trashRate\":" + N(e.Rules.RecyclerCoinsPerTrash) + ",\"cadence\":" + N(e.Rules.PigeonsPerDelivery) + ",\"deliveryRate\":" + N(e.Rules.CoinsPerDelivery) +
                ",\"before\":" + EncodeDelivery(e.Before) + ",\"carryAfter\":" + N(e.After.PigeonProgress) + ",\"deliveries\":" + N(e.Deliveries) + ",\"recyclerCoins\":" + N(e.RecyclerCoins) + ",\"deliveryCoins\":" + N(e.DeliveryCoins) + "}";
        }
        static FrozenSpinEffects DecodeEffects(object raw, int[] symbols)
        {
            if (raw == null) return null;
            if (symbols == null || symbols.Length != 3) throw new FormatException("Effects require frozen symbols");
            var e = Object(raw, "format", "sequence", "recyclerOwned", "pigeonOwned", "trashRate", "cadence", "deliveryRate", "before", "carryAfter", "deliveries", "recyclerCoins", "deliveryCoins");
            return new FrozenSpinEffects(Int(e["format"]), Number(e["sequence"]), new SpinOutcome(symbols[0], symbols[1], symbols[2]),
                new EffectOwnership(Int(e["recyclerOwned"]), Int(e["pigeonOwned"])), new EffectRules(Number(e["trashRate"]), Int(e["cadence"]), Number(e["deliveryRate"])),
                DecodeDelivery(e["before"]), Int(e["carryAfter"]), Int(e["deliveries"]), Number(e["recyclerCoins"]), Number(e["deliveryCoins"]));
        }
        static string EncodeOperation(WaveOperation w)
        {
            return "{\"format\":" + N(w.Format) + ",\"target\":" + N(w.Target) + ",\"price\":" + N(w.Price) + ",\"baseCoins\":" + N(w.BaseCoins) +
                ",\"workshopCoins\":" + N(w.WorkshopCoins) + ",\"slotCoins\":" + N(w.SlotCoins) + ",\"effects\":" + EncodeEffects(w.Effects) + ",\"purchase\":" + (w.Purchase == null ? "null" : UpgradeOwnershipCodec.Encode(w.Purchase)) + "}";
        }
        static WaveOperation DecodeOperation(object raw, int[] symbols)
        {
            if (raw == null) return null;
            var w = Object(raw, "format", "target", "price", "baseCoins", "workshopCoins", "slotCoins", "effects", "purchase");
            return new WaveOperation(Int(w["format"]), Int(w["target"]), Number(w["price"]), Number(w["baseCoins"]), Number(w["workshopCoins"]), Number(w["slotCoins"]),
                DecodeEffects(w["effects"], symbols), w["purchase"] == null ? null : DecodeOwnership(w["purchase"]));
        }
    }
}
