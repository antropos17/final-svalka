using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JunkToNeon.Contracts
{
    public static class ChapterEffectIds
    {
        public const string PotatoCoins = "chapter.potato-coins", BoxCoins = "chapter.box-coins";
        public const string TrashCoins = "chapter.trash-coins", PigeonCoins = "chapter.pigeon-coins";
        public const string SkillUniversal = "skill.universal-coins", SkillRecycling = "skill.recycling-coins", SkillDelivery = "skill.delivery-coins";
    }
    public sealed class SymbolCoinSource
    {
        public string SourceId { get; }
        public int SymbolId { get; }
        public long CoinsPerSymbol { get; }
        public SymbolCoinSource(string sourceId, int symbolId, long coinsPerSymbol)
        {
            if (!StableId.IsValid(sourceId) || !SymbolIds.IsValid(symbolId) || coinsPerSymbol < 1 || coinsPerSymbol > 1000000)
                throw new ArgumentException("Invalid symbol-coin source");
            SourceId = sourceId; SymbolId = symbolId; CoinsPerSymbol = coinsPerSymbol;
        }
    }
    // Complete immutable source descriptors, aggregated to four bounded coin rates.
    // Registration/ownership and pinned-catalog validation remain the caller's responsibility.
    public sealed class ChapterSymbolModifiers
    {
        public const int MaximumSources = 32;
        readonly ReadOnlyCollection<SymbolCoinSource> sources;
        readonly long[] rates = new long[4];
        public IReadOnlyList<SymbolCoinSource> Sources { get { return sources; } }
        public static ChapterSymbolModifiers Empty { get; } = new ChapterSymbolModifiers(Array.Empty<SymbolCoinSource>());
        public ChapterSymbolModifiers(IEnumerable<SymbolCoinSource> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<SymbolCoinSource>();
            foreach (var value in values)
            {
                if (list.Count >= MaximumSources || value == null || !ids.Add(value.SourceId)) throw new ArgumentException("Null/duplicate/oversized source list");
                list.Add(value); rates[value.SymbolId] = checked(rates[value.SymbolId] + value.CoinsPerSymbol);
            }
            list.Sort((left, right) => StringComparer.Ordinal.Compare(left.SourceId, right.SourceId));
            sources = list.AsReadOnly();
        }
        public long CoinsPerSymbol(int symbolId)
        {
            if (!SymbolIds.IsValid(symbolId)) throw new ArgumentOutOfRangeException(nameof(symbolId));
            return rates[symbolId];
        }
    }
}
