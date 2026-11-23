using System.Linq;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Guidance;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        // Pure projections of an already confirmed chapter; never a session/frame query.
        static int RemainingPaidStages(ChapterState chapter)
        {
            var era=ChapterCatalog.Rules.Definition.Eras.Single(entry=>entry.Id==chapter.Progress.EraId);
            return era.Catalog.Upgrades.Count(upgrade=>upgrade.Price.HasValue&&upgrade.Price.Value>0&&!chapter.Progress.CompletedUpgradeIds.Contains(upgrade.Id));
        }
        static string EraProgressSummary(ChapterState chapter)
        {
            int remaining=RemainingPaidStages(chapter);
            if(chapter.Progress.EraId==EraIds.Junkyard&&remaining==0)return "Junkyard complete: free entry to Workshop Yard. 12/24.";
            if(chapter.Progress.EraId==EraIds.WorkshopYard&&ChapterCatalog.Rules.IsChapterComplete(chapter.Progress))return "Chapter complete: 24/24. Continue if you wish.";
            string units=remaining==1?"building stage":"building stages";
            return chapter.Progress.EraId==EraIds.Junkyard?$"{remaining} {units} until Workshop Yard.":
                $"Chapter needs {remaining} {units}. Buildings {chapter.Progress.CompletedUpgradeIds.Count}/24.";
        }
        static string FinalCompletionPayoff(ChapterState chapter,ProjectCompletion completion)
        {
            if(chapter==null||completion==null||!ChapterCatalog.Rules.IsChapterComplete(chapter.Progress))return "";
            var era=ChapterCatalog.Rules.Definition.Eras.Single(entry=>entry.Id==chapter.Progress.EraId);
            return era.Catalog.CompletionUpgradeIds.Contains(completion.ProjectId)?ChapterNarrative.CompletionPayoff:"";
        }
        static string FinalConstructionPayoff(ModelState before,ModelState committed,string project)
        {
            if(before==null||committed==null||project==null||committed.sequence!=before.sequence+1)return "";
            var previous=ChapterEconomy.Project(before);var chapter=ChapterEconomy.Project(committed);
            if(previous.Progress.CompletedUpgradeIds.Count!=23||chapter.Progress.CompletedUpgradeIds.Count!=24||
                previous.Progress.CompletedUpgradeIds.Contains(project)||!chapter.Progress.CompletedUpgradeIds.Contains(project)||
                ChapterCatalog.Rules.IsChapterComplete(previous.Progress))return "";
            var completion=JourneyEconomy.Project(committed).Guidance.LastCompletion;
            return completion?.Sequence==committed.sequence&&completion.ProjectId==project?FinalCompletionPayoff(chapter,completion):"";
        }
        // Existing fact/advice callers below are explicit explanation opens.
        static string MilestoneFact(ChapterMilestoneSnapshot snapshot,string id)
        {
            foreach(var entry in snapshot.Milestones)if(entry.Definition.Id==id)return entry.Reached?"yes":"no";
            throw new System.InvalidOperationException("Missing fixed milestone identity");
        }
        string MilestoneFacts(ChapterMilestoneSnapshot snapshot)
        {return MilestoneFacts(snapshot,Session.Chapter,Session.OptionalAchievements);}
        static string MilestoneFacts(ChapterMilestoneSnapshot snapshot,ChapterState chapter,OptionalAchievementSnapshot optional)
        {
            return $"Buildings: {snapshot.ConstructionCount}/24.\n"+EraProgressSummary(chapter)+$"\nMilestones: house repaired {MilestoneFact(snapshot,ChapterMilestones.HouseRepaired)}, Workshop {MilestoneFact(snapshot,ChapterMilestones.WorkshopBuilt)}, Junkyard {MilestoneFact(snapshot,ChapterMilestones.JunkyardRestored)}, chapter {MilestoneFact(snapshot,ChapterMilestones.ChapterRestored)}."+EarnedOptionalAchievements(optional);
        }
        static string EarnedOptionalAchievements(OptionalAchievementSnapshot optional)
        {
            var earned=new System.Collections.Generic.List<string>();
            foreach(var achievement in optional.Achievements)
                if(achievement.Reached)earned.Add(achievement.Definition.Id==Core.OptionalAchievements.AllSkillsOwned?"all skills owned":"order reward collected");
            return earned.Count==0?"":"\nIn this save: "+string.Join("; ",earned)+".";
        }
        string MilestoneAdvice(ChapterMilestoneSnapshot snapshot)
        {
            if(snapshot.NextAction!=MilestoneNextAction.InspectSelectedProject)return MilestoneAdvice(snapshot,null,null);
            var state=Session.State;return MilestoneAdvice(snapshot,state,ChapterEconomy.Project(state));
        }
        static string MilestoneAdvice(ChapterMilestoneSnapshot snapshot,ModelState state,ChapterState chapter)
        {
            switch(snapshot.NextAction)
            {
                case MilestoneNextAction.ContinueEraEntry:return "Era entry saved. Continue it first.";
                case MilestoneNextAction.EnterWorkshopYard:return "Next: enter Workshop Yard for free.";
                case MilestoneNextAction.FinishOrContinueOptionally:return "You may finish here; orders and skills are optional.";
                case MilestoneNextAction.InspectHouse:return "Next: inspect the house; select a project explicitly.";
                case MilestoneNextAction.InspectSelectedProject:
                    return $"Goal: {ProjectName(snapshot.SelectedProjectId)} · {state.coins}/{ChapterEconomy.SelectedProjectPrice(chapter)} at its saved price.\nInspect it; reselect only explicitly.";
                default:return "Next: choose one building.";
            }
        }
        string ChapterEndMilestones()
        {
            var snapshot=Session.Milestones;
            return Guidance.ChapterNarrative.ChapterEnd+"\n"+MilestoneFacts(snapshot)+"\n"+MilestoneAdvice(snapshot)+$" Skills: {snapshot.OwnedSkillCount}/6; no further era here.";
        }
    }
}
