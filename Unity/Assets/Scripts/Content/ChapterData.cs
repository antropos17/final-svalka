using System.Collections.Generic;
using JunkToNeon.Contracts;

namespace JunkToNeon.Content
{
    public static class ChapterData
    {
        // Explicit provisional tuning, not an exponential formula or measured balance.
        // Columns: Junkyard build/improve, Workshop Yard upgrade1/upgrade2.
        static readonly long[,] prices = {
            { 40, 600, 420, 680 }, { 120, 260, 380, 620 }, { 180, 320, 400, 640 },
            { 240, 360, 440, 660 }, { 160, 300, 360, 580 }, { 200, 340, 460, 700 }
        };
        static readonly string[] names = { "House", "Workshop", "Recycler", "Aviary", "Cart", "Power source" };
        public static ChapterDefinition TwoEras()
        {
            return new ChapterDefinition(ChapterContentIds.Catalog, new[] {
                new EraDefinition(EraIds.Junkyard, Era(EraIds.Junkyard, 0)),
                new EraDefinition(EraIds.WorkshopYard, Era(EraIds.WorkshopYard, 1)) });
        }
        static CatalogDefinition Era(string era, int eraIndex)
        {
            var families = new List<FamilyDefinition>(); var stages = new List<StageDefinition>();
            var upgrades = new List<UpgradeDefinition>(); var completion = new List<string>();
            for (int i = 0; i < FamilyIds.All.Count; i++)
            {
                string family = FamilyIds.All[i]; families.Add(new FamilyDefinition(family, names[i]));
                for (int level = 0; level < 3; level++)
                    stages.Add(new StageDefinition(ChapterIds.Stage(era, family, level), family, level,
                        eraIndex == 0 && i == 0 ? "Editable/HouseStage" + level : null, FallbackVisual.TextAndBlock));
                for (int level = 1; level <= 2; level++)
                {
                    string id = ChapterIds.Project(era, family, level);
                    var prerequisites = new List<string>();
                    if (level == 2) prerequisites.Add(ChapterIds.Project(era, family, 1));
                    else if (eraIndex == 0 && i != 0) prerequisites.Add(GameIds.HouseRepair);
                    var effects = new List<ContentEffect>();
                    string benefit;
                    if (eraIndex == 0 && i == 0)
                    {
                        if (level == 1) effects.Add(new ContentEffect(ContentIds.UnlockAuto, ContentEffectKind.UnlockAuto, 0, false));
                        benefit = level == 1 ? "Unlocks AUTO." : "Completes the house requirement for Workshop Yard.";
                    }
                    else if (eraIndex == 0 && i < 4 && level == 1)
                    {
                        if (i == 1) effects.Add(new ContentEffect(GameIds.WorkshopSpinCoins, ContentEffectKind.SpinCoins, 1, true));
                        if (i == 2) effects.Add(new ContentEffect(GameIds.RecyclerTrashCoins, ContentEffectKind.TrashSymbolCoins, 1, true));
                        if (i == 3) effects.Add(new ContentEffect(GameIds.PigeonDeliveryCoins, ContentEffectKind.PigeonDeliveryCoins, 3, true, 3));
                        benefit = i == 1 ? "+1 coin per later spin." : i == 2 ? "+1 coin per trash symbol on later spins." : "+3 coins per three pigeon appearances on later spins; carries 0..2.";
                    }
                    else
                    {
                        int symbol = ChapterContentIds.FamilySymbol(family);
                        effects.Add(new ContentEffect(ChapterContentIds.SymbolEffect(id), ContentEffectKind.SymbolCoins, 1, true, symbolId: symbol));
                        benefit = "+1 coin per " + new[] { "potato", "box", "trash", "pigeon" }[symbol] + " symbol on later spins.";
                    }
                    upgrades.Add(new UpgradeDefinition(id, ChapterIds.Stage(era, family, level - 1), ChapterIds.Stage(era, family, level),
                        prices[i, eraIndex * 2 + level - 1], true, !(eraIndex == 0 && i == 0), prerequisites, effects, benefitSummary: benefit));
                }
                completion.Add(ChapterIds.Project(era, family, 2));
            }
            return new CatalogDefinition(era + ".active.v1", false, families, stages, upgrades, completion);
        }
    }
}
