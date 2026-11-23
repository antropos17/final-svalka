using JunkToNeon.Contracts;

namespace JunkToNeon.Content
{
    // Content-owned descriptor registry; identities/DTOs come from shared Contracts.
    public static class ChapterContentIds
    {
        public const string Catalog = "chapter.two-eras.v1";
        public const string RuleRevision = "chapter.rules.v1";
        public static string SymbolEffect(string upgradeId) { return upgradeId + ".symbol-coins"; }
        public static bool TrySymbolEffect(string effectId, out int symbol)
        {
            symbol = -1;
            for (int era = 0; era < 2; era++) for (int family = 0; family < FamilyIds.All.Count; family++) for (int level = 1; level <= 2; level++)
            {
                if (era == 0 && (family == 0 || family < 4 && level == 1)) continue;
                string upgrade = ChapterIds.Project(era == 0 ? EraIds.Junkyard : EraIds.WorkshopYard, FamilyIds.All[family], level);
                if (effectId == SymbolEffect(upgrade)) { symbol = FamilySymbol(FamilyIds.All[family]); return true; }
            }
            return false;
        }
        public static int FamilySymbol(string family)
        {
            switch (family)
            {
                case FamilyIds.House: case FamilyIds.PowerSource: return SymbolIds.Potato;
                case FamilyIds.Workshop: case FamilyIds.Cart: return SymbolIds.Box;
                case FamilyIds.Recycler: return SymbolIds.Trash;
                case FamilyIds.PigeonLoft: return SymbolIds.Pigeon;
                default: throw new System.ArgumentException("Unknown family.", nameof(family));
            }
        }
    }
}
