using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JunkToNeon.Contracts
{
    public static class EraIds
    {
        public const string Junkyard = "junkyard", WorkshopYard = "workshop-yard";
        public static bool IsKnown(string id) { return id == Junkyard || id == WorkshopYard; }
    }
    public static class FamilyIds
    {
        public const string House = "house", Workshop = "workshop", Recycler = "recycler", PigeonLoft = "pigeon-loft", Cart = "cart", PowerSource = "power-source";
        static readonly ReadOnlyCollection<string> all = Array.AsReadOnly(new[] { House, Workshop, Recycler, PigeonLoft, Cart, PowerSource });
        public static IReadOnlyList<string> All { get { return all; } }
        public static bool IsKnown(string id) { foreach (var item in all) if (item == id) return true; return false; }
    }
    public static class SkillIds
    {
        // Keeping the original identity avoids a second charge or a second +1 source.
        public const string Universal1 = GameIds.SlotCoinBoost, Universal2 = "skill.universal.2";
        public const string Recycling1 = "skill.recycling.1", Recycling2 = "skill.recycling.2";
        public const string Delivery1 = "skill.delivery.1", Delivery2 = "skill.delivery.2";
    }
    public static class ChapterIds
    {
        public const string UnlockWorkshopYard = "era.workshop-yard.unlock";
        public static string Stage(string eraId, string familyId, int localLevel)
        {
            Validate(eraId, familyId, localLevel, 0);
            return familyId + (eraId == EraIds.Junkyard ? ".stage." : ".workshop-yard.stage.") + localLevel.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        public static string Project(string eraId, string familyId, int toLocalLevel)
        {
            Validate(eraId, familyId, toLocalLevel, 1);
            if (eraId == EraIds.WorkshopYard) return familyId + ".workshop-yard.upgrade-" + toLocalLevel.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (familyId == FamilyIds.House) return toLocalLevel == 1 ? GameIds.HouseRepair : GameIds.HouseImprove;
            return familyId + (toLocalLevel == 1 ? ".build" : ".improve");
        }
        public static bool IsProject(string id, string eraId)
        {
            if (!EraIds.IsKnown(eraId) || id == null) return false;
            foreach (var family in FamilyIds.All) for (int level = 1; level <= 2; level++)
                if (id == Project(eraId, family, level)) return true;
            return false;
        }
        static void Validate(string eraId, string familyId, int level, int minimum)
        {
            if (!EraIds.IsKnown(eraId) || !FamilyIds.IsKnown(familyId) || level < minimum || level > 2)
                throw new ArgumentException("Unknown era/family/stage identity");
        }
    }
    public sealed class FamilyStage
    {
        public string FamilyId { get; }
        public int AbsoluteLevel { get; }
        public int LocalLevel { get { return AbsoluteLevel % 3; } }
        public FamilyStage(string familyId, int absoluteLevel)
        {
            if (!FamilyIds.IsKnown(familyId) || absoluteLevel < 0 || absoluteLevel > 5) throw new ArgumentException("Invalid family stage");
            FamilyId = familyId; AbsoluteLevel = absoluteLevel;
        }
    }
    // Exactly six same world objects. Levels0..2 are Junkyard;3..5 are Workshop Yard.
    // Era entry advances2->3 without deleting prior ownership/history or granting coins.
    public sealed class FamilyStages
    {
        readonly ReadOnlyCollection<FamilyStage> entries;
        public string EraId { get; }
        public IReadOnlyList<FamilyStage> Entries { get { return entries; } }
        public FamilyStages(string eraId, IEnumerable<FamilyStage> values)
        {
            if (!EraIds.IsKnown(eraId) || values == null) throw new ArgumentException("Invalid stage snapshot");
            var found = new Dictionary<string, FamilyStage>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (found.Count >= 6 || value == null || found.ContainsKey(value.FamilyId)) throw new ArgumentException("Unknown/duplicate/oversized family snapshot");
                if (eraId == EraIds.Junkyard ? value.AbsoluteLevel > 2 : value.AbsoluteLevel < 3) throw new ArgumentException("Stage belongs to another era");
                found.Add(value.FamilyId, value);
            }
            if (found.Count != 6) throw new ArgumentException("Expected all six family stages");
            var ordered = new List<FamilyStage>(6);
            foreach (var id in FamilyIds.All) ordered.Add(found[id]);
            EraId = eraId; entries = ordered.AsReadOnly();
        }
        public int AbsoluteLevel(string familyId)
        {
            foreach (var value in entries) if (value.FamilyId == familyId) return value.AbsoluteLevel;
            throw new ArgumentException("Unknown family", nameof(familyId));
        }
        public int LocalLevel(string familyId) { return AbsoluteLevel(familyId) % 3; }
    }
}
