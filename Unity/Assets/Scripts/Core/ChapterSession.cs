using System;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Core
{
    // Confirmation binds the observed goal, including its historical frozen terms.
    // No committed cancellation history is retained: old requests strictly refuse.
    public sealed class ClearGoalRequest
    {
        public const string Kind="clear-goal";
        public long ExpectedSequence { get; }
        public long Sequence { get { return checked(ExpectedSequence+1); } }
        public string ProjectId { get; }
        public long SavedPrice { get; }
        public string PricingRevision { get; }
        public ClearGoalRequest(long expectedSequence,string projectId,long savedPrice,string pricingRevision)
        {
            if(expectedSequence<0||expectedSequence>=(long.MaxValue-2)/2||savedPrice<=0||
                !JunkToNeon.Content.ConstructionPricing.IsKnown(pricingRevision)||!ChapterCatalog.Rules.TryGetUpgrade(projectId,out _))
                throw new ArgumentException("Invalid goal-clear request");
            ExpectedSequence=expectedSequence;ProjectId=projectId;SavedPrice=savedPrice;PricingRevision=pricingRevision;
        }
    }
    public sealed partial class GameStore
    {
        public ModelState ExecuteChapter(string kind, string action, long sequence, long at = 0, SkillPurchaseRequest request = null,bool manualSpin=false,int tutorialBits=0,ClearGoalRequest clearGoal=null)
        {
            lock(gate)
            {
                if(blocked||state==null)throw new InvalidOperationException("Chapter mutation blocked: "+Error);
                if(kind==ClearGoalRequest.Kind)
                {
                    if(state.pending!=null||request!=null||manualSpin||tutorialBits!=0)throw new InvalidOperationException("Goal clear requires confirmed progress and no unrelated payload");
                    var chapter=ChapterEconomy.Project(state);
                    if(clearGoal==null||chapter.TransitionPending||chapter.SelectedProjectId==null||
                        clearGoal.ExpectedSequence!=state.sequence||clearGoal.Sequence!=sequence||clearGoal.ProjectId!=action||
                        clearGoal.ProjectId!=chapter.SelectedProjectId||clearGoal.PricingRevision!=chapter.ConstructionPriceRevision||
                        clearGoal.SavedPrice!=ChapterEconomy.SelectedProjectPrice(chapter))
                        throw new InvalidOperationException("Goal-clear confirmation changed or is stale");
                }
                else if(clearGoal!=null)throw new InvalidOperationException("Unexpected goal-clear request");
                var pending=JourneyEconomy.ReserveChapter(state,kind,action,sequence,at,request,manualSpin,tutorialBits);
                if(ReferenceEquals(pending,state))return state.Copy();
                try { Write(pending);Write(GameLogic.ApplyPending(pending));return state.Copy(); }
                catch(Exception error){blocked=true;Error=error.Message;throw;}
            }
        }
        internal ClearGoalRequest ReadClearGoalRequest()
        {
            lock(gate)
            {
                if(blocked||state==null||state.pending!=null)throw new InvalidOperationException("Goal clear requires a confirmed healthy snapshot");
                var chapter=ChapterEconomy.Project(state);
                if(chapter.TransitionPending||chapter.SelectedProjectId==null)throw new InvalidOperationException("No actionable saved goal");
                return new ClearGoalRequest(state.sequence,chapter.SelectedProjectId,ChapterEconomy.SelectedProjectPrice(chapter),chapter.ConstructionPriceRevision);
            }
        }
    }
    public sealed partial class GameSession
    {
        public ClearGoalRequest QuoteClearGoal() { return store.ReadClearGoalRequest(); }
        public ModelState ClearGoal(ClearGoalRequest request,long at=0)
        { return store.ExecuteChapter(ClearGoalRequest.Kind,request?.ProjectId,request?.Sequence??0,at,clearGoal:request); }
        public ChapterState Chapter { get { return ChapterEconomy.Project(store.State); } }
        public ModelState ExecuteChapterSpin(long sequence,long at=0,bool manualSpin=true,int tutorialBits=0) { return store.ExecuteChapter("spin",null,sequence,at,manualSpin:manualSpin,tutorialBits:tutorialBits); }
        public ModelState SelectChapterProject(string projectId,long sequence,long at=0,int tutorialBits=0) { return store.ExecuteChapter("select",projectId,sequence,at,tutorialBits:tutorialBits); }
        public ModelState BuySkill(SkillPurchaseRequest request,long at=0) { return store.ExecuteChapter("skill",request?.NodeId,request?.Sequence??0,at,request); }
        public ModelState EnterWorkshopYard(long sequence,long at=0) { return store.ExecuteChapter("enter-era",null,sequence,at); }
        public ModelState AcknowledgeEraTransition(long sequence,long at=0) { return store.ExecuteChapter("era-seen",null,sequence,at); }
    }
}
