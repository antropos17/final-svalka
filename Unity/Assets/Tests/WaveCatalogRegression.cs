using System;
using System.Collections.Generic;
using JunkToNeon.Core;

namespace JunkToNeon.Tests
{
    public static class WaveCatalogRegression
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Fails<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception(message);
        }
        static bool Has(IReadOnlyList<string> values, string id)
        {
            foreach (var value in values) if (value == id) return true;
            return false;
        }

        public static IReadOnlyList<string> Run()
        {
            string[] identities = { "house.repair", "house.improve", "workshop.build", "recycler.build", "pigeon-loft.build" };
            long[] prices = { 40, 600, 120, 180, 240 };
            for (int target = 1; target <= identities.Length; target++)
            {
                Require(ProgressionCatalog.Id(target) == identities[target - 1], "Persistent target mapping changed");
                Require(ProgressionCatalog.Price(target) == prices[target - 1], "Active price differs from the approved prototype");
            }
            foreach (int target in new[] { -1, 0, 6 })
            {
                Fails<ArgumentOutOfRangeException>(() => ProgressionCatalog.Id(target), "Unknown project ID accepted");
                Fails<ArgumentOutOfRangeException>(() => ProgressionCatalog.Price(target), "Unknown project price accepted");
                Require(!ProgressionCatalog.Available(target, 0, 0, 0, 0), "Unknown project became available");
            }

            for (int house = 0; house <= 2; house++)
                for (int workshop = 0; workshop <= 1; workshop++)
                    for (int recycler = 0; recycler <= 1; recycler++)
                        for (int pigeon = 0; pigeon <= 1; pigeon++)
                        {
                            int h = house, w = workshop, r = recycler, p = pigeon;
                            if (h == 0 && (w != 0 || r != 0 || p != 0))
                            {
                                Fails<ArgumentException>(() => ProgressionCatalog.Completed(h, w, r, p), "Unrepaired ownership accepted");
                                continue;
                            }
                            var completed = ProgressionCatalog.Completed(h, w, r, p);
                            Require(completed.Count == h + w + r + p, "Completed project count or disabled content leaked");
                            Require(Has(completed, identities[0]) == (h >= 1) && Has(completed, identities[1]) == (h == 2) &&
                                Has(completed, identities[2]) == (w == 1) && Has(completed, identities[3]) == (r == 1) &&
                                Has(completed, identities[4]) == (p == 1), "Completed stable IDs differ from scalar progress");
                            Require(ProgressionCatalog.Available(1, h, w, r, p) == (h == 0) &&
                                ProgressionCatalog.Available(2, h, w, r, p) == (h == 1) &&
                                ProgressionCatalog.Available(3, h, w, r, p) == (h >= 1 && w == 0) &&
                                ProgressionCatalog.Available(4, h, w, r, p) == (h >= 1 && r == 0) &&
                                ProgressionCatalog.Available(5, h, w, r, p) == (h >= 1 && p == 0), "Available projects violate explicit prerequisites");
                            Require(ProgressionCatalog.Complete(h, w, r, p) == (h == 2 && w == 1 && r == 1 && p == 1),
                                "Completion omitted a family or required an optional slot upgrade");
                        }
            foreach (int[] invalid in new[] { new[] { -1, 0, 0, 0 }, new[] { 3, 0, 0, 0 }, new[] { 1, -1, 0, 0 },
                new[] { 1, 2, 0, 0 }, new[] { 1, 0, -1, 0 }, new[] { 1, 0, 2, 0 }, new[] { 1, 0, 0, -1 }, new[] { 1, 0, 0, 2 } })
            {
                Fails<ArgumentException>(() => ProgressionCatalog.Completed(invalid[0], invalid[1], invalid[2], invalid[3]), "Out-of-range progress accepted");
                Fails<ArgumentException>(() => ProgressionCatalog.Complete(invalid[0], invalid[1], invalid[2], invalid[3]), "Invalid progress completed");
                Fails<ArgumentException>(() => ProgressionCatalog.Available(0, invalid[0], invalid[1], invalid[2], invalid[3]), "Unknown target concealed invalid progress");
            }

            var keys = ProgressionCatalog.RequiredAssetKeys;
            Require(keys.Count == 3 && keys[0] == "Editable/HouseStage0" && keys[1] == "Editable/HouseStage1" && keys[2] == "Editable/HouseStage2",
                "Required baseline asset keys changed");
            // These are synthetic registry fixtures, not observed Resources/art acceptance.
            ProgressionCatalog.ValidateResources(new[] { "Editable/HouseStage2", "Editable/HouseStage0", "Editable/HouseStage1", "unrelated" });
            Fails<ArgumentNullException>(() => ProgressionCatalog.ValidateResources(null), "Null resource registry accepted");
            for (int omitted = 0; omitted < keys.Count; omitted++)
            {
                var registry = new List<string>();
                for (int index = 0; index < keys.Count; index++) if (index != omitted) registry.Add(keys[index]);
                Fails<ArgumentException>(() => ProgressionCatalog.ValidateResources(registry), "Missing house resource accepted");
            }
            Fails<NotSupportedException>(() => ((IList<string>)keys)[0] = "changed", "Required asset keys are mutable");
            var all = ProgressionCatalog.Completed(2, 1, 1, 1);
            Fails<NotSupportedException>(() => ((IList<string>)all).Add("slot.coin-boost"), "Completed project projection is mutable");
            Require(ProgressionCatalog.Complete(2, 1, 1, 1) && !Has(all, "slot.coin-boost"), "Slot upgrade became a completion requirement");

            return new[] {
                "Active target IDs1..5 and independent approved prices40/600/120/180/240; unknown targets rejected",
                "All valid four-family progress combinations, explicit prerequisites and completion excluding optional upgrades",
                "Invalid scalar progress, immutable projections and synthetic missing-resource registry rejection (not art acceptance)"
            };
        }
    }
}
