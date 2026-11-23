using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JunkToNeon.Core;

if(args.Length!=4||args[0]!="--case"||args[1]!="claim-backup-rollback"||args[2]!="--workspace")throw new ArgumentException("Use --case claim-backup-rollback --workspace <explicit source-only X: checkout>");
var files=new PrivateFiles(args[3]);var codec=new JsonStateCodec();string currentCase="initialization";var results=new List<object>();int opens=0;
string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
void Need(bool condition,string text){if(!condition)throw new Exception(text);}
var fixtures=new Dictionary<string,string>();
foreach(string name in new[]{"ready-sort-no-claim.json","earlier-no-order.json"}){string p=Path.Combine(files.Workspace,"Checks/OptionalOrderStorage/fixtures",name);PrivateFiles.RejectReparse(p);fixtures.Add(name,Hash(p));}
string original=File.ReadAllText(Path.Combine(files.Workspace,"Checks/OptionalOrderStorage/fixtures/ready-sort-no-claim.json"));
string older=File.ReadAllText(Path.Combine(files.Workspace,"Checks/OptionalOrderStorage/fixtures/earlier-no-order.json"));
var sources=new Dictionary<string,string>();foreach(string module in new[]{"Core","Contracts","Content","Effects","Upgrades","Orders","Guidance"})foreach(string p in Directory.GetFiles(Path.Combine(files.Workspace,"Unity/Assets/Scripts",module),"*.cs")){PrivateFiles.RejectReparse(p);sources.Add(Path.GetRelativePath(files.Workspace,p).Replace('\\','/'),Hash(p));}
var harness=new Dictionary<string,string>();foreach(string name in new[]{"Program.cs","PrivateFiles.cs","ClaimBackupRollbackChecks.csproj","Directory.Build.props"})harness.Add("Checks/ClaimBackupRollback/"+name,Hash(Path.Combine(files.Workspace,"Checks/ClaimBackupRollback",name)));
harness.Add("Checks/MilestoneStorage/AuditedStorage.cs",Hash(Path.Combine(files.Workspace,"Checks/MilestoneStorage/AuditedStorage.cs")));
string assembly=typeof(GameStore).Assembly.Location;
string Raw(string d,int i)=>File.ReadAllText(files.PathAt(d,i));
bool Exists(string d,int i)=>File.Exists(files.PathAt(d,i));
bool Same(Dictionary<string,string> a,Dictionary<string,string> b)=>a.Count==b.Count&&a.All(p=>b.TryGetValue(p.Key,out string v)&&v==p.Value);
GameSession Open(AuditedStorage storage){opens++;return GameSession.Open(new GameStore(storage,codec),1,initializeJourney:true);}
void ReadyFalse(GameSession game){Need(!game.RecoveryRequired&&game.Error==null&&codec.Encode(game.State)==original,"Confirmed snapshot differs from the exact chosen ready backup");var a=game.OptionalAchievements;
 Need(a.Achievements.Count==2&&a.Achievements[0].Definition.Id=="optional.all-skills-owned"&&!a.Achievements[0].Reached&&a.Achievements[1].Definition.Id=="optional.retained-order-claim"&&!a.Achievements[1].Reached&&a.ReachedCount==0,"Rollback retained a fabricated/lifetime optional fact");}
object Queries(GameSession game,AuditedStorage storage,string d,bool blocked){var inventory=files.Inventory(d);string before=codec.Encode(game.State),error=game.Error;int reads=storage.ReadAttempts,writes=storage.WriteAttempts,preserves=storage.PreserveCalls,restores=storage.RestoreAttempts;
 for(int i=0;i<3;i++){if(blocked){bool refused=false;try{_ = game.OptionalAchievements;}catch(InvalidOperationException){refused=true;}Need(refused,"Uncertain claim exposed a successful achievement query");}else ReadyFalse(game);}
 Need(before==codec.Encode(game.State)&&error==game.Error&&reads==storage.ReadAttempts&&writes==storage.WriteAttempts&&preserves==storage.PreserveCalls&&restores==storage.RestoreAttempts&&Same(inventory,files.Inventory(d)),"Query performed IO or changed full state/error/evidence");
 return new{queries=3,blocked,readDelta=0,writeDelta=0,preserveDelta=0,restoreDelta=0,inventory};}
void Receipt(bool pass,Exception error=null){File.WriteAllText(Path.Combine(files.Root,"receipt.json"),JsonSerializer.Serialize(new{status=pass?"PASS":"FAIL",sources,fixtures,harness,executedAssemblySha256=Hash(assembly),
 filesystem=new DriveInfo(Path.GetPathRoot(files.Workspace)).DriveFormat,os=Environment.OSVersion.VersionString,privateRoot=files.Root,results,currentCase,fixtureBytes=files.CheckBudget(),failure=error==null?null:new{type=error.GetType().Name,error.HResult,description=error.Message},
 scope="One explicit private Windows backup rollback after uncertain claim; no UI/corruption/Android/power-loss acceptance, no lifetime anti-replay guarantee"},new JsonSerializerOptions{WriteIndented=true}));}
try{
 Need(sources.Count==50&&fixtures["ready-sort-no-claim.json"]=="F438F5618D1FCC08F05909E4A34A926B3C28F08C9B03DC6D7C1C3559799E8D17"&&fixtures["earlier-no-order.json"]=="2064DB59C0F2ED5C65AD55988D71931651E06E1256B00D01A4E2631BE7C3CE65","Accepted source/fixture binding differs");
 var ready=codec.Decode(original);var initialOlder=codec.Decode(older);
 Need(codec.Encode(ready)==original&&codec.Encode(initialOlder)==older&&ready.coins==1062&&ready.sequence==43&&ready.seed==2371448660u&&ready.journey.Orders.Ready&&ready.journey.Orders.InstanceSequence==27&&ready.journey.Orders.LatestClaim==null&&initialOlder.coins==690&&initialOlder.sequence==26,"Unexpected accepted ready/older snapshot");
 currentCase="claim-backup-rollback";string d=files.Case(currentCase);files.Put(d,0,original);files.Put(d,1,older);
 using(var storage=new AuditedStorage(files,d)){
  var game=Open(storage);ReadyFalse(game);Need(game.BackupAvailable&&storage.WriteAttempts==0,"Initial valid older backup/funded primary did not open read-only");var initialInventory=files.Inventory(d);
  storage.LockAfterNextWrite=true;object failure=null;
  try{game.ClaimOrder("order.sort-salvage",27,44);}catch(IOException error){Need((error.HResult&65535)==32,"Expected specifically Windows sharing32");failure=new{type=error.GetType().Name,hresult=error.HResult,win32Code=error.HResult&65535};}
  Need(failure!=null&&game.RecoveryRequired&&storage.WriteAttempts==2&&storage.Writes.Count==1,"Expected durable reservation then failed final replacement");
  string pending=Raw(d,0),chosen=Raw(d,1),applied=Raw(d,2);var pendingState=codec.Decode(pending);var appliedState=codec.Decode(applied);
  Need(chosen==original&&chosen!=older,"Reservation must roll backup to previous ready1062 primary, not retain the initial690 backup");
  Need(pendingState.coins==1062&&pendingState.sequence==43&&pendingState.pending?.journey?.Reward==120&&pendingState.pending.sequence==44&&appliedState.pending==null&&appliedState.coins==1182&&appliedState.sequence==44&&appliedState.journey.Orders.LatestClaim?.RewardCoins==120,"Uncertain primary/applied snapshots differ from exact claim terms");
  var blockedQueries=Queries(game,storage,d,true);storage.Release();
  Need(opens==1,"Do not normal-Open/recover the pending primary before explicit rollback");
  var restored=game.RestoreBackup();ReadyFalse(game);
  Need(storage.RestoreAttempts==1&&storage.WriteAttempts==2&&codec.Encode(restored)==chosen,"Explicit rollback changed the chosen validated backup or used a normal write");
  Need(Raw(d,0)==chosen&&Raw(d,1)==chosen&&Raw(d,4)==pending&&Raw(d,5)==chosen&&Raw(d,6)==applied&&!Exists(d,2)&&!Exists(d,3)&&!Exists(d,7),"Rollback failed exact current-primary/backup/temp preservation or left staged replay candidates");
  var confirmed=files.Inventory(d);bool repeatRefused=false;try{game.RestoreBackup();}catch(InvalidOperationException){repeatRefused=true;}
  Need(repeatRefused&&storage.RestoreAttempts==1&&Same(confirmed,files.Inventory(d)),"Healthy repeat restore wrote/rolled back without a blocked session");
  var healthyQueries=Queries(game,storage,d,false);int writes=storage.WriteAttempts,restores=storage.RestoreAttempts;
  for(int i=0;i<3;i++){game=Open(storage);ReadyFalse(game);Queries(game,storage,d,false);Need(codec.Encode(game.State)==chosen,"Reload resurrected the staged claim or created a fresh game");}
  Need(writes==storage.WriteAttempts&&restores==storage.RestoreAttempts&&Same(confirmed,files.Inventory(d)),"Healthy reload performed a claim/restore/write");
  // Intentional rollback also rolls back sequence/claim history. The identical44
  // request is admissible as a new proposal; do NOT claim lifetime deduplication.
  var proposal=JourneyEconomy.Reserve(game.State,"order-claim","order.sort-salvage",27,44);
  Need(proposal.pending?.sequence==44&&proposal.pending.journey.Reward==120&&codec.Encode(proposal)==pending,"Rollback invented unavailable anti-replay history");
  ReadyFalse(game);Need(Same(confirmed,files.Inventory(d)),"Pure admissibility probe wrote/paid");
  results.Add(new{name=currentCase,passed=true,expectedFailure=failure,initialInventory,initialBackupCoins=690,chosenBackupCoins=1062,blockedQueries,healthyQueries,
   confirmedInventory=confirmed,finalCoins=1062,finalSequence=43,claimAbsent=true,optionalFactsFalse=true,seed=2371448660u,explicitRestores=1,normalWriteAttempts=2,
   reopensAfterRestore=3,replayWrites=0,sameIdentityPureProposalAdmissibleAfterRollback=true,noNewDurableClaimIssued=true});
 }
 foreach(var p in sources)Need(Hash(Path.Combine(files.Workspace,p.Key))==p.Value,"Production source changed");
 foreach(var p in fixtures)Need(Hash(Path.Combine(files.Workspace,"Checks/OptionalOrderStorage/fixtures",p.Key))==p.Value,"Accepted fixture changed");
 currentCase="complete";Receipt(true);Console.WriteLine("PASS explicit claim backup rollback; receipt "+Path.Combine(files.Root,"receipt.json"));
}catch(Exception error){Receipt(false,error);Console.Error.WriteLine("FAIL "+currentCase+": "+error.Message+"; evidence preserved at "+files.Root);throw;}