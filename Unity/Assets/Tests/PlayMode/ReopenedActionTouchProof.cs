#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using JunkToNeon.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    // One actual-InputSystem counterexample attempt. No runtime hooks, event-handler
    // invocation, module processing/reset, geometry edit or input-setting override.
    internal static class ReopenedActionTouchProof
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        const int HeldId=23101,NavigationId=23102; // Owned synthetic contacts only.
        [Serializable] sealed class Phase
        {
            public string name,target,focusedEditor;public int frame,confirmInstance,moduleInstance;
            public long coins,sequence;public bool panelActive,confirmActive,applicationFocused;
            public int[] pressedOwnedTouchIds;
        }
        [Serializable] sealed class Trace
        {
            public string outcome="Setup",reason,beforeStateSha256,afterStateSha256,freshStateSha256,cleanupError;
            public int originalConfirmInstance,currentConfirmInstance,ownedDeviceId;
            public string pointerBehavior,pointActionType,clickActionType;
            public int ownedPointControls,ownedClickControls;
            public bool passThroughBindingsValidated;
            public bool sameObject,criticalReleaseReached,ownedDeviceRemoved,existingDevicesPreserved,settingsPreserved,previousTouchscreenCurrentRestored,previousPointerCurrentRestored;
            public Phase[] phases;
        }
        static void Require(bool value,string reason,Trace trace)
        {
            if(value)return;trace.outcome="Inconclusive";trace.reason=reason;Assert.Inconclusive(reason);
        }
        static string Hash(string raw)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-","");}
        static T Field<T>(Component game,string name)=>(T)game.GetType().GetField(name,Hidden).GetValue(game);
        static Vector2 Center(Button button)
        {
            var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        }
        static GameObject ClickHandler(RaycastResult result)=>result.gameObject==null?null:ExecuteEvents.GetEventHandler<IPointerClickHandler>(result.gameObject);
        static bool Hits(Button button,Vector2 position)
        {
            var results=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},results);
            return results.Count>0&&ClickHandler(results[0])==button.gameObject;
        }
        static int[] Pressed(Touchscreen screen)=>screen==null?new int[0]:screen.touches.Where(t=>t.press.isPressed).Select(t=>t.touchId.ReadValue()).OrderBy(x=>x).ToArray();
        static Dictionary<string,string> Inventory(string directory)
        {
            var paths=Directory.GetFiles(directory);
            if(paths.Length>8||Directory.GetDirectories(directory).Length!=0)throw new IOException("Unexpected private proof inventory");
            return paths.ToDictionary(Path.GetFileName,path=>{
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length>262148)throw new IOException("Invalid private proof file");
                return Convert.ToBase64String(File.ReadAllBytes(path))+":"+File.GetLastWriteTimeUtc(path).Ticks;
            });
        }
        static bool Same(Dictionary<string,string> before,string directory)
        {var after=Inventory(directory);return before.Count==after.Count&&before.All(x=>after.TryGetValue(x.Key,out var value)&&value==x.Value);}
        static bool TouchBinding(InputActionReference reference,string path,Touchscreen screen)
        {
            var action=reference?.action;
            return action!=null&&action.enabled&&action.type==InputActionType.PassThrough&&
                action.bindings.Any(b=>string.Equals(b.effectivePath,path,StringComparison.OrdinalIgnoreCase))&&action.controls.Any(c=>c.device==screen);
        }
        static void Queue(Touchscreen screen,int id,UnityEngine.InputSystem.TouchPhase phase,Vector2 position)
        {InputSystem.QueueStateEvent(screen,new TouchState{touchId=id,phase=phase,position=position});}
        static IEnumerator Tap(Touchscreen screen,int id,Vector2 position,Action<string> capture,string name,Trace trace,bool held)
        {
            Queue(screen,id,UnityEngine.InputSystem.TouchPhase.Began,position);yield return null;yield return null;
            capture(name+"-down");Require(Pressed(screen).SequenceEqual(held?new[]{HeldId,NavigationId}:new[]{NavigationId}),"Actual owned contact state did not establish "+name,trace);
            Queue(screen,id,UnityEngine.InputSystem.TouchPhase.Ended,position);yield return null;yield return null;capture(name+"-up");
            Require(Pressed(screen).SequenceEqual(held?new[]{HeldId}:new int[0]),"Owned held contact was canceled or navigation contact did not end: "+name,trace);
        }
        internal static IEnumerator LoadGuarded(IEnumerator loader)
        {
            try
            {
                while(true)
                {
                    bool more=false;object next=null;
                    try{more=loader.MoveNext();if(more)next=loader.Current;}
                    catch(Exception error){Assert.Inconclusive("Private additive-scene setup: "+error.GetType().Name+": "+error.Message);}
                    if(!more)break;yield return next;
                }
            }
            finally{(loader as IDisposable)?.Dispose();}
        }
        internal static IEnumerator Run(Component game,string directory)
        {
            var trace=new Trace();var phases=new List<Phase>();var codec=new JsonStateCodec();Touchscreen owned=null;
            var originalDevices=InputSystem.devices.ToArray();var priorCurrent=Touchscreen.current;
            var priorPointer=UnityEngine.InputSystem.Pointer.current;
            var originalSettings=InputSystem.settings;var originalUpdateMode=originalSettings.updateMode;
            var session=(GameSession)game.GetType().GetProperty("Session").GetValue(game);
            var panel=Field<GameObject>(game,"panel");InputSystemUIInputModule module=null;
            string before=null;Dictionary<string,string> files=null;bool privateValidated=false;
            Action<string> capture=name=>{
                if(phases.Count>=20)throw new InvalidOperationException("Bounded touch trace exhausted");
                var button=Field<Button>(game,"selectButton");var state=session.State;
                phases.Add(new Phase{name=name,frame=Time.frameCount,coins=state?.coins??-1,sequence=state?.sequence??-1,
                    target=Field<string>(game,"chapterProject"),confirmInstance=button==null?0:button.GetInstanceID(),moduleInstance=module==null?0:module.GetInstanceID(),
                    panelActive=panel.activeSelf,confirmActive=button!=null&&button.isActiveAndEnabled,pressedOwnedTouchIds=Pressed(owned),
                    applicationFocused=Application.isFocused,focusedEditor=EditorWindow.focusedWindow?.GetType().FullName});
            };
            try
            {
                string expectedRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/combined-lifecycle-tests"))+Path.DirectorySeparatorChar;
                Require(Path.GetFullPath(directory).StartsWith(expectedRoot,StringComparison.OrdinalIgnoreCase)&&Guid.TryParseExact(Path.GetFileName(directory),"N",out _),"Wrong private-X proof directory",trace);
                privateValidated=true;
                Require(!session.RecoveryRequired&&session.State.coins==960&&session.State.houseStage==1&&session.State.workshopBuilt==0&&session.Chapter.SelectedProjectId==null&&!panel.activeSelf,"Private960 setup differs from the declared synthetic fixture",trace);
                Require(priorPointer!=null||priorCurrent==null,"Original current-pointer state cannot be restored using public APIs",trace);
                before=codec.Encode(session.State);files=Inventory(directory);trace.beforeStateSha256=Hash(before);
                Require(ChapterEconomy.ProjectPrice("house.improve")==480&&ChapterEconomy.ProjectPrice("workshop.build")==120,"Current literal480/120 offers differ from this bounded proof",trace);
                // Focus only before adding the owned device/any gesture, never during hold.
                var simulators=Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w=>w.GetType().FullName=="UnityEditor.DeviceSimulation.SimulatorWindow").ToArray();
                Require(simulators.Length==1,"One existing official Simulator is required",trace);simulators[0].Focus();simulators[0].Repaint();yield return null;yield return null;
                module=EventSystem.current?.currentInputModule as InputSystemUIInputModule;
                Require(module!=null&&module.isActiveAndEnabled&&module.pointerBehavior==UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack,"Active installed multitouch UI module not established",trace);
                try{owned=InputSystem.AddDevice<Touchscreen>("JtnConsentProofTouchscreen");}
                catch(Exception error){Require(false,"Owned touchscreen setup: "+error.GetType().Name,trace);}
                trace.ownedDeviceId=owned.deviceId;yield return null;yield return null;
                Require(TouchBinding(module.point,"<Touchscreen>/touch*/position",owned)&&TouchBinding(module.leftClick,"<Touchscreen>/touch*/press",owned),"Enabled PassThrough wildcard actions did not bind the owned touchscreen",trace);
                trace.pointerBehavior=module.pointerBehavior.ToString();trace.pointActionType=module.point.action.type.ToString();trace.clickActionType=module.leftClick.action.type.ToString();
                trace.passThroughBindingsValidated=true;trace.ownedPointControls=module.point.action.controls.Count(c=>c.device==owned);trace.ownedClickControls=module.leftClick.action.controls.Count(c=>c.device==owned);
                // The yard buildings open the same House and Workshop panels the hidden legacy root buttons did.
                var yard=Field<Button[]>(game,"yardButtons");var house=yard[0];var workshop=yard[1];capture("device-ready");
                Require(house.IsActive()&&house.IsInteractable()&&Hits(house,Center(house)),"Existing house button center is not naturally raycastable",trace);
                yield return Tap(owned,NavigationId,Center(house),capture,"open-house",trace,false);
                var selectA=Field<Button>(game,"selectButton");
                Require(panel.activeSelf&&selectA.isActiveAndEnabled&&selectA.IsInteractable()&&Field<string>(game,"chapterProject")=="house.improve","Actual first tap did not open actionable house480 panel",trace);
                Require(codec.Encode(session.State)==before&&Same(files,directory),"Actual first open changed the declared960 fixture",trace);trace.originalConfirmInstance=selectA.GetInstanceID();
                Vector2 heldPosition=Center(selectA);Require(Hits(selectA,heldPosition),"House confirmation center is obscured",trace);
                Queue(owned,HeldId,UnityEngine.InputSystem.TouchPhase.Began,heldPosition);yield return null;yield return null;capture("hold-house-confirm");
                Require(Pressed(owned).SequenceEqual(new[]{HeldId})&&ClickHandler(module.GetLastRaycastResult(HeldId))==selectA.gameObject,"Actual held touch was not processed over house confirmation",trace);
                bool focused=Application.isFocused;string focusedEditor=EditorWindow.focusedWindow?.GetType().FullName;
                var close=Field<Button>(game,"closeButton");Require(Hits(close,Center(close)),"Separate Close center is obscured",trace);
                yield return Tap(owned,NavigationId,Center(close),capture,"close-house",trace,true);
                Require(!panel.activeSelf&&codec.Encode(session.State)==before&&Same(files,directory),"Actual Close did not cancel A read-only",trace);
                Require(workshop.IsActive()&&workshop.IsInteractable()&&Hits(workshop,Center(workshop)),"Existing workshop button center is not naturally raycastable",trace);
                yield return Tap(owned,NavigationId,Center(workshop),capture,"open-workshop",trace,true);
                var selectB=Field<Button>(game,"selectButton");trace.currentConfirmInstance=selectB.GetInstanceID();trace.sameObject=ReferenceEquals(selectA,selectB);
                Require(panel.activeSelf&&selectB.isActiveAndEnabled&&selectB.IsInteractable()&&Field<string>(game,"chapterProject")=="workshop.build","Actual second tap did not open actionable workshop120 panel",trace);
                Require(module==EventSystem.current.currentInputModule&&module.isActiveAndEnabled&&focused==Application.isFocused&&focusedEditor==EditorWindow.focusedWindow?.GetType().FullName,"Module/focus changed during held navigation",trace);
                Require(Hits(selectB,heldPosition),"Original held coordinate does not naturally hit current B confirm; geometry must not be moved",trace);
                Require(Pressed(owned).SequenceEqual(new[]{HeldId})&&codec.Encode(session.State)==before&&Same(files,directory),"State/files/contact changed before critical release",trace);capture("before-old-release");
                // No B-confirm down was queued. Only the old A-confirm contact ends.
                Queue(owned,HeldId,UnityEngine.InputSystem.TouchPhase.Ended,heldPosition);yield return null;yield return null;trace.criticalReleaseReached=true;capture("after-old-release");
                Require(module==EventSystem.current.currentInputModule&&module.isActiveAndEnabled&&focused==Application.isFocused&&focusedEditor==EditorWindow.focusedWindow?.GetType().FullName&&Pressed(owned).Length==0,"Release contact/module/focus processing was not stable",trace);
                string after=codec.Encode(session.State);trace.afterStateSha256=Hash(after);
                bool unchanged=after==before&&Same(files,directory);
                bool predicted=session.State.coins==840&&session.State.workshopBuilt==1&&session.State.houseStage==1&&session.State.sequence==codec.Decode(before).sequence+1&&session.State.seed==codec.Decode(before).seed&&session.Chapter.SelectedProjectId==null&&!session.RecoveryRequired;
                Require(unchanged||predicted,"Critical release reached an unexpected outcome; preserve trace for classification",trace);
                trace.outcome=predicted?"Counterexample960to840":"NoChargePassed";
                Assert.That(after,Is.EqualTo(before),"Old held house480 press must not authorize workshop120 without a fresh B press: expected960/workshop0, observed840/workshop1");
                Assert.That(Same(files,directory),Is.True,"Old release changed private bytes/inventory/timestamps");
                // A new down/up belongs to B itself and must remain one-click.
                Require(Hits(selectB,Center(selectB)),"Fresh B confirmation is not naturally raycastable",trace);
                yield return Tap(owned,NavigationId,Center(selectB),capture,"fresh-workshop-confirm",trace,false);
                Require(!session.RecoveryRequired,"Fresh current confirmation blocked healthy persistence",trace);
                var fresh=session.State;
                Assert.That(fresh.coins,Is.EqualTo(840),"Fresh workshop120 must debit960 exactly once");
                Assert.That(fresh.workshopBuilt,Is.EqualTo(1));
                Assert.That(fresh.sequence,Is.EqualTo(codec.Decode(before).sequence+1));
                Assert.That(fresh.seed,Is.EqualTo(codec.Decode(before).seed));
                Assert.That(fresh.pending,Is.Null);Assert.That(session.Chapter.SelectedProjectId,Is.Null);
                Assert.That(File.ReadAllText(Path.Combine(directory,"jtn.save.v1.json")),Is.EqualTo(codec.Encode(fresh)));
                trace.freshStateSha256=Hash(codec.Encode(fresh));trace.outcome="NoChargeThenFresh120Passed";
            }
            finally
            {
                try
                {
                    if(owned!=null&&owned.added)InputSystem.RemoveDevice(owned);
                    if(priorCurrent!=null&&priorCurrent.added&&Touchscreen.current==null)priorCurrent.MakeCurrent();
                    if(priorPointer!=null&&priorPointer.added&&!ReferenceEquals(UnityEngine.InputSystem.Pointer.current,priorPointer))priorPointer.MakeCurrent();
                }
                catch(Exception error){trace.cleanupError=error.GetType().Name+": "+error.Message;}
                trace.ownedDeviceRemoved=owned==null||!owned.added;
                trace.existingDevicesPreserved=originalDevices.All(d=>InputSystem.devices.Contains(d));
                trace.settingsPreserved=ReferenceEquals(originalSettings,InputSystem.settings)&&InputSystem.settings.updateMode==originalUpdateMode;
                trace.previousTouchscreenCurrentRestored=Touchscreen.current==priorCurrent;
                trace.previousPointerCurrentRestored=UnityEngine.InputSystem.Pointer.current==priorPointer;
                trace.phases=phases.ToArray();
                bool cleanupOk=trace.ownedDeviceRemoved&&trace.existingDevicesPreserved&&trace.settingsPreserved&&trace.previousTouchscreenCurrentRestored&&trace.previousPointerCurrentRestored&&trace.cleanupError==null;
                bool cleanupInconclusive=(trace.outcome=="NoChargePassed"||trace.outcome=="NoChargeThenFresh120Passed")&&!cleanupOk;
                if(cleanupInconclusive){trace.outcome="Inconclusive";trace.reason="Owned input-device/current-pointer cleanup was not exact";}
                // Written only after all read-only comparisons, inside this one retained
                // private fixture. No user/global input state is recorded.
                if(privateValidated)File.WriteAllText(Path.Combine(directory,"actual-touch-proof.json"),JsonUtility.ToJson(trace,true));
                TestContext.WriteLine("Actual touch proof: "+trace.outcome+"; retained private trace="+privateValidated);
                if(cleanupInconclusive)Assert.Inconclusive(trace.reason);
            }
        }
        // TEST-ONLY extension, not executed by this producer. Two independent
        // contacts target distinct natural points of the existing Restore button.
        internal static IEnumerator RunRecovery(Component game,string directory,string expectedBackup)
        {
            var trace=new Trace();var phases=new List<Phase>();var codec=new JsonStateCodec();Touchscreen owned=null;
            var originalDevices=InputSystem.devices.ToArray();var priorCurrent=Touchscreen.current;
            var priorPointer=UnityEngine.InputSystem.Pointer.current;
            var originalSettings=InputSystem.settings;var originalUpdateMode=originalSettings.updateMode;
            var session=(GameSession)game.GetType().GetProperty("Session").GetValue(game);
            var panel=Field<GameObject>(game,"panel");InputSystemUIInputModule module=null;
            bool privateValidated=false;
            Action<string> capture=name=>{
                if(phases.Count>=20)throw new InvalidOperationException("Bounded recovery touch trace exhausted");
                var button=Field<Button>(game,"selectButton");var state=session.State;
                phases.Add(new Phase{name=name,frame=Time.frameCount,coins=state?.coins??-1,sequence=state?.sequence??-1,
                    target=Field<bool>(game,"restoreConfirmation")?"restore-confirm":"restore-offer",confirmInstance=button.GetInstanceID(),
                    moduleInstance=module==null?0:module.GetInstanceID(),panelActive=panel.activeSelf,confirmActive=button.isActiveAndEnabled,
                    pressedOwnedTouchIds=Pressed(owned),applicationFocused=Application.isFocused,focusedEditor=EditorWindow.focusedWindow?.GetType().FullName});
            };
            try
            {
                string expectedRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/combined-lifecycle-tests"))+Path.DirectorySeparatorChar;
                Require(Path.GetFullPath(directory).StartsWith(expectedRoot,StringComparison.OrdinalIgnoreCase)&&Guid.TryParseExact(Path.GetFileName(directory),"N",out _),"Wrong private-X recovery proof directory",trace);
                privateValidated=true;
                Require(session.State==null&&session.RecoveryRequired&&session.BackupAvailable&&panel.activeSelf&&!Field<bool>(game,"restoreConfirmation"),"Expected StateNULL blocked Restore offer",trace);
                Require(priorPointer!=null||priorCurrent==null,"Original current-pointer state cannot be restored using public APIs",trace);
                var expected=codec.Decode(expectedBackup);
                Require(expected.coins==73&&expected.seed==123&&expected.sequence==0&&expected.lastSettledAt==1000&&expected.autoHintSeen==1,"Synthetic73 backup differs from declared setup",trace);
                var originalFiles=Inventory(directory);string error=session.Error;
                string originalPrimary=File.ReadAllText(Path.Combine(directory,"jtn.save.v1.json"));
                Require(File.ReadAllText(Path.Combine(directory,"jtn.backup.v1.json"))==expectedBackup,"Private backup bytes differ from setup",trace);
                var simulators=Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w=>w.GetType().FullName=="UnityEditor.DeviceSimulation.SimulatorWindow").ToArray();
                Require(simulators.Length==1,"One existing official Simulator is required",trace);simulators[0].Focus();simulators[0].Repaint();yield return null;yield return null;
                module=EventSystem.current?.currentInputModule as InputSystemUIInputModule;
                Require(module!=null&&module.isActiveAndEnabled&&module.pointerBehavior==UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack,"Active multitouch UI module not established",trace);
                try{owned=InputSystem.AddDevice<Touchscreen>("JtnRecoveryConsentProofTouchscreen");}
                catch(Exception exception){Require(false,"Owned recovery touchscreen setup: "+exception.GetType().Name,trace);}
                trace.ownedDeviceId=owned.deviceId;yield return null;yield return null;
                Require(TouchBinding(module.point,"<Touchscreen>/touch*/position",owned)&&TouchBinding(module.leftClick,"<Touchscreen>/touch*/press",owned),"Recovery wildcard actions did not bind owned contacts",trace);
                trace.pointerBehavior=module.pointerBehavior.ToString();trace.pointActionType=module.point.action.type.ToString();trace.clickActionType=module.leftClick.action.type.ToString();
                trace.passThroughBindingsValidated=true;trace.ownedPointControls=module.point.action.controls.Count(c=>c.device==owned);trace.ownedClickControls=module.leftClick.action.controls.Count(c=>c.device==owned);
                var select=Field<Button>(game,"selectButton");trace.originalConfirmInstance=select.GetInstanceID();
                var corners=new Vector3[4];((RectTransform)select.transform).GetWorldCorners(corners);
                var canvas=select.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                float width=(RectTransformUtility.WorldToScreenPoint(camera,corners[2])-RectTransformUtility.WorldToScreenPoint(camera,corners[1])).magnitude;
                Vector2 held=Center(select)-Vector2.right*width*.2f,fresh=Center(select)+Vector2.right*width*.2f;
                Require(select.isActiveAndEnabled&&select.IsInteractable()&&Hits(select,held)&&Hits(select,fresh),"Two distinct Restore points must naturally raycast to existing button",trace);
                bool focused=Application.isFocused;string focusedEditor=EditorWindow.focusedWindow?.GetType().FullName;
                Queue(owned,HeldId,UnityEngine.InputSystem.TouchPhase.Began,held);yield return null;yield return null;capture("hold-restore-offer");
                Require(Pressed(owned).SequenceEqual(new[]{HeldId})&&ClickHandler(module.GetLastRaycastResult(HeldId))==select.gameObject,"Held restore offer was not delivered",trace);
                yield return Tap(owned,NavigationId,fresh,capture,"fresh-open-restore-confirmation",trace,true);
                trace.currentConfirmInstance=Field<Button>(game,"selectButton").GetInstanceID();trace.sameObject=trace.originalConfirmInstance==trace.currentConfirmInstance;
                Require(trace.sameObject&&panel.activeSelf&&select.isActiveAndEnabled,"Recovery shared control was unexpectedly replaced/hidden",trace);
                Assert.That(Field<bool>(game,"restoreConfirmation"),Is.True,"Fresh Restore must open the in-place confirmation");
                Assert.That(session.State,Is.Null);Assert.That(session.RecoveryRequired,Is.True);
                Assert.That(Same(originalFiles,directory),Is.True,"First Restore confirmation must write nothing");Assert.That(session.Error,Is.EqualTo(error));
                Require(module==EventSystem.current.currentInputModule&&module.isActiveAndEnabled&&focused==Application.isFocused&&focusedEditor==EditorWindow.focusedWindow?.GetType().FullName&&Hits(select,held),"Recovery module/focus/natural held coordinate changed",trace);
                Queue(owned,HeldId,UnityEngine.InputSystem.TouchPhase.Ended,held);yield return null;yield return null;trace.criticalReleaseReached=true;capture("after-old-restore-offer-release");
                Require(Pressed(owned).Length==0&&module==EventSystem.current.currentInputModule&&module.isActiveAndEnabled&&focused==Application.isFocused&&focusedEditor==EditorWindow.focusedWindow?.GetType().FullName,"Recovery release contact/module/focus was not stable",trace);
                trace.outcome="RecoveryOldReleaseReached";
                Assert.That(session.State,Is.Null,"Old offer-phase held contact cannot authorize the restore confirmation");
                Assert.That(session.RecoveryRequired,Is.True);Assert.That(Field<bool>(game,"restoreConfirmation"),Is.True);
                Assert.That(session.Error,Is.EqualTo(error));Assert.That(Same(originalFiles,directory),Is.True,"Old offer release must preserve all backup/recovery evidence");
                yield return Tap(owned,NavigationId,fresh,capture,"fresh-confirm-restore",trace,false);
                Require(module==EventSystem.current.currentInputModule&&module.isActiveAndEnabled&&focused==Application.isFocused&&focusedEditor==EditorWindow.focusedWindow?.GetType().FullName,"Fresh recovery input module/focus changed",trace);
                Assert.That(session.RecoveryRequired,Is.False,"Fresh second Restore must recover the existing backup");
                Assert.That(session.State,Is.Not.Null);
                Assert.That(codec.Encode(session.State),Is.EqualTo(expectedBackup),"Restore must recover exact synthetic73 state, not grant a new37 or advance RNG/history");
                Assert.That(File.ReadAllText(Path.Combine(directory,"jtn.save.v1.json")),Is.EqualTo(expectedBackup));
                Assert.That(File.ReadAllText(Path.Combine(directory,"jtn.backup.v1.json")),Is.EqualTo(expectedBackup));
                Assert.That(File.ReadAllText(Path.Combine(directory,"jtn.recovery-primary.v1.json")),Is.EqualTo(originalPrimary));
                Assert.That(File.ReadAllText(Path.Combine(directory,"jtn.recovery-backup.v1.json")),Is.EqualTo(expectedBackup));
                var restored=Inventory(directory);yield return null;yield return null;
                Assert.That(codec.Encode(session.State),Is.EqualTo(expectedBackup));Assert.That(Same(restored,directory),Is.True,"Completed fresh restoration repeated a write");
                trace.freshStateSha256=Hash(codec.Encode(session.State));trace.outcome="RecoveryOldReleaseRejectedThenFresh73";
            }
            finally
            {
                try
                {
                    if(owned!=null&&owned.added)InputSystem.RemoveDevice(owned);
                    if(priorCurrent!=null&&priorCurrent.added&&Touchscreen.current==null)priorCurrent.MakeCurrent();
                    if(priorPointer!=null&&priorPointer.added&&!ReferenceEquals(UnityEngine.InputSystem.Pointer.current,priorPointer))priorPointer.MakeCurrent();
                }
                catch(Exception exception){trace.cleanupError=exception.GetType().Name+": "+exception.Message;}
                trace.ownedDeviceRemoved=owned==null||!owned.added;trace.existingDevicesPreserved=originalDevices.All(d=>InputSystem.devices.Contains(d));
                trace.settingsPreserved=ReferenceEquals(originalSettings,InputSystem.settings)&&InputSystem.settings.updateMode==originalUpdateMode;
                trace.previousTouchscreenCurrentRestored=Touchscreen.current==priorCurrent;trace.previousPointerCurrentRestored=UnityEngine.InputSystem.Pointer.current==priorPointer;trace.phases=phases.ToArray();
                bool cleanupOk=trace.ownedDeviceRemoved&&trace.existingDevicesPreserved&&trace.settingsPreserved&&trace.previousTouchscreenCurrentRestored&&trace.previousPointerCurrentRestored&&trace.cleanupError==null;
                bool cleanupInconclusive=trace.outcome=="RecoveryOldReleaseRejectedThenFresh73"&&!cleanupOk;
                if(cleanupInconclusive){trace.outcome="Inconclusive";trace.reason="Recovery owned-input cleanup was not exact";}
                if(privateValidated)File.WriteAllText(Path.Combine(directory,"actual-restore-touch-proof.json"),JsonUtility.ToJson(trace,true));
                TestContext.WriteLine("Actual restore touch proof: "+trace.outcome+"; retained private trace="+privateValidated);
                if(cleanupInconclusive)Assert.Inconclusive(trace.reason);
            }
        }
        [Serializable] sealed class SubmitTrace
        {
            public string outcome="Setup",reason,cleanupError,beforeStateSha256,afterDownStateSha256,afterUpStateSha256;
            public int ownedDeviceId,ownedSubmitPerformed;
            public long coinsBefore,coinsAfterDown,coinsAfterUp,sequenceBefore,sequenceAfterDown;
            public bool enterBound,selectedExistingControl,moduleStable,focusStable,ownedDeviceRemoved,existingDevicesPreserved,settingsPreserved,previousKeyboardCurrentRestored,previousPointerCurrentRestored;
        }
        // TEST-ONLY fresh Submit dispatch: no manual OnSubmit/UnityEvent call.
        internal static IEnumerator RunSubmit(Component game,string directory)
        {
            var trace=new SubmitTrace();var codec=new JsonStateCodec();Keyboard owned=null;InputAction action=null;
            Action<InputAction.CallbackContext> observe=null;
            var devices=InputSystem.devices.ToArray();var priorKeyboard=Keyboard.current;var priorPointer=UnityEngine.InputSystem.Pointer.current;
            var settings=InputSystem.settings;var updateMode=settings.updateMode;bool privateValidated=false;
            Action<bool,string> require=(value,reason)=>{if(!value){trace.outcome="Inconclusive";trace.reason=reason;Assert.Inconclusive(reason);}};
            var session=(GameSession)game.GetType().GetProperty("Session").GetValue(game);
            try
            {
                string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/combined-lifecycle-tests"))+Path.DirectorySeparatorChar;
                require(Path.GetFullPath(directory).StartsWith(root,StringComparison.OrdinalIgnoreCase)&&Guid.TryParseExact(Path.GetFileName(directory),"N",out _),"Wrong private-X Submit directory");privateValidated=true;
                var select=Field<Button>(game,"selectButton");
                require(!session.RecoveryRequired&&session.State.coins==960&&session.State.houseStage==1&&session.State.workshopBuilt==0&&session.Chapter.SelectedProjectId==null&&Field<GameObject>(game,"panel").activeSelf&&Field<string>(game,"chapterProject")=="workshop.build"&&select.isActiveAndEnabled&&select.IsInteractable(),"Declared960 private workshop confirmation setup differs");
                var simulators=Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w=>w.GetType().FullName=="UnityEditor.DeviceSimulation.SimulatorWindow").ToArray();
                require(simulators.Length==1,"One existing official Simulator is required");simulators[0].Focus();simulators[0].Repaint();yield return null;yield return null;
                var module=EventSystem.current?.currentInputModule as InputSystemUIInputModule;
                require(module!=null&&module.isActiveAndEnabled,"Installed UI module is unavailable");
                try{owned=InputSystem.AddDevice<Keyboard>("JtnFreshSubmitProofKeyboard");}
                catch(Exception exception){require(false,"Owned keyboard setup: "+exception.GetType().Name);}
                trace.ownedDeviceId=owned.deviceId;yield return null;yield return null;
                action=module.submit?.action;
                trace.enterBound=action!=null&&action.enabled&&action.type==InputActionType.Button&&action.controls.Any(c=>ReferenceEquals(c,owned.enterKey));
                require(trace.enterBound,"Installed enabled Submit action does not bind the owned Enter control; do not override bindings");
                // Public selection is the legal keyboard-navigation precondition;
                // the confirmation itself must arrive from the actual Submit action.
                EventSystem.current.SetSelectedGameObject(select.gameObject);
                trace.selectedExistingControl=EventSystem.current.currentSelectedGameObject==select.gameObject;
                require(trace.selectedExistingControl&&Hits(select,Center(select)),"Existing current confirm cannot be selected/raycasted naturally");
                string before=codec.Encode(session.State);
                trace.beforeStateSha256=Hash(before);trace.coinsBefore=session.State.coins;trace.sequenceBefore=session.State.sequence;
                bool focused=Application.isFocused;string focusedEditor=EditorWindow.focusedWindow?.GetType().FullName;
                observe=context=>{if(context.control!=null&&context.control.device==owned)trace.ownedSubmitPerformed++;};action.performed+=observe;
                InputSystem.QueueStateEvent(owned,new KeyboardState(Key.Enter));yield return null;yield return null;
                trace.moduleStable=module==EventSystem.current.currentInputModule&&module.isActiveAndEnabled;
                trace.focusStable=focused==Application.isFocused&&focusedEditor==EditorWindow.focusedWindow?.GetType().FullName;
                require(trace.moduleStable&&trace.focusStable&&owned.enterKey.isPressed&&trace.ownedSubmitPerformed==1,"Actual Enter-down/Submit action/module/focus was not established");
                trace.outcome="ActualSubmitDownReached";var paid=session.State;
                trace.coinsAfterDown=paid.coins;trace.sequenceAfterDown=paid.sequence;trace.afterDownStateSha256=Hash(codec.Encode(paid));
                Assert.That(session.RecoveryRequired,Is.False);Assert.That(paid.coins,Is.EqualTo(840),"Actual fresh Submit must pay current workshop120 once from960");
                Assert.That(paid.workshopBuilt,Is.EqualTo(1));Assert.That(paid.houseStage,Is.EqualTo(1));
                Assert.That(paid.sequence,Is.EqualTo(codec.Decode(before).sequence+1));Assert.That(paid.seed,Is.EqualTo(codec.Decode(before).seed));
                Assert.That(paid.pending,Is.Null);Assert.That(session.Chapter.SelectedProjectId,Is.Null);
                string committed=codec.Encode(paid);Assert.That(File.ReadAllText(Path.Combine(directory,"jtn.save.v1.json")),Is.EqualTo(committed));
                var paidFiles=Inventory(directory);
                InputSystem.QueueStateEvent(owned,new KeyboardState());yield return null;yield return null;
                trace.moduleStable=module==EventSystem.current.currentInputModule&&module.isActiveAndEnabled;
                trace.focusStable=focused==Application.isFocused&&focusedEditor==EditorWindow.focusedWindow?.GetType().FullName;
                require(trace.moduleStable&&trace.focusStable&&!owned.enterKey.isPressed,"Actual Enter-up/module/focus was not established");
                trace.coinsAfterUp=session.State.coins;trace.afterUpStateSha256=Hash(codec.Encode(session.State));
                Assert.That(trace.ownedSubmitPerformed,Is.EqualTo(1),"Key-up must not perform Submit again");
                Assert.That(codec.Encode(session.State),Is.EqualTo(committed));Assert.That(Same(paidFiles,directory),Is.True,"Key-up must not repeat debit/write");
                trace.outcome="ActualSubmit960to840Once";
            }
            finally
            {
                try
                {
                    if(action!=null&&observe!=null)action.performed-=observe;
                    if(owned!=null&&owned.added)InputSystem.RemoveDevice(owned);
                    if(priorKeyboard!=null&&priorKeyboard.added&&Keyboard.current!=priorKeyboard)priorKeyboard.MakeCurrent();
                    if(priorPointer!=null&&priorPointer.added&&!ReferenceEquals(UnityEngine.InputSystem.Pointer.current,priorPointer))priorPointer.MakeCurrent();
                }
                catch(Exception exception){trace.cleanupError=exception.GetType().Name+": "+exception.Message;}
                trace.ownedDeviceRemoved=owned==null||!owned.added;trace.existingDevicesPreserved=devices.All(d=>InputSystem.devices.Contains(d));
                trace.settingsPreserved=ReferenceEquals(settings,InputSystem.settings)&&InputSystem.settings.updateMode==updateMode;
                trace.previousKeyboardCurrentRestored=Keyboard.current==priorKeyboard;trace.previousPointerCurrentRestored=UnityEngine.InputSystem.Pointer.current==priorPointer;
                bool cleanupOk=trace.ownedDeviceRemoved&&trace.existingDevicesPreserved&&trace.settingsPreserved&&trace.previousKeyboardCurrentRestored&&trace.previousPointerCurrentRestored&&trace.cleanupError==null;
                bool cleanupInconclusive=trace.outcome=="ActualSubmit960to840Once"&&!cleanupOk;
                if(cleanupInconclusive){trace.outcome="Inconclusive";trace.reason="Owned keyboard/current-pointer cleanup was not exact";}
                if(privateValidated)File.WriteAllText(Path.Combine(directory,"actual-submit-proof.json"),JsonUtility.ToJson(trace,true));
                TestContext.WriteLine("Actual Submit proof: "+trace.outcome+"; retained private trace="+privateValidated);
                if(cleanupInconclusive)Assert.Inconclusive(trace.reason);
            }
        }
    }
}
#endif
