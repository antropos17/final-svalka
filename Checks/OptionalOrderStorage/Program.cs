using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JunkToNeon.Core;

if(args.Length!=2||args[0]!="--workspace")throw new ArgumentException("Use --workspace with the explicit isolated absolute X: checkout");
var files=new PrivateFiles(args[1]);var codec=new JsonStateCodec();var cases=new List<object>();string currentCase="initialization";
string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
void Need(bool value,string text){if(!value)throw new Exception(text);}
string readyPath=Path.Combine(files.Workspace,"Checks/OptionalOrderStorage/fixtures/ready-sort-no-claim.json");
string earlierPath=Path.Combine(files.Workspace,"Checks/OptionalOrderStorage/fixtures/earlier-no-order.json");
PrivateFiles.RejectReparse(readyPath);PrivateFiles.RejectReparse(earlierPath);
string original=File.ReadAllText(readyPath),earlier=File.ReadAllText(earlierPath);
var input=codec.Decode(original);var oldInput=codec.Decode(earlier);
var fixtureHashes=new Dictionary<string,string>{{"ready-sort-no-claim.json",Hash(readyPath)},{"earlier-no-order.json",Hash(earlierPath)}};
var sources=new Dictionary<string,string>();
foreach(string module in new[]{"Core","Contracts","Content","Effects","Upgrades","Orders","Guidance"})
 foreach(string p in Directory.GetFiles(Path.Combine(files.Workspace,"Unity/Assets/Scripts",module),"*.cs")){PrivateFiles.RejectReparse(p);sources.Add(Path.GetRelativePath(files.Workspace,p).Replace('\\','/'),Hash(p));}
var harness=new Dictionary<string,string>();
foreach(string name in new[]{"Program.cs","PrivateFiles.cs","OptionalOrderStorageChecks.csproj","Directory.Build.props"})harness.Add("Checks/OptionalOrderStorage/"+name,Hash(Path.Combine(files.Workspace,"Checks/OptionalOrderStorage",name)));
harness.Add("Checks/MilestoneStorage/AuditedStorage.cs",Hash(Path.Combine(files.Workspace,"Checks/MilestoneStorage/AuditedStorage.cs")));
string assembly=typeof(GameStore).Assembly.Location;
string Raw(string d,int i)=>File.ReadAllText(files.PathAt(d,i));
bool Exists(string d,int i)=>File.Exists(files.PathAt(d,i));
bool Same(Dictionary<string,string> a,Dictionary<string,string> b)=>a.Count==b.Count&&a.All(p=>b.TryGetValue(p.Key,out string v)&&v==p.Value);
GameSession Open(AuditedStorage storage)=>GameSession.Open(new GameStore(storage,codec),1,initializeJourney:true);
void Facts(GameSession game,bool collected){var snapshot=game.OptionalAchievements;
 Need(snapshot.Achievements.Count==2&&snapshot.Achievements[0].Definition.Id=="optional.all-skills-owned"&&!snapshot.Achievements[0].Reached,"Zero-skill fixture acquired an invented skill achievement");
 Need(snapshot.Achievements[1].Definition.Id=="optional.retained-order-claim"&&snapshot.Achievements[1].Reached==collected&&snapshot.ReachedCount==(collected?1:0),"Incorrect current retained-claim achievement");}
void State(GameSession game,bool collected){var s=game.State;
 Need(!game.RecoveryRequired&&s.pending==null&&s.coins==(collected?1182:1062)&&s.sequence==(collected?44:43),"Wrong confirmed claim wallet/sequence");
 using(var a=JsonDocument.Parse(original))using(var b=JsonDocument.Parse(codec.Encode(s)))foreach(string key in new[]{"seed","chapter","wave","lastSpin","houseStage","workshopBuilt","selectedStage","lastSettledAt"})
  Need(a.RootElement.GetProperty(key).GetRawText()==b.RootElement.GetProperty(key).GetRawText(),"Claim changed preserved economic context: "+key);
 var orders=s.journey.Orders;
 Need(collected?orders.ActiveOrderId==null&&orders.LatestClaim!=null&&orders.LatestClaim.OrderId=="order.sort-salvage"&&orders.LatestClaim.InstanceSequence==27&&orders.LatestClaim.ClaimSequence==44&&orders.LatestClaim.RewardCoins==120:
  orders.Ready&&orders.InstanceSequence==27&&orders.LatestClaim==null,"Wrong ready/retained claim identity");Facts(game,collected);}
object Query(GameSession game,AuditedStorage storage,string directory,bool blocked,bool collected){
 var inventory=files.Inventory(directory);string before=codec.Encode(game.State),error=game.Error;int reads=storage.ReadAttempts,writes=storage.WriteAttempts,preserves=storage.PreserveCalls;
 for(int i=0;i<3;i++){if(blocked){bool refused=false;try{_ = game.OptionalAchievements;}catch(InvalidOperationException){refused=true;}Need(refused,"Uncertain session certified an achievement");}else Facts(game,collected);}
 Need(game.Error==error&&codec.Encode(game.State)==before&&storage.ReadAttempts==reads&&storage.WriteAttempts==writes&&storage.PreserveCalls==preserves&&Same(inventory,files.Inventory(directory)),"Query changed state/error or performed storage IO");
 return new{queries=3,blocked,readDelta=0,writeDelta=0,preserveDelta=0,stateErrorUnchanged=true,inventory};}
object Sharing(Action action){try{action();}catch(IOException error){Need((error.HResult&65535)==32,"Expected specifically Windows sharing32");return new{type=error.GetType().Name,hresult=error.HResult,win32Code=error.HResult&65535};}throw new Exception("Expected private deny-delete replacement failure");}
object Replays(GameSession game,AuditedStorage storage,string directory){var inventory=files.Inventory(directory);int writes=storage.WriteAttempts;string committed=codec.Encode(game.State);
 for(int i=0;i<3;i++){game=Open(storage);State(game,true);Query(game,storage,directory,false,true);game.ClaimOrder("order.sort-salvage",27,44);Need(codec.Encode(game.State)==committed,"Exact retained claim replay changed state/history");}
 Need(storage.WriteAttempts==writes&&Same(inventory,files.Inventory(directory)),"Repeated reopen/query/exact claim replay wrote or paid again");
 // Pure projection of an exact older valid save is a current fact, not lifetime history.
 var oldFacts=OptionalAchievements.Project(ChapterEconomy.Project(oldInput),JourneyEconomy.Project(oldInput));
 Need(oldInput.coins==690&&oldInput.sequence==26&&!oldFacts.Achievements[0].Reached&&!oldFacts.Achievements[1].Reached,"Earlier valid snapshot retained a lifetime achievement");
 State(game,true);return new{reopens=3,passiveQueries=9,exactClaimReplays=3,writeDelta=0,olderExactSnapshotProjectsFalse=true,inventory};}
void Receipt(bool passed,Exception error=null){File.WriteAllText(Path.Combine(files.Root,"receipt.json"),JsonSerializer.Serialize(new{status=passed?"PASS":"FAIL",sources,fixtureHashes,harness,
 executedAssemblySha256=Hash(assembly),filesystem=new DriveInfo(Path.GetPathRoot(files.Workspace)).DriveFormat,os=Environment.OSVersion.VersionString,privateRoot=files.Root,cases,currentCase,fixtureBytes=files.CheckBudget(),
 failure=error==null?null:new{type=error.GetType().Name,error.HResult,description=error.Message},scope="Two actual private Windows order-claim sharing failures; no Android/power-loss/ordinary-save acceptance; older-save projection is not a physical backup-restore test"},new JsonSerializerOptions{WriteIndented=true}));}
try{
 Need(sources.Count==50&&fixtureHashes["ready-sort-no-claim.json"]=="F438F5618D1FCC08F05909E4A34A926B3C28F08C9B03DC6D7C1C3559799E8D17"&&fixtureHashes["earlier-no-order.json"]=="2064DB59C0F2ED5C65AD55988D71931651E06E1256B00D01A4E2631BE7C3CE65","Accepted source/fixture binding differs");
 Need(input.coins==1062&&input.sequence==43&&input.seed==2371448660u&&input.journey.Orders.Ready&&input.journey.Orders.InstanceSequence==27&&input.journey.Orders.LatestClaim==null,"Accepted ready fixture changed");
 currentCase="reservation-replacement-denied";string directory=files.Case(currentCase);files.Put(directory,0,original);
 using(var storage=new AuditedStorage(files,directory)){
  var game=Open(storage);State(game,false);object readyQuery=Query(game,storage,directory,false,false);storage.HoldPrimary();object failure=Sharing(()=>game.ClaimOrder("order.sort-salvage",27,44));
  Need(game.RecoveryRequired&&Raw(directory,0)==original&&Exists(directory,2)&&(!Exists(directory,1)||Raw(directory,1)==original),"Denied reservation changed authoritative ready state");
  string reservation=Raw(directory,2);var frozen=codec.Decode(reservation);
  Need(frozen.coins==1062&&frozen.sequence==43&&frozen.pending?.journey?.Reward==120&&frozen.pending.journey.CoinsAfter==1182&&frozen.pending.sequence==44,"Wrong flushed unpromoted claim");
  object blockedQuery=Query(game,storage,directory,true,false);storage.Release();game=Open(storage);State(game,false);
  Need(Raw(directory,0)==original&&!Exists(directory,2)&&Raw(directory,3)==reservation,"Reopen paid an unpromoted reservation or lost evidence");
  object reopenedReady=Query(game,storage,directory,false,false);var beforeNewIntent=files.Inventory(directory);
  // This is a separate explicit new claim after healthy reopen, never an auto-retry.
  game.ClaimOrder("order.sort-salvage",27,44);State(game,true);
  Need(storage.WriteAttempts==3&&Raw(directory,3)==reservation&&Raw(directory,1)==reservation&&Raw(directory,0)==codec.Encode(GameLogic.ApplyPending(frozen)),"Fresh explicit claim changed exact result/evidence");
  var replays=Replays(game,storage,directory);cases.Add(new{name=currentCase,passed=true,expectedFailure=failure,readyQuery,blockedQuery,reopenedReady,beforeNewIntent,unpromotedReservationPaid=false,
   explicitNewClaimReward=120,finalCoins=1182,writeAttempts=storage.WriteAttempts,replays});
 }
 currentCase="commit-replacement-denied";directory=files.Case(currentCase);files.Put(directory,0,original);
 using(var storage=new AuditedStorage(files,directory)){
  var game=Open(storage);State(game,false);storage.LockAfterNextWrite=true;object failure=Sharing(()=>game.ClaimOrder("order.sort-salvage",27,44));
  Need(game.RecoveryRequired&&storage.Writes.Count==1&&storage.WriteAttempts==2,"Expected successful reservation then denied commit");
  string reservation=storage.Writes[0].Primary,applied=Raw(directory,2);var pending=codec.Decode(reservation);var paid=codec.Decode(applied);
  Need(Raw(directory,0)==reservation&&Raw(directory,1)==original&&pending.pending?.sequence==44&&paid.coins==1182&&paid.sequence==44&&paid.pending==null&&paid.journey.Orders.LatestClaim?.RewardCoins==120,"Uncertain file identities differ from exact pending/applied claim");
  object blockedQuery=Query(game,storage,directory,true,false);storage.Release();game=Open(storage);State(game,true);
  Need(storage.WriteAttempts==3&&Raw(directory,0)==applied&&Raw(directory,1)==reservation&&!Exists(directory,2)&&Raw(directory,3)==applied,"Recovery did not preserve/apply exact frozen claim once");
  var replays=Replays(game,storage,directory);cases.Add(new{name=currentCase,passed=true,expectedFailure=failure,blockedQuery,recoveryReward=120,finalCoins=1182,writeAttempts=storage.WriteAttempts,replays});
 }
 Need(Hash(readyPath)==fixtureHashes["ready-sort-no-claim.json"]&&Hash(earlierPath)==fixtureHashes["earlier-no-order.json"],"Original accepted fixture changed");
 foreach(var pair in sources)Need(Hash(Path.Combine(files.Workspace,pair.Key))==pair.Value,"Production source changed");
 currentCase="complete";Receipt(true);Console.WriteLine("PASS two optional-order storage groups; receipt "+Path.Combine(files.Root,"receipt.json"));
}catch(Exception error){Receipt(false,error);Console.Error.WriteLine("FAIL "+currentCase+": "+error.Message+"; evidence preserved at "+files.Root);throw;}