#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JunkToNeon.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    public sealed partial class FirstSessionPresentationUiTests
    {
        List<bool> ObserveSlotHaptics()
        {
            var requests = new List<bool>();
            Action<bool> observe = requests.Add;
            Game.GetType().GetEvent("SlotHapticRequested", Hidden).GetAddMethod(true).Invoke(Game, new object[] { observe });
            return requests;
        }

        RectTransform CoinWalletIcon()=>Field<TMP_Text>("productCoins").transform.parent
            .GetComponentsInChildren<MaskableGraphic>().Single(g=>g.GetType().Name=="JtnProductIcon").rectTransform;

        long VisibleCoins()=>long.Parse(Field<TMP_Text>("productCoins").text.Split('\n')[0]);

        IEnumerator PremiumSpin(bool shortPortrait)
        {
            HouseProfile(shortPortrait?3:2);yield return null;
            yield return Harness("Prepare",ModelState.Fresh(1));
            Assert.That(Screen.height,Is.EqualTo(shortPortrait?2280:2340));
            var haptics=ObserveSlotHaptics();
            var images=Field<Image[]>("reelImages");var rest=images.Select(i=>i.rectTransform.anchoredPosition).ToArray();
            int first=Time.frameCount;yield return Click("spinButton");
            var visibleCoins=new List<long>{VisibleCoins()};
            Assert.That(visibleCoins[0],Is.EqualTo(37),"The committed award is hidden while the reels move");
            Assert.That(Session.State.coins,Is.EqualTo(45));Assert.That(Session.State.lastSpin.symbols,Is.EqualTo(new[]{0,0,2}));
            string committed=State;var files=Files();var incoming=Field<Image[]>("reelIncoming");
            var pool=incoming.Select(i=>i.GetInstanceID()).ToArray();
            Field<Button>("spinButton").onClick.Invoke();Field<Button>("spinButton").onClick.Invoke();Unchanged(committed,files);
            bool moved=false,staggered=false,goldVisible=false,creditVisible=false,burstVisible=false,heardRoll=false,creditSound=false;
            int peakSparks=0;
            var icon=CoinWalletIcon();
            var iconRest=icon.localScale;
            float highestRise=0,closestToWallet=float.PositiveInfinity;
            bool beganAtReels=false,received=false,lateAccent=false;
            Vector2 previous=images[2].rectTransform.anchoredPosition;
            int samples=0;float[] captureTimes={.15f,.50f,1.15f,2.75f,3.67f,3.94f,4.24f};
            string capture=Environment.GetEnvironmentVariable("JTN_PREMIUM_CAPTURE_DIRECTORY");
            if(!string.IsNullOrEmpty(capture))capture=Path.Combine(capture,shortPortrait?"short":"regular");
            var clock=System.Diagnostics.Stopwatch.StartNew();
            while((bool)Game.GetType().GetProperty("Busy").GetValue(Game)||clock.Elapsed.TotalSeconds<4.65)
            {
                yield return null;
                if((bool)Game.GetType().GetProperty("Busy").GetValue(Game))
                {
                    heardRoll|=Field<AudioSource>("slotRollSource")?.isPlaying==true;
                    moved|=Vector2.Distance(previous,images[2].rectTransform.anchoredPosition)>.1f;
                    previous=images[2].rectTransform.anchoredPosition;
                    staggered|=!incoming[0].enabled&&incoming[2].enabled&&Vector2.Distance(rest[0],images[0].rectTransform.anchoredPosition)<.01f;
                    Assert.That((int)Game.GetType().GetProperty("RewardFeedbackVisibleCount").GetValue(Game),Is.Zero);
                }
                var particles=Field<Image[]>("rewardImpact");
                var activeEffects=particles.Where(i=>i!=null&&i.isActiveAndEnabled&&i.color.a>0);
                var lead=particles[0];
                if(lead!=null&&lead.isActiveAndEnabled&&lead.color.a>.25f)
                {
                    var layer=(RectTransform)lead.transform.parent;
                    Vector2 source=(Vector2)layer.InverseTransformPoint(images[1].transform.TransformPoint(images[1].rectTransform.rect.center))-layer.rect.center;
                    Vector2 end=(Vector2)layer.InverseTransformPoint(icon.TransformPoint(icon.rect.center))-layer.rect.center;
                    Vector2 position=lead.rectTransform.anchoredPosition;
                    beganAtReels|=Vector2.Distance(position,source)<20;
                    highestRise=Mathf.Max(highestRise,position.y-source.y);
                    closestToWallet=Mathf.Min(closestToWallet,Vector2.Distance(position,end));
                }
                received|=icon.localScale.x>iconRest.x*1.05f;
                goldVisible|=activeEffects.Any(i=>i.sprite!=null&&i.sprite.name=="coin");
                peakSparks=Math.Max(peakSparks,activeEffects.Count(i=>i.sprite!=null&&i.sprite.name=="gold-spark"));
                var burst=Field<Image>("rewardBurst");
                burstVisible|=burst!=null&&burst.isActiveAndEnabled&&burst.sprite?.name=="gold-burst"&&burst.color.a>0;
                if(burst!=null&&burst.isActiveAndEnabled&&Field<float>("rewardImpactAge")>=.10f)
                    lateAccent|=Field<AudioSource>("slotAccentSource").isPlaying;
                creditVisible|=Field<TMP_Text[]>("rewardFeedbackLabels").Any(i=>i.gameObject.activeInHierarchy&&i.text=="Coins +8"&&i.color.a>0);
                var shown=VisibleCoins();
                if(shown!=visibleCoins[visibleCoins.Count-1])
                {
                    visibleCoins.Add(shown);
                    creditSound|=Field<AudioSource>("slotCoinSource")?.isPlaying==true;
                }
                if(!string.IsNullOrEmpty(capture)&&samples<captureTimes.Length&&clock.Elapsed.TotalSeconds>=captureTimes[samples])
                {
                    string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/premium-slot-20261115/captures"));
                    Assert.That(Path.GetFullPath(capture).StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
                    Directory.CreateDirectory(capture);string file="frame-"+(samples++).ToString("D3")+".png";
                    ScreenCapture.CaptureScreenshot(Path.Combine(capture,file));
                    File.AppendAllText(Path.Combine(capture,"frames.tsv"),file+"\t"+clock.Elapsed.TotalSeconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+"\t"+Time.frameCount+"\n");
                }
                Unchanged(committed,files);
                Assert.That(clock.Elapsed.TotalSeconds,Is.LessThan(8));
            }
            Assert.That(Time.frameCount,Is.GreaterThan(first+10));Assert.That(moved&&staggered,Is.True,"Actual translating frames and left-to-right stop required");
            Assert.That(goldVisible&&creditVisible,Is.True,"Gold and the actual +8 receipt must be visible after reveal");
            Assert.That(beganAtReels&&highestRise>100&&closestToWallet<12&&received,Is.True,
                $"Coin must lift into the HUD icon and make it react. Rise {highestRise:F1}, closest {closestToWallet:F1}");
            Assert.That(icon.localScale,Is.EqualTo(iconRest));
            Assert.That(burstVisible,Is.True,"Generated gold burst must render on the committed spin");
            Assert.That(peakSparks,Is.EqualTo(4),"An actual 8-Coin spin uses four of the six pooled sparks");
            Assert.That(visibleCoins,Is.EqualTo(new long[]{37,39,41,43,45}));
            Assert.That(lateAccent,Is.False,"The ordinary burst has no large-win impact");
            Assert.That(heardRoll&&creditSound,Is.True,"The wallet steps with the quiet landing source");
            Assert.That(Field<int>("slotSettledReels"),Is.EqualTo(7));
            Assert.That(haptics,Is.EqualTo(new[]{false,false,false,true}),"Three light stops precede one Coin confirmation");
            Assert.That(Field<AudioSource>("slotRollSource").isPlaying,Is.False);
            for(int i=0;i<3;i++)
            {
                Assert.That(images[i].sprite,Is.EqualTo(Field<Sprite[]>("reelSymbols")[Session.State.lastSpin.symbols[i]]));
                Assert.That(images[i].rectTransform.anchoredPosition,Is.EqualTo(rest[i]));Assert.That(incoming[i].enabled,Is.False);
            }
            var impact=Field<Image[]>("rewardImpact");Assert.That(impact.Length,Is.EqualTo(18));Assert.That(impact.All(i=>i!=null&&!i.raycastTarget&&!i.gameObject.activeSelf),Is.True);
            Assert.That(Field<Image>("rewardBurst").raycastTarget||Field<Image>("rewardBurst").gameObject.activeInHierarchy,Is.False);
            var impactIds=impact.Select(i=>i.GetInstanceID()).ToArray();
            yield return Click("spinButton");yield return HouseFinish();
            Assert.That(Field<Image[]>("reelIncoming").Select(i=>i.GetInstanceID()),Is.EqualTo(pool));
            Assert.That(Field<Image[]>("rewardImpact").Select(i=>i.GetInstanceID()),Is.EqualTo(impactIds));
            Assert.That(Field<Image[]>("rewardImpact").Any(i=>i.gameObject.activeInHierarchy),Is.True);
            committed=State;files=Files();Call("ShowProject");yield return null;
            Assert.That(impact.All(i=>!i.gameObject.activeInHierarchy),Is.True);Unchanged(committed,files);
            Assert.That(Field<AudioSource>("slotAccentSource").isPlaying,Is.False);
            Assert.That(Field<AudioSource>("slotCoinSource").isPlaying,Is.False);
            Assert.That(VisibleCoins(),Is.EqualTo(Session.State.coins));
            Call("ClosePanel");Call("Reload");yield return null;
            Assert.That(impact.All(i=>!i.gameObject.activeInHierarchy),Is.True);Unchanged(committed,files);
            Assert.That(haptics,Is.EqualTo(new[]{false,false,false,true,false,false,false,true}),"Each new spin gets its own cues. Reload does not replay them.");
            TestContext.WriteLine("Premium spin frames="+first+"->"+Time.frameCount+"; actual scroll/stagger/final [0,0,2], 37->45 once; repeated contact, reuse, panel/Reload clear; capture frames="+samples);
        }

        [UnityTest] public IEnumerator PremiumReelsAndGoldRegularPortrait(){yield return PremiumSpin(false);}
        [UnityTest] public IEnumerator PremiumReelsAndGoldShortPortrait(){yield return PremiumSpin(true);}

        [UnityTest] public IEnumerator PremiumCoinStreamReachesWalletDuringNextAutoSpin()
        {
            var fixture=ModelState.Fresh(1);fixture.houseStage=1;fixture.autoHintSeen=1;
            yield return Harness("Prepare",fixture);
            Session.SelectExpansionSpeed("speed.fast",Session.State.sequence,1000);Call("Render",null,null);
            long sequence=Session.State.sequence;
            yield return Click("autoButton");
            Assert.That(Session.Arcade.LastResourceReceipt.CoinIncome,Is.EqualTo(8));
            var icon=CoinWalletIcon();
            var rest=icon.localScale;
            float deadline=Time.realtimeSinceStartup+5;
            bool inFlight=false,arrived=false;
            while(Time.realtimeSinceStartup<deadline&&!arrived)
            {
                yield return null;
                if(Session.State.sequence!=sequence+2||!(bool)Game.GetType().GetProperty("Busy").GetValue(Game))continue;
                var lead=Field<Image[]>("rewardImpact")[0];
                inFlight|=lead!=null&&lead.isActiveAndEnabled&&lead.color.a>.25f;
                arrived=icon.localScale.x>rest.x*1.05f;
            }
            Assert.That(inFlight&&arrived,Is.True,"First receipt must arrive while the next AUTO spin is moving");
            Assert.That(VisibleCoins(),Is.EqualTo(45));
            Assert.That(Session.State.coins,Is.EqualTo(53));
            Call("Render",null,null);
            Assert.That(VisibleCoins(),Is.EqualTo(45),"Redraw cannot reveal the next committed credit");
            NoFlyers();
            string committed=State;var files=Files();
            Call("Reload");yield return null;
            Unchanged(committed,files);
            Assert.That(icon.localScale,Is.EqualTo(rest));
            Assert.That(VisibleCoins(),Is.EqualTo(53));
            Assert.That(Field<Image[]>("rewardImpact").All(i=>!i.gameObject.activeInHierarchy),Is.True);
        }

        [UnityTest] public IEnumerator PremiumTripleUsesWiderBurstAndTwelveSparksWithExactCoinSteps()
        {
            yield return Harness("Prepare",ModelState.Fresh(2));
            Session.SelectExpansionSpeed("speed.fast",Session.State.sequence,1000);Call("Render",null,null);
            yield return Click("spinButton");
            Assert.That(Session.State.lastSpin.symbols,Is.EqualTo(new[]{0,0,0}));
            Assert.That(Session.Arcade.LastResourceReceipt.CoinIncome,Is.EqualTo(23));
            Assert.That(Session.State.coins,Is.EqualTo(60));
            var values=new List<long>{VisibleCoins()};
            var clock=System.Diagnostics.Stopwatch.StartNew();
            int peakSparks=0;
            float peakBurstScale=0;
            bool heardBurstAccent=false;
            while(clock.Elapsed.TotalSeconds<2.65)
            {
                yield return null;
                long shown=VisibleCoins();
                if(shown!=values[values.Count-1])values.Add(shown);
                var particles=Field<Image[]>("rewardImpact");
                peakSparks=Math.Max(peakSparks,particles.Count(i=>i!=null&&i.isActiveAndEnabled&&i.sprite?.name=="gold-spark"&&i.color.a>.05f));
                var burst=Field<Image>("rewardBurst");
                if(burst!=null&&burst.isActiveAndEnabled&&burst.color.a>.05f)
                {
                    peakBurstScale=Mathf.Max(peakBurstScale,burst.rectTransform.localScale.x);
                    if(Field<float>("rewardImpactAge")>=.10f)
                        heardBurstAccent|=Field<AudioSource>("slotAccentSource").isPlaying;
                }
            }
            Assert.That(values,Is.EqualTo(new long[]{37,41,45,49,53,57,60}));
            Assert.That(peakSparks,Is.EqualTo(12));
            Assert.That(peakBurstScale,Is.GreaterThan(1.6f));
            Assert.That(heardBurstAccent,Is.True,"The large burst has its own accent after the seat clicks");
            Assert.That(Field<Image[]>("rewardImpact").All(i=>!i.gameObject.activeInHierarchy),Is.True);
        }

        [UnityTest] public IEnumerator PremiumFastReducedPauseAndBusyReloadPreserveOutcome()
        {
            yield return Harness("Prepare",ModelState.Fresh(2));
            var haptics=ObserveSlotHaptics();
            Session.SelectExpansionSpeed("speed.fast",Session.State.sequence,1000);Call("Render",null,null);Call("SetRewardFeedbackReduced",true);
            var images=Field<Image[]>("reelImages");var rest=images.Select(i=>i.rectTransform.anchoredPosition).ToArray();
            yield return Click("spinButton");string committed=State;var files=Files();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(images.Select(i=>i.rectTransform.anchoredPosition),Is.EqualTo(rest));Assert.That(Field<Image[]>("reelIncoming").All(i=>!i.enabled),Is.True);
            Call("OnApplicationPause",true);double paused=Field<double>("presentationElapsed");yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Field<double>("presentationElapsed"),Is.EqualTo(paused));Call("OnApplicationPause",false);
            yield return HouseFinish();Unchanged(committed,files);NoFlyers();Assert.That(Field<Image[]>("rewardImpact").All(i=>i==null),Is.True);
            Assert.That(Field<Image>("rewardBurst"),Is.Null);
            Assert.That(Field<AudioSource>("slotRollSource"),Is.Null,"Reduced effects must leave the slot silent");
            Assert.That(Field<AudioSource>("slotAccentSource"),Is.Null,"Muted large wins create no accent playback");
            Assert.That(VisibleCoins(),Is.EqualTo(60));
            Assert.That(Session.State.coins,Is.EqualTo(60));Assert.That(Session.State.seed,Is.EqualTo(697882754u));
            Call("SetRewardFeedbackReduced",false);yield return Click("spinButton");committed=State;files=Files();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Field<AudioSource>("slotRollSource").isPlaying,Is.True);
            Call("Reload");yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(Field<AudioSource>("slotRollSource").isPlaying,Is.False);
            Assert.That(haptics,Is.Empty,"Reduced effects and Reload before reveal must not dispatch a cue");
            Unchanged(committed,files);NoFlyers();Assert.That(Field<Image[]>("reelIncoming").All(i=>!i.enabled),Is.True);
            Assert.That(images.Select(i=>i.rectTransform.anchoredPosition),Is.EqualTo(rest));
            TestContext.WriteLine("Saved fast1.5s, reduced no translations/gold; paused clock unchanged; Busy Reload preserves exact full committed snapshot and rejects old animation.");
        }
    }
}
#endif
