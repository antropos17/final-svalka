using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JunkToNeon.Contracts;

namespace JunkToNeon.Content
{
    // Pure view of validated legacy v1 fields. No new grant, save rewrite or pending replay.
    public sealed class LegacyProgress
    {
        public ReadOnlyCollection<string> CompletedUpgradeIds { get; }
        public string SelectedUpgradeId { get; }
        LegacyProgress(List<string> completed, string selected)
        { CompletedUpgradeIds = completed.AsReadOnly(); SelectedUpgradeId = selected; }

        public static LegacyProgress Project(int houseStage, int workshopBuilt = 0, int selectedStage = 0, int legacyContentVersion = 1)
        {
            if (legacyContentVersion != 1 || houseStage < 0 || houseStage > 2 || workshopBuilt < 0 || workshopBuilt > 1 ||
                (workshopBuilt == 1 && houseStage < 1)) throw new ArgumentException("Unsupported or invalid legacy progress.");
            string selected = null;
            if (selectedStage == 1 && houseStage == 0) selected = GameIds.HouseRepair;
            else if (selectedStage == 2 && houseStage == 1) selected = GameIds.HouseImprove;
            else if (selectedStage == 3 && houseStage >= 1 && workshopBuilt == 0) selected = GameIds.WorkshopBuild;
            else if (selectedStage != 0) throw new ArgumentException("Unknown or unavailable legacy target.");
            var completed = new List<string>();
            if (houseStage >= 1) completed.Add(GameIds.HouseRepair);
            if (houseStage >= 2) completed.Add(GameIds.HouseImprove);
            if (workshopBuilt == 1) completed.Add(GameIds.WorkshopBuild);
            return new LegacyProgress(completed, selected);
        }
    }
}
