using System.Collections.Generic;

namespace ExpansionPacing
{
    internal sealed class Purchase
    {
        public string Kind { get; set; }
        public string Id { get; set; }
        public int Spin { get; set; }
        public long Price { get; set; }
        public long Wallet { get; set; }
        public int Gap { get; set; }
    }
    internal sealed class Checkpoint
    {
        public int Spin { get; set; }
        public long Wallet { get; set; }
        public int Constructions { get; set; }
        public int Skills { get; set; }
        public string Selected { get; set; }
        public long SelectedPrice { get; set; }
        public long SpinIncome { get; set; }
        public long ExchangeIncome { get; set; }
        public object Expansion { get; set; }
    }
    internal sealed class RunResult
    {
        public uint Seed { get; set; }
        public string Policy { get; set; }
        public string Status { get; set; } = "running";
        public int Spins { get; set; }
        public int EraReadySpin { get; set; }
        public int EraEnteredSpin { get; set; }
        public int ChapterSpin { get; set; }
        public int FirstRepairSpin { get; set; }
        public double ProjectedNormalManualSeconds { get; set; }
        public double ProjectedFastManualSeconds { get; set; }
        public double ProjectedNormalAutoSeconds { get; set; }
        public double ProjectedFastAutoSeconds { get; set; }
        public int MaximumConstructionGap { get; set; }
        public int FirstOptionalSkillSpin { get; set; } = -1;
        public int ConstructionCount { get; set; }
        public long ConstructionSpend { get; set; }
        public long SkillSpend { get; set; }
        public long SpinIncome { get; set; }
        public long ExchangeIncome { get; set; }
        public long Wallet { get; set; }
        public int Skills { get; set; }
        public int Writes { get; set; }
        public uint FinalSeed { get; set; }
        public string FinalStateHash { get; set; }
        public double WallSeconds { get; set; }
        public List<Purchase> Purchases { get; set; } = new List<Purchase>();
        public List<Checkpoint> Checkpoints { get; set; } = new List<Checkpoint>();
        public object Expansion { get; set; }
        public int FirstUsefulShopAffordabilitySpin { get; set; } = -1;
        public int FirstUsefulShopPurchaseSpin { get; set; } = -1;
        public int FirstPotatoSpendSpin { get; set; } = -1;
        public int FirstBoostAffordabilitySpin { get; set; } = -1;
        public int UsefulShopPurchasesBy50 { get; set; }
        public int BoostArms { get; set; }
        public int BoostConsumptions { get; set; }
        public List<ExpansionEvent> ExpansionEvents { get; set; } = new List<ExpansionEvent>();
        public List<object> ExpansionSpinTrace { get; set; } = new List<object>();
    }
    internal sealed class ExpansionEvent
    {
        public string Action { get; set; }
        public string Id { get; set; }
        public int Spin { get; set; }
        public long PartsCost { get; set; }
        public long PotatoesCost { get; set; }
        public long EnergyCost { get; set; }
        public long CoinsCost { get; set; }
        public long CoinsReward { get; set; }
    }
}
