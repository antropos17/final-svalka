using System;
using System.Collections.Generic;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Diagnostics;
using UnityEngine;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        readonly LocalDiagnosticRing localDiagnostics = new LocalDiagnosticRing(new BuildIdentity("unity-r10-local-diagnostics"));
        public IReadOnlyList<DiagnosticRecord> LocalDiagnostics { get { return localDiagnostics.Snapshot(); } }

        void StartDiagnostics() { Application.logMessageReceived += ObserveUnityLog; }
        void StopDiagnostics() { Application.logMessageReceived -= ObserveUnityLog; }
        long DiagnosticSequence { get { return Session?.State?.sequence ?? 0; } }
        void ObserveUnityLog(string unusedMessage, string unusedStack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            try { AnalyticsBoundary.TryRecord(localDiagnostics, LocalEvent.RuntimeError(DiagnosticSequence,
                type == LogType.Exception ? RuntimeErrorCode.ManagedException : RuntimeErrorCode.UnityError)); }
            catch (Exception) { /* Optional diagnostics must never log their own failure. */ }
        }
        void ObserveRequiredResourceFailure()
        {
            try { AnalyticsBoundary.TryRecord(localDiagnostics, LocalEvent.MissingResource(DiagnosticSequence, ResourceKind.OtherRequiredResource)); }
            catch (Exception) { }
        }
        void ObserveOperationFailure()
        {
            try
            {
                // The existing store exposes a blocked/uncertain result, not which
                // write failed. Do not invent reservation-versus-commit precision.
                var value = Session?.RecoveryRequired == true
                    ? LocalEvent.PersistenceError(DiagnosticSequence, PersistenceErrorCode.RecoveryBlocked)
                    : LocalEvent.RuntimeError(DiagnosticSequence, RuntimeErrorCode.UnexpectedRuntimeState);
                AnalyticsBoundary.TryRecord(localDiagnostics, value);
            }
            catch (Exception) { }
        }
        void ObserveCommitted(ModelState before, ModelState after, string selectedProject = null)
        {
            // Only a successful NEW command reaches this observer. Save opening,
            // recovered history, exact replay, clocks and rendered frames are silent.
            try
            {
                if (before == null || after == null || after.pending != null || after.sequence <= before.sequence) return;
                var old = JourneyEconomy.Project(before); var next = JourneyEconomy.Project(after);
                if (selectedProject != null)
                    AnalyticsBoundary.TryRecord(localDiagnostics, LocalEvent.ProjectSelected(after.sequence, selectedProject));
                var completion = next.Guidance.LastCompletion;
                if (completion != null && completion.Sequence == after.sequence &&
                    (old.Guidance.LastCompletion == null || old.Guidance.LastCompletion.Sequence != completion.Sequence))
                    AnalyticsBoundary.TryRecord(localDiagnostics, LocalEvent.ProjectCompleted(after.sequence, completion.ProjectId));
                if (ChapterEconomy.Project(before).Progress.EraId == EraIds.Junkyard && ChapterEconomy.Project(after).Progress.EraId == EraIds.WorkshopYard)
                    AnalyticsBoundary.TryRecord(localDiagnostics, LocalEvent.EraEntered(after.sequence, EraIds.WorkshopYard));
                var claim = next.Orders.LatestClaim;
                if (claim != null && claim.ClaimSequence == after.sequence &&
                    (old.Orders.LatestClaim == null || old.Orders.LatestClaim.ClaimSequence != claim.ClaimSequence))
                    AnalyticsBoundary.TryRecord(localDiagnostics, LocalEvent.OrderClaimed(claim.ClaimSequence, claim.OrderId, claim.InstanceSequence));
                if (old.Guidance.TutorialBits != LocalEvent.TutorialBitsMask && next.Guidance.TutorialBits == LocalEvent.TutorialBitsMask)
                    AnalyticsBoundary.TryRecord(localDiagnostics, LocalEvent.TutorialCompleted(after.sequence, old.Guidance.TutorialBits, next.Guidance.TutorialBits));
            }
            catch (Exception) { /* Factory validation and sink failures cannot change the committed result. */ }
        }
    }
}
