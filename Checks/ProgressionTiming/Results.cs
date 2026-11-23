using System;
using System.Collections.Generic;
using System.Linq;

namespace ProductionTiming
{
    public sealed class Event
    {
        public string Kind { get; set; }
        public string Id { get; set; }
        public int Spin { get; set; }
        public long Price { get; set; }
        public long Wallet { get; set; }
        public double NominalAutoSeconds { get; set; }
    }
    public sealed class Milestone
    {
        public int Spins { get; set; }
        public double ManualSeconds { get; set; }
        public double NominalAutoSeconds { get; set; }
        public int ExplicitDecisions { get; set; }
        public double AutoPlusTwoSecondsPerDecision { get; set; }
        public long Wallet { get; set; }
    }
    public sealed class OrderResult
    {
        public string Id { get; set; }
        public int Instance { get; set; }
        public int Spins { get; set; }
        public double NominalManualSeconds { get; set; }
        public long SpinIncome { get; set; }
        public long ClaimIncome { get; set; }
        public int CarryBefore { get; set; }
        public int CarryAfter { get; set; }
    }
    public sealed class RunResult
    {
        public uint Seed { get; set; }
        public string Policy { get; set; }
        public string Scenario { get; set; }
        public string Status { get; set; }
        public int Spins { get; set; }
        public int Writes { get; set; }
        public long SerializedCharacters { get; set; }
        public long FreshGrant { get; set; }
        public long SpinIncome { get; set; }
        public long OfflineIncome { get; set; }
        public long AbsenceSeconds { get; set; }
        public long ConstructionSpend { get; set; }
        public long SkillSpend { get; set; }
        public long WalletAfterSkills { get; set; }
        public int SkillsOwnedAtChapterEnd { get; set; }
        public long SkillSpendAtChapterEnd { get; set; }
        public int ConstructionCount { get; set; }
        public int LongestConstructionGapSpins { get; set; }
        public double LongestConstructionGapNominalAutoSeconds { get; set; }
        public double RunnerWallSeconds { get; set; }
        public long PeakManagedBytes { get; set; }
        public Dictionary<string,Milestone> Milestones { get; set; } = new Dictionary<string,Milestone>();
        public List<Event> Purchases { get; set; } = new List<Event>();
        public List<OrderResult> Orders { get; set; } = new List<OrderResult>();
    }
    public sealed class Distribution
    {
        public int Count { get; set; }
        public double Minimum { get; set; }
        public double Median { get; set; }
        public double P90NearestRank { get; set; }
        public double P95NearestRank { get; set; }
        public double Maximum { get; set; }
        public static Distribution Of(IEnumerable<double> values)
        {
            var a=values.OrderBy(x=>x).ToArray();if(a.Length==0)return new Distribution();
            return new Distribution{Count=a.Length,Minimum=a[0],Median=a.Length%2==0?(a[a.Length/2-1]+a[a.Length/2])/2:a[a.Length/2],P90NearestRank=a[(int)Math.Ceiling(.90*a.Length)-1],P95NearestRank=a[(int)Math.Ceiling(.95*a.Length)-1],Maximum=a[a.Length-1]};
        }
    }
}
