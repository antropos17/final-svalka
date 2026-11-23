using System.Collections.Generic;
using JunkToNeon.Contracts;

namespace JunkToNeon.Content
{
    public static class ContentIds
    {
        public const string UnlockAuto = "house.unlock-auto";
        public const string House = "house", Workshop = "workshop", Recycler = "recycler";
        // Aviary in the source plan; shared project identity is pigeon-loft.build.
        public const string PigeonLoft = "pigeon-loft", Cart = "cart", PowerSource = "power-source";
        public static string Stage(string familyId, int level) { return familyId + ".stage." + level; }
    }

    public static class CatalogData
    {
        // Integration passes EconomyRules values. This assembly must not depend on Core.
        public static CatalogDefinition CurrentPrototype(long repairPrice, long improvePrice, long workshopPrice, long workshopBonus)
        {
            var stages = HouseStages();
            stages.Add(new StageDefinition("workshop.stage.0", ContentIds.Workshop, 0));
            stages.Add(new StageDefinition("workshop.stage.1", ContentIds.Workshop, 1));
            return new CatalogDefinition("prototype.r6", false,
                new[] { new FamilyDefinition(ContentIds.House, "House"), new FamilyDefinition(ContentIds.Workshop, "Workshop") }, stages,
                new[] {
                    new UpgradeDefinition(GameIds.HouseRepair, "house.stage.0", "house.stage.1", repairPrice, true, false,
                        effects: new[] { new ContentEffect(ContentIds.UnlockAuto, ContentEffectKind.UnlockAuto, 0, false) }),
                    new UpgradeDefinition(GameIds.HouseImprove, "house.stage.1", "house.stage.2", improvePrice, true, false,
                        new[] { GameIds.HouseRepair }),
                    new UpgradeDefinition(GameIds.WorkshopBuild, "workshop.stage.0", "workshop.stage.1", workshopPrice, true, true,
                        new[] { GameIds.HouseRepair },
                        new[] { new ContentEffect(GameIds.WorkshopSpinCoins, ContentEffectKind.SpinCoins, workshopBonus, true) })
                }, new[] { GameIds.HouseImprove, GameIds.WorkshopBuild });
        }

        // Six identities are from original tasks25/27. Expansion tuning and dependency edges
        // are proposed. No runtime caller selects this dataset automatically.
        public static CatalogDefinition ProposedFirstEra()
        {
            string[] ids = { ContentIds.House, ContentIds.Workshop, ContentIds.Recycler, ContentIds.PigeonLoft, ContentIds.Cart, ContentIds.PowerSource };
            string[] names = { "House", "Workshop", "Recycler", "Aviary", "Cart", "Power source" };
            string[] builds = { GameIds.HouseRepair, GameIds.WorkshopBuild, GameIds.RecyclerBuild, GameIds.PigeonLoftBuild, "cart.build", "power-source.build" };
            var families = new List<FamilyDefinition>(); var stages = HouseStages();
            var upgrades = new List<UpgradeDefinition>(); var completion = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                string family = ids[i], build = builds[i];
                families.Add(new FamilyDefinition(family, names[i]));
                if (i != 0) for (int level = 0; level < 3; level++) stages.Add(new StageDefinition(ContentIds.Stage(family, level), family, level));
                ContentEffect[] effects = new ContentEffect[0];
                if (i == 0) effects = new[] { new ContentEffect(ContentIds.UnlockAuto, ContentEffectKind.UnlockAuto, 0, false) };
                if (i == 1) effects = new[] { new ContentEffect(GameIds.WorkshopSpinCoins, ContentEffectKind.SpinCoins, 1, true) };
                if (i == 2) effects = new[] { new ContentEffect(GameIds.RecyclerTrashCoins, ContentEffectKind.TrashSymbolCoins, 1, true) };
                if (i == 3) effects = new[] { new ContentEffect(GameIds.PigeonDeliveryCoins, ContentEffectKind.PigeonDeliveryCoins, 3, true, 3) };
                upgrades.Add(new UpgradeDefinition(build, ContentIds.Stage(family, 0), ContentIds.Stage(family, 1),
                    i == 0 ? (long?)40 : i == 1 ? 120 : (long?)null, false, true,
                    i == 0 ? new string[0] : new[] { GameIds.HouseRepair }, effects, benefitPending: i >= 4));
                string improve = i == 0 ? GameIds.HouseImprove : family + ".improve";
                upgrades.Add(new UpgradeDefinition(improve, ContentIds.Stage(family, 1), ContentIds.Stage(family, 2),
                    i == 0 ? (long?)600 : null, false, true, new[] { build }, benefitPending: i != 0));
                completion.Add(improve);
            }
            return new CatalogDefinition("junkyard.proposed.v1", true, families, stages, upgrades, completion);
        }
        static List<StageDefinition> HouseStages()
        {
            var result = new List<StageDefinition>();
            for (int i = 0; i < 3; i++)
                result.Add(new StageDefinition(ContentIds.Stage(ContentIds.House, i), ContentIds.House, i,
                    "Editable/HouseStage" + i, FallbackVisual.TextAndBlock));
            return result;
        }
    }
}
