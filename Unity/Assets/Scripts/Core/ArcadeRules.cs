using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JunkToNeon.Core
{
    public static partial class EconomyRules
    {
        public const int ArcadeTicketCadence=8, ArcadeTicketCap=3, ArcadeStartTickets=1;
        public const int ArcadeMemoryAttempts=9, ArcadeSortingItems=8, ArcadePackUnits=5;
        public const int ArcadeSlot1Parts=20, ArcadeSlot2Potatoes=20, ArcadeSlot3Parts=30, ArcadeSlot3Potatoes=30;
        public const long ArcadeSlot1Diamonds=2, ArcadeSlot2Diamonds=4, ArcadeSlot3Diamonds=7, ArcadePackDiamonds=1, ArcadeBoostDiamonds=1;
        public const uint ArcadeInitialSeed=1831565813U;
        public static int ArcadeMemoryReward(int attempts,bool solved)
        {if(attempts<0||attempts>9)throw new ArgumentOutOfRangeException(nameof(attempts));return !solved?0:attempts<=4?3:attempts<=6?2:1;}
        public static int ArcadeSortingReward(int correct)
        {if(correct<0||correct>8)throw new ArgumentOutOfRangeException(nameof(correct));return correct>=7?3:correct>=5?2:correct>=3?1:0;}
    }
    public sealed class ArcadeSlotAttributes
    {
        public int Level {get;}
        public int BoxPresencePartBonus {get;} public int PotatoPresencePotatoBonus {get;}
        public int RawBonusPartsAddition {get;} public int RawBonusPotatoesAddition {get;}
        internal ArcadeSlotAttributes(int level)
        {
            Level=level;BoxPresencePartBonus=level>=1?1:0;PotatoPresencePotatoBonus=level>=2?1:0;
            RawBonusPartsAddition=level>=3?1:0;RawBonusPotatoesAddition=level>=3?1:0;
        }
    }
    public sealed class ArcadeOffer
    {
        public string Id { get; } public string Kind { get; } public int Level { get; }
        public int PartsCost { get; } public int PotatoesCost { get; } public long DiamondsCost { get; }
        public int PartsCredit { get; } public int PotatoesCredit { get; } public bool ArmsBonusBoost { get; }
        internal ArcadeOffer(string id,string kind,int level,int parts,int potatoes,long diamonds,int addParts=0,int addPotatoes=0,bool boost=false)
        {Id=id;Kind=kind;Level=level;PartsCost=parts;PotatoesCost=potatoes;DiamondsCost=diamonds;PartsCredit=addParts;PotatoesCredit=addPotatoes;ArmsBonusBoost=boost;}
    }
    public static class ArcadeCatalog
    {
        public const string RuleRevision="arcade.rules.v1";
        public const string Memory="arcade.memory", Sorting="arcade.sorting";
        public const string Slot1="arcade.slot.1", Slot2="arcade.slot.2", Slot3="arcade.slot.3";
        public const string PartsPack="arcade.pack.parts", PotatoesPack="arcade.pack.potatoes", BonusBoost="arcade.boost.next-bonus";
        static readonly ArcadeSlotAttributes[] slotAttributes={new ArcadeSlotAttributes(0),new ArcadeSlotAttributes(1),new ArcadeSlotAttributes(2),new ArcadeSlotAttributes(3)};
        public static ArcadeSlotAttributes SlotAttributes(int level)
        {if(level<0||level>=slotAttributes.Length)throw new ArgumentOutOfRangeException(nameof(level));return slotAttributes[level];}
        static readonly ReadOnlyCollection<ArcadeOffer> offers=Array.AsReadOnly(new[]{
            new ArcadeOffer(Slot1,"slot",1,EconomyRules.ArcadeSlot1Parts,0,EconomyRules.ArcadeSlot1Diamonds),
            new ArcadeOffer(Slot2,"slot",2,0,EconomyRules.ArcadeSlot2Potatoes,EconomyRules.ArcadeSlot2Diamonds),
            new ArcadeOffer(Slot3,"slot",3,EconomyRules.ArcadeSlot3Parts,EconomyRules.ArcadeSlot3Potatoes,EconomyRules.ArcadeSlot3Diamonds),
            new ArcadeOffer(PartsPack,"pack",0,0,0,EconomyRules.ArcadePackDiamonds,addParts:EconomyRules.ArcadePackUnits),
            new ArcadeOffer(PotatoesPack,"pack",0,0,0,EconomyRules.ArcadePackDiamonds,addPotatoes:EconomyRules.ArcadePackUnits),
            new ArcadeOffer(BonusBoost,"boost",0,0,0,EconomyRules.ArcadeBoostDiamonds,boost:true)});
        public static IReadOnlyList<ArcadeOffer> Offers {get{return offers;}}
        public static ArcadeOffer Get(string id){foreach(var offer in offers)if(offer.Id==id)return offer;throw new ArgumentException("Unknown Arcade offer",nameof(id));}
        internal static bool Game(string id){return id==Memory||id==Sorting;}
        internal static bool Action(string action){return action=="buy"||action=="start"||action=="pick"||action=="continue"||action=="sort"||action=="claim"||action=="abandon";}
        internal static uint Next(uint seed){if(seed==0)throw new ArgumentException("Zero Arcade seed");seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return seed;}
        internal static int[] Shuffle(string id,uint seed,out uint after)
        {
            if(!Game(id))throw new ArgumentException("Unknown Arcade game");
            int[] board=id==Memory?new[]{0,0,1,1,2,2}:new[]{0,0,1,1,2,2,3,3};
            for(int i=board.Length-1;i>0;i--){seed=Next(seed);int j=(int)(seed%(uint)(i+1));int value=board[i];board[i]=board[j];board[j]=value;}
            after=seed;return board;
        }
    }
}
