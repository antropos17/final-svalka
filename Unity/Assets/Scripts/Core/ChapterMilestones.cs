using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JunkToNeon.Contracts;

namespace JunkToNeon.Core
{
    public enum MilestoneNextAction { InspectHouse, InspectSelectedProject, ChooseConstruction, EnterWorkshopYard, ContinueEraEntry, FinishOrContinueOptionally }

    public sealed class MilestoneDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public string Fact { get; }
        internal MilestoneDefinition(string id, string title, string fact) { Id=id;Title=title;Fact=fact; }
    }

    public sealed class MilestoneProgress
    {
        public MilestoneDefinition Definition { get; }
        public bool Reached { get; }
        internal MilestoneProgress(MilestoneDefinition definition, bool reached) { Definition=definition;Reached=reached; }
    }

    // Current-save facts only: no earned time, lifetime history, reward or acknowledgement.
    public sealed class ChapterMilestoneSnapshot
    {
        public ReadOnlyCollection<MilestoneProgress> Milestones { get; }
        public int ReachedCount { get; }
        public int ConstructionCount { get; }
        public int TotalConstructions { get { return 24; } }
        public int OwnedSkillCount { get; }
        public MilestoneNextAction NextAction { get; }
        public string SelectedProjectId { get; }
        internal ChapterMilestoneSnapshot(MilestoneProgress[] milestones, int constructions, int skills, MilestoneNextAction nextAction, string selected)
        {
            Milestones=Array.AsReadOnly(milestones);ConstructionCount=constructions;OwnedSkillCount=skills;NextAction=nextAction;SelectedProjectId=selected;
            int reached=0;foreach(var milestone in milestones)if(milestone.Reached)reached++;ReachedCount=reached;
        }
    }

    public static class ChapterMilestones
    {
        public const string HouseRepaired="milestone.house-repaired", WorkshopBuilt="milestone.workshop-built", JunkyardRestored="milestone.junkyard-restored", ChapterRestored="milestone.chapter-restored";
        static readonly ReadOnlyCollection<MilestoneDefinition> catalog=Array.AsReadOnly(new[] {
            new MilestoneDefinition(HouseRepaired,"House repaired","The current save contains the house repair."),
            new MilestoneDefinition(WorkshopBuilt,"Workshop built","The current save contains the workshop."),
            new MilestoneDefinition(JunkyardRestored,"Junkyard restored","All twelve Junkyard construction stages are present, including after era entry."),
            new MilestoneDefinition(ChapterRestored,"Both yards restored","All twenty-four construction stages are present; optional skills are not required.")
        });
        public static IReadOnlyList<MilestoneDefinition> Catalog { get { return catalog; } }

        // On-demand projection of a validated immutable ChapterState. Never called by Render/Update/Canvas.
        public static ChapterMilestoneSnapshot Project(ChapterState chapter)
        {
            if(chapter==null)throw new ArgumentNullException(nameof(chapter));
            var stages=ChapterCatalog.Rules.Stages(chapter.Progress);
            bool workshopYard=stages.EraId==EraIds.WorkshopYard,allJunkyard=true,allFinal=true;
            foreach(var stage in stages.Entries){allJunkyard&=stage.AbsoluteLevel==2;allFinal&=stage.AbsoluteLevel==5;}
            bool repaired=stages.AbsoluteLevel(FamilyIds.House)>=1;
            bool restored=workshopYard||allJunkyard,complete=workshopYard&&allFinal;
            var facts=new[] {repaired,stages.AbsoluteLevel(FamilyIds.Workshop)>=1,restored,complete};
            var progress=new MilestoneProgress[catalog.Count];
            for(int i=0;i<progress.Length;i++)progress[i]=new MilestoneProgress(catalog[i],facts[i]);
            var action=chapter.TransitionPending?MilestoneNextAction.ContinueEraEntry:
                complete?MilestoneNextAction.FinishOrContinueOptionally:
                !workshopYard&&allJunkyard?MilestoneNextAction.EnterWorkshopYard:
                chapter.SelectedProjectId!=null?MilestoneNextAction.InspectSelectedProject:
                !repaired?MilestoneNextAction.InspectHouse:MilestoneNextAction.ChooseConstruction;
            // Count owned purchases, not absolute levels: free2->3 era entry stays12/24.
            return new ChapterMilestoneSnapshot(progress,chapter.Progress.CompletedUpgradeIds.Count,chapter.Skills.Receipts.Count,action,chapter.SelectedProjectId);
        }
    }
}
