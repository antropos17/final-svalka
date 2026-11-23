using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JunkToNeon.Core;
using JunkToNeon.Contracts;

if(args.Length!=2 || args[0]!="--workspace")throw new ArgumentException("Use --workspace with the explicit isolated absolute X: checkout");
var files=new PrivateFiles(args[1]);
var codec=new JsonStateCodec();
var cases=new List<object>();
string currentCase="initialization";
string fixture=Path.Combine(files.Workspace,"Checks/Milestones/fixtures/late23-skills6.json");
PrivateFiles.RejectReparse(fixture);
string Hash(string path){return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));}
var sourceHashes=new Dictionary<string,string>();
foreach(string module in new[]{"Core","Contracts","Content","Effects","Upgrades","Orders","Guidance"})
    foreach(string path in Directory.GetFiles(Path.Combine(files.Workspace,"Unity/Assets/Scripts",module),"*.cs")){
        PrivateFiles.RejectReparse(path);sourceHashes.Add(Path.GetRelativePath(files.Workspace,path).Replace('\\','/'),Hash(path));
    }
string fixtureHash=Hash(fixture),original=File.ReadAllText(fixture);
var harnessHashes=new Dictionary<string,string>();
foreach(string name in new[]{"Program.cs","AuditedStorage.cs","PrivateFiles.cs","MilestoneStorageChecks.csproj","Directory.Build.props"})
    harnessHashes.Add(name,Hash(Path.Combine(files.Workspace,"Checks/MilestoneStorage",name)));
string executedAssembly=typeof(GameStore).Assembly.Location;
void Need(bool value,string why){if(!value)throw new Exception(why);}
string Raw(string d,int i){return File.ReadAllText(files.PathAt(d,i));}
bool Exists(string d,int i){return File.Exists(files.PathAt(d,i));}
bool Same(Dictionary<string,string> a,Dictionary<string,string> b){return a.Count==b.Count&&a.All(x=>b.TryGetValue(x.Key,out string v)&&v==x.Value);}
GameSession Open(AuditedStorage storage){return GameSession.Open(new GameStore(storage,codec),1,initializeJourney:true);}
void Facts(GameSession game,int count,bool final){
    var m=game.Milestones;
    Need(m.ConstructionCount==count&&m.TotalConstructions==24&&m.ReachedCount==(final?4:3),"Incorrect current-save construction/reached facts");
    string[] ids={"milestone.house-repaired","milestone.workshop-built","milestone.junkyard-restored","milestone.chapter-restored"};
    Need(m.Milestones.Count==4,"Incorrect milestone cardinality");
    for(int i=0;i<4;i++)Need(m.Milestones[i].Definition.Id==ids[i]&&m.Milestones[i].Reached==(i<3||final),"Incorrect stable milestone fact");
}
void Wallet(GameSession game,long coins,long sequence){
    var state=game.State;
    Need(!game.RecoveryRequired&&state.pending==null&&state.coins==coins&&state.sequence==sequence&&state.seed==2647435461u,"Unexpected authoritative wallet/seed/sequence");
    Need(state.chapter.Skills.Receipts.Count==6&&state.lastSpin.sequence==34,"Historical skill/spin receipt changed");
}
object Sharing(Action action){
    try{action();}catch(IOException failure){Need((failure.HResult&65535)==32,"Expected specifically Windows sharing violation32");return new{type=failure.GetType().Name,hresult=failure.HResult,win32Code=failure.HResult&65535};}
    throw new Exception("Expected private deny-delete replacement failure");
}
object BlockedQueries(GameSession game,AuditedStorage storage,string directory){
    var inventory=files.Inventory(directory);int writes=storage.WriteAttempts,preserves=storage.PreserveCalls,reads=storage.ReadAttempts;
    string error=game.Error,before=codec.Encode(game.State);
    for(int i=0;i<3;i++){
        bool refused=false;try{_ = game.Milestones;}catch(InvalidOperationException){refused=true;}
        Need(refused,"Uncertain session exposed a successful milestone projection");
    }
    Need(game.RecoveryRequired&&game.Error==error&&codec.Encode(game.State)==before,"Rejected query changed error or economic state");
    Need(storage.WriteAttempts==writes&&storage.PreserveCalls==preserves&&storage.ReadAttempts==reads&&Same(inventory,files.Inventory(directory)),"Rejected query changed storage/evidence");
    return new{queries=3,readDelta=0,writeDelta=0,errorUnchanged=true,stateUnchanged=true,inventory};
}
void Receipt(bool passed,Exception error=null){
    var result=new{status=passed?"PASS":"FAIL",sourceHashes,fixtureSha256=fixtureHash,harnessHashes,executedAssemblySha256=Hash(executedAssembly),
        filesystem=new DriveInfo(Path.GetPathRoot(files.Workspace)).DriveFormat,os=Environment.OSVersion.VersionString,privateRoot=files.Root,cases,currentCase,
        fixtureBytes=files.CheckBudget(),failure=error==null?null:new{type=error.GetType().Name,error.HResult,description=error.Message},
        scope="Two actual private Windows FileSnapshotStorage sharing-failure cases for the Milestones API; no Android, power-loss, process-kill or ordinary-save acceptance"};
    File.WriteAllText(Path.Combine(files.Root,"receipt.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
}
try{
    var input=codec.Decode(original);
    Need(sourceHashes.Count==48&&fixtureHash=="024AF09393E3FABCE2A1193B6A458674B849A5AF173FA6F7F2ED3AC293312BFC","Unexpected source count or accepted fixture identity");
    Need(input.coins==10657&&input.sequence==34&&input.seed==2647435461u&&input.pending==null&&input.chapter.SelectedProjectId==null,"Unexpected late23 fixture");
    string target=ChapterIds.Project(EraIds.WorkshopYard,FamilyIds.PowerSource,2);
    currentCase="reservation-replacement-denied";
    string directory=files.Case(currentCase);files.Put(directory,0,original);
    using(var storage=new AuditedStorage(files,directory)){
        var game=Open(storage);Wallet(game,10657,34);Facts(game,23,false);
        var initial=files.Inventory(directory);storage.HoldPrimary();
        object failure=Sharing(()=>game.SelectChapterProject(target,35));
        Need(game.RecoveryRequired&&Raw(directory,0)==original&&Exists(directory,2),"Reservation failure changed original or lost temp");
        Need(!Exists(directory,1)||Raw(directory,1)==original,"Denied replacement produced unrelated backup");
        string reserved=Raw(directory,2);var frozen=codec.Decode(reserved);
        Need(frozen.coins==10657&&frozen.sequence==34&&frozen.pending?.chapter?.Price==700&&frozen.pending.chapter.CoinsAfter==9957&&frozen.pending.sequence==35,"Flushed reservation is not the exact700 final purchase");
        object blocked=BlockedQueries(game,storage,directory);storage.Release();game=Open(storage);
        Wallet(game,10657,34);Facts(game,23,false);
        Need(Raw(directory,0)==original&&!Exists(directory,2)&&Raw(directory,3)==reserved&&(!Exists(directory,1)||Raw(directory,1)==original),"Reopen failed to preserve authoritative23 and exact interrupted reservation");
        var recovered=files.Inventory(directory);int writes=storage.WriteAttempts;
        for(int i=0;i<2;i++){game=Open(storage);Facts(game,23,false);Wallet(game,10657,34);}
        Need(writes==storage.WriteAttempts&&Same(recovered,files.Inventory(directory)),"Healthy23 reopens/queries wrote");
        cases.Add(new{name=currentCase,passed=true,expectedFailure=failure,blockedQueries=blocked,initialInventory=initial,reopenedInventory=recovered,
            reopenedCoins=10657,constructionCount=23,writeAttempts=storage.WriteAttempts,unpromotedReservationPaid=false});
    }
    currentCase="commit-replacement-denied";
    directory=files.Case(currentCase);files.Put(directory,0,original);
    using(var storage=new AuditedStorage(files,directory)){
        var game=Open(storage);Wallet(game,10657,34);Facts(game,23,false);storage.LockAfterNextWrite=true;
        object failure=Sharing(()=>game.SelectChapterProject(target,35));
        Need(game.RecoveryRequired&&storage.Writes.Count==1&&storage.WriteAttempts==2,"Expected one successful reservation then denied commit");
        string reservation=storage.Writes[0].Primary,applied=Raw(directory,2);
        Need(Raw(directory,0)==reservation&&Raw(directory,1)==original&&codec.Decode(reservation).pending?.sequence==35,"Uncertain primary/backup not exact reservation/original");
        var after=codec.Decode(applied);Need(after.coins==9957&&after.sequence==35&&after.pending==null&&after.seed==2647435461u,"Applied temporary result differs from exact final debit");
        object blocked=BlockedQueries(game,storage,directory);storage.Release();game=Open(storage);
        Wallet(game,9957,35);Facts(game,24,true);
        Need(Raw(directory,0)==applied&&Raw(directory,1)==reservation&&!Exists(directory,2)&&Raw(directory,3)==applied,"Recovery changed exact committed/backup/interrupted bytes");
        Need(storage.WriteAttempts==3,"Pending recovery must perform exactly one normal final write");
        var recovered=files.Inventory(directory);int writes=storage.WriteAttempts;
        string committed=codec.Encode(game.State);
        for(int i=0;i<3;i++){game=Open(storage);for(int j=0;j<3;j++)Facts(game,24,true);Wallet(game,9957,35);Need(codec.Encode(game.State)==committed,"Repeated query changed achievement/history/state");}
        Need(writes==storage.WriteAttempts&&Same(recovered,files.Inventory(directory)),"Recovered reopen/passive query wrote or paid twice");
        cases.Add(new{name=currentCase,passed=true,expectedFailure=failure,blockedQueries=blocked,recoveredInventory=recovered,
            finalCoins=9957,debit=700,constructionCount=24,repeatedReopens=3,passiveQueries=9,replayWrites=0,writeAttempts=storage.WriteAttempts});
    }
    currentCase="complete";
    Need(Hash(fixture)==fixtureHash,"Original accepted fixture changed");
    foreach(var source in sourceHashes)Need(Hash(Path.Combine(files.Workspace,source.Key))==source.Value,"Production source changed");
    Receipt(true);Console.WriteLine("PASS two milestone storage groups; receipt "+Path.Combine(files.Root,"receipt.json"));
}catch(Exception failure){Receipt(false,failure);Console.Error.WriteLine("FAIL "+currentCase+": "+failure.Message+"; originals preserved at "+files.Root);throw;}