using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JunkToNeon.Contracts;

namespace JunkToNeon.Core
{
    public static partial class EconomyRules
    {
        public const int ExpansionPartsCap=200, ExpansionPotatoesCap=200, ExpansionEnergyCap=30;
        public const int ExpansionEnergyPerSpin=1, ExpansionBonusCadence=5, ExpansionBoostCost=15;
        public const float ExpansionNormalSpinSeconds=3.5f, ExpansionFastSpinSeconds=1.5f;
        public const uint ExpansionBonusSeed=2738958700u;
        public const int ExpansionModuleParts=12, ExpansionHarvestParts=8, ExpansionHarvestPotatoes=8;
        public const int ExpansionComboParts=20, ExpansionComboPotatoes=10, ExpansionDynamoParts=12, ExpansionDynamoPotatoes=8;
        public const int ExpansionModuleSymbolYield=1, ExpansionComboYield=1, ExpansionDynamoEnergy=1;
        public const int ExpansionSocket2Parts=30, ExpansionSocket3Parts=60, ExpansionSocket3Potatoes=30;
        public const int ExpansionThemeParts=15, ExpansionThemePotatoes=5;
        public const int ExpansionBonusParts=20, ExpansionBonusPotatoes=10;
        public const int ExpansionYield1Parts=15, ExpansionYield1Potatoes=15, ExpansionYield2Parts=30, ExpansionYield2Potatoes=30;
        public const int ExpansionEquipment1Parts=20, ExpansionEquipment1Potatoes=10, ExpansionEquipment2Parts=40, ExpansionEquipment2Potatoes=20;
        public const int ExpansionWorkshop1Parts=25, ExpansionWorkshop1Potatoes=15;
        public const int ExpansionTradeUnits=20, ExpansionTradeCoins=20;
        public const int ExpansionHouseStoragePerLevel=50, ExpansionPigeonEnergyPerLevel=5;
        public const int ExpansionWorkbench1Parts=30, ExpansionWorkbench1Potatoes=20, ExpansionWorkbench2Parts=40, ExpansionWorkbench2Potatoes=30;
        public const int ExpansionWorkbenchBonusPerResource=1;
        public static int ExpansionBonusPartsFor(int symbol)
        { switch(symbol){case 0:return 5;case 1:return 0;case 2:return 3;case 3:return 8;default:throw new ArgumentOutOfRangeException(nameof(symbol));} }
        public static int ExpansionBonusPotatoesFor(int symbol)
        { switch(symbol){case 0:return 0;case 1:return 5;case 2:return 3;case 3:return 2;default:throw new ArgumentOutOfRangeException(nameof(symbol));} }
    }
    public sealed class ExpansionOffer
    {
        public string Id { get; }
        public string Kind { get; }
        public string FamilyId { get; }
        public int Level { get; }
        public int PartsCost { get; }
        public int PotatoesCost { get; }
        public int EnergyCost { get { return 0; } }
        public long CoinsCost { get { return 0; } }
        public long CoinsReward { get; }
        internal ExpansionOffer(string id,string kind,int parts,int potatoes,int level=0,string family=null,long coinsReward=0)
        {Id=id;Kind=kind;PartsCost=parts;PotatoesCost=potatoes;Level=level;FamilyId=family;CoinsReward=coinsReward;}
    }
    public static class ExpansionCatalog
    {
        public const string RuleRevision="expansion.rules.v1";
        public const string NextRuleRevision="expansion.rules.v2";
        public const string ArcadeRuleRevision="expansion.rules.v3";
        public const string Salvage="module.salvage", Packing="module.packing", Harvest="module.harvest";
        public const string Combo="module.combo", Dynamo="module.dynamo";
        public const string DefaultTheme="theme.neon", Amber="theme.amber", Mint="theme.mint", Violet="theme.violet";
        public const string Frost="theme.frost", Rose="theme.rose";
        public const string Workbench1="workbench.bonus.1", Workbench2="workbench.bonus.2";
        public const string FocusBalanced="focus.balanced", FocusParts="focus.parts", FocusPotatoes="focus.potatoes";
        public const string PresetParts="preset.parts", PresetHarvest="preset.harvest", PresetEnergy="preset.energy";
        public const string BonusUnlock="bonus.unlock", NextBonusBoost="boost.next-bonus";
        static readonly ReadOnlyCollection<ExpansionOffer> offers=Create();
        public static IReadOnlyList<ExpansionOffer> Offers { get { return offers; } }
        static ReadOnlyCollection<ExpansionOffer> Create()
        {
            var result=new List<ExpansionOffer> {
                new ExpansionOffer(Salvage,"module",EconomyRules.ExpansionModuleParts,0),
                new ExpansionOffer(Packing,"module",EconomyRules.ExpansionModuleParts,0),
                new ExpansionOffer(Harvest,"module",EconomyRules.ExpansionHarvestParts,EconomyRules.ExpansionHarvestPotatoes),
                new ExpansionOffer(Combo,"module",EconomyRules.ExpansionComboParts,EconomyRules.ExpansionComboPotatoes),
                new ExpansionOffer(Dynamo,"module",EconomyRules.ExpansionDynamoParts,EconomyRules.ExpansionDynamoPotatoes),
                new ExpansionOffer("socket.2","socket",EconomyRules.ExpansionSocket2Parts,0,2),
                new ExpansionOffer("socket.3","socket",EconomyRules.ExpansionSocket3Parts,EconomyRules.ExpansionSocket3Potatoes,3),
                new ExpansionOffer(Amber,"theme",EconomyRules.ExpansionThemeParts,EconomyRules.ExpansionThemePotatoes),
                new ExpansionOffer(Mint,"theme",EconomyRules.ExpansionThemeParts,EconomyRules.ExpansionThemePotatoes),
                new ExpansionOffer(Violet,"theme",EconomyRules.ExpansionThemeParts,EconomyRules.ExpansionThemePotatoes),
                new ExpansionOffer(BonusUnlock,"bonus",EconomyRules.ExpansionBonusParts,EconomyRules.ExpansionBonusPotatoes),
                new ExpansionOffer("potato.yield.1","yield",EconomyRules.ExpansionYield1Parts,EconomyRules.ExpansionYield1Potatoes,1),
                new ExpansionOffer("potato.yield.2","yield",EconomyRules.ExpansionYield2Parts,EconomyRules.ExpansionYield2Potatoes,2),
                new ExpansionOffer("trade.parts","trade",EconomyRules.ExpansionTradeUnits,0,coinsReward:EconomyRules.ExpansionTradeCoins),
                new ExpansionOffer("trade.potatoes","trade",0,EconomyRules.ExpansionTradeUnits,coinsReward:EconomyRules.ExpansionTradeCoins)
            };
            foreach(string family in FamilyIds.All)for(int level=1;level<=2;level++)
                result.Add(new ExpansionOffer("equipment."+family+"."+level,"equipment",level==1?(family==FamilyIds.Workshop?EconomyRules.ExpansionWorkshop1Parts:EconomyRules.ExpansionEquipment1Parts):EconomyRules.ExpansionEquipment2Parts,
                    level==1?(family==FamilyIds.Workshop?EconomyRules.ExpansionWorkshop1Potatoes:EconomyRules.ExpansionEquipment1Potatoes):EconomyRules.ExpansionEquipment2Potatoes,level,family));
            // Append only: the original27 identities, order, costs and effects remain exact.
            result.Add(new ExpansionOffer(Workbench1,"workbench",EconomyRules.ExpansionWorkbench1Parts,EconomyRules.ExpansionWorkbench1Potatoes,1));
            result.Add(new ExpansionOffer(Workbench2,"workbench",EconomyRules.ExpansionWorkbench2Parts,EconomyRules.ExpansionWorkbench2Potatoes,2));
            result.Add(new ExpansionOffer(Frost,"theme",EconomyRules.ExpansionThemeParts,EconomyRules.ExpansionThemePotatoes));
            result.Add(new ExpansionOffer(Rose,"theme",EconomyRules.ExpansionThemeParts,EconomyRules.ExpansionThemePotatoes));
            return result.AsReadOnly();
        }
        public static ExpansionOffer Get(string id)
        {foreach(var offer in offers)if(offer.Id==id)return offer;throw new ArgumentException("Unknown expansion offer",nameof(id));}
        internal static int ModuleIndex(string id)
        {if(id==Salvage)return 0;if(id==Packing)return 1;if(id==Harvest)return 2;if(id==Combo)return 3;if(id==Dynamo)return 4;throw new ArgumentException("Unknown module",nameof(id));}
        internal static int ThemeIndex(string id)
        {if(id==DefaultTheme)return 0;if(id==Amber)return 1;if(id==Mint)return 2;if(id==Violet)return 3;if(id==Frost)return 4;if(id==Rose)return 5;throw new ArgumentException("Unknown theme",nameof(id));}
        internal static bool IsFocus(string id){return id==FocusBalanced||id==FocusParts||id==FocusPotatoes;}
        internal static string[] PresetPriority(string id)
        {
            if(id==PresetParts)return new[]{Salvage,Packing,Combo,Harvest,Dynamo};
            if(id==PresetHarvest)return new[]{Harvest,Combo,Dynamo,Salvage,Packing};
            if(id==PresetEnergy)return new[]{Dynamo,Combo,Salvage,Packing,Harvest};
            throw new ArgumentException("Unknown expansion preset",nameof(id));
        }
        internal static bool RequiresV2(string action,string id)
        {return action=="focus"||action=="preset"||id==Frost||id==Rose||id==Workbench1||id==Workbench2;}
        internal static string RevisionFor(ExpansionState state,string action,string id)
        {return state.FormatVersion>=3?ArcadeRuleRevision:state.FormatVersion==2||RequiresV2(action,id)?NextRuleRevision:RuleRevision;}
        internal static int FamilyIndex(string id)
        {for(int i=0;i<FamilyIds.All.Count;i++)if(FamilyIds.All[i]==id)return i;throw new ArgumentException("Unknown equipment family",nameof(id));}
    }
}
