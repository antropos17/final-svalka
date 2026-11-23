using System;
using System.Collections.Generic;

namespace JunkToNeon.Contracts
{
    // Persistent identities, independent of translated labels and catalog ordering.
    public static class GameIds
    {
        public const string Coins = "coins";
        public const string HouseRepair = "house.repair", HouseImprove = "house.improve";
        public const string WorkshopBuild = "workshop.build", RecyclerBuild = "recycler.build", PigeonLoftBuild = "pigeon-loft.build";
        public const string SlotCoinBoost = "slot.coin-boost";
        public const string WorkshopSpinCoins = "workshop.spin-coins", RecyclerTrashCoins = "recycler.trash-coins", PigeonDeliveryCoins = "pigeon.delivery-coins";
    }

    public static class StableId
    {
        public const int MaximumLength = 64;
        public static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaximumLength || value[0] < 'a' || value[0] > 'z') return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '.' && c != '-') return false;
            return true;
        }
    }

    public static class SymbolIds
    {
        public const int Potato = 0, Box = 1, Trash = 2, Pigeon = 3;
        public static bool IsValid(int id) { return id >= Potato && id <= Pigeon; }
    }

    public readonly struct SpinOutcome
    {
        public int Left { get; }
        public int Middle { get; }
        public int Right { get; }
        public SpinOutcome(int left, int middle, int right)
        {
            if (!SymbolIds.IsValid(left) || !SymbolIds.IsValid(middle) || !SymbolIds.IsValid(right)) throw new ArgumentOutOfRangeException(nameof(left), "Unknown reel symbol");
            Left = left; Middle = middle; Right = right;
        }
        public int Count(int symbol)
        {
            if (!SymbolIds.IsValid(symbol)) throw new ArgumentOutOfRangeException(nameof(symbol));
            return (Left == symbol ? 1 : 0) + (Middle == symbol ? 1 : 0) + (Right == symbol ? 1 : 0);
        }
    }

    // One contribution per stable effect ID. This describes value; it never grants it.
    public sealed class EffectContribution
    {
        public string EffectId { get; }
        public long Coins { get; }
        public EffectContribution(string effectId, long coins)
        {
            if (!StableId.IsValid(effectId)) throw new ArgumentException("Invalid effect ID", nameof(effectId));
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins));
            EffectId = effectId; Coins = coins;
        }
    }

    public static class CoinEffects
    {
        public const int MaximumContributions = 16;
        public static long Total(IReadOnlyList<EffectContribution> contributions)
        {
            if (contributions == null) throw new ArgumentNullException(nameof(contributions));
            if (contributions.Count > MaximumContributions) throw new ArgumentException("Too many effect contributions", nameof(contributions));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            long total = 0;
            foreach (var contribution in contributions)
            {
                if (contribution == null || !ids.Add(contribution.EffectId)) throw new ArgumentException("Null or duplicate effect contribution", nameof(contributions));
                total = checked(total + contribution.Coins);
            }
            return total;
        }
    }
}
