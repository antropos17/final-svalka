using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JunkToNeon.Core;

static class Program
{
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static string Root;
    sealed class Storage:ISnapshotStorage
    {
        public string Primary,Backup; public int Reads,Writes,FailAt=-1; public bool Land; public Action Observe;
        public string ReadPrimary(){Reads++;return Primary;} public string ReadBackup(){Reads++;return Backup;}
        public void Write(string raw)
        {
            Writes++;Observe?.Invoke();
            if(Writes==FailAt&&!Land)throw new IOException("private before-landing fault");
            Backup=Primary;Primary=raw;if(Writes==FailAt)throw new IOException("private after-landing fault");
        }
    }
    static void Need(bool value,string message){if(!value)throw new Exception(message);}
    static void Reject(Action action){try{action();}catch(ArgumentException){return;}catch(InvalidOperationException){return;}catch(FormatException){return;}catch(OverflowException){return;}throw new Exception("Expected refusal");}
    static GameSession Open(Storage st){var g=GameSession.Open(new GameStore(st,Codec),1);Need(!g.RecoveryRequired,"Open: "+g.Error);return g;}
    static Storage Stored(ModelState s)=>new Storage{Primary=Codec.Encode(s)};
    static ModelState Input(string relative)=>Codec.Decode(File.ReadAllText(Path.Combine(Root,relative)));
    static ModelState Stock(int parts=95,int potatoes=83,long coins=7,ModelState source=null,bool upgraded=false)
    {
        var s=source?.Copy()??GameLogic.ApplyPending(GameLogic.Reserve(ModelState.Fresh(1),"spin",0,1));
        if(s.expansion==null)s=GameLogic.ApplyPending(ExpansionEconomy.Reserve(s,ExpansionEconomy.Quote(s,"speed","speed.fast")));
        var d=new ExpansionDraft(s.expansion){Parts=parts,Potatoes=potatoes};
        if(upgraded)d.Equipment[0]=2;
        s.expansion=d.Freeze();s.coins=coins;GameLogic.Validate(s);return s;
    }
    static void NoIo(Storage st,GameSession g,Action action)
    {
        var p=st.Primary;var b=st.Backup;var state=Codec.Encode(g.State);int r=st.Reads,w=st.Writes;
        Reject(action);Need(p==st.Primary&&b==st.Backup&&state==Codec.Encode(g.State)&&r==st.Reads&&w==st.Writes&&!g.RecoveryRequired,"Refusal changed IO/state/health");
    }
    static void Balances(ModelState s,int parts,int potatoes,long coins,string why)
    {Need(s.expansion.Parts==parts&&s.expansion.Potatoes==potatoes&&s.coins==coins,why);}
    static void Boundaries()
    {
        foreach(string id in new[]{"trade.parts","trade.potatoes"})foreach(int stock in new[]{19,20,39,40,95})
        {
            var s=Stock(stock,stock);var st=Stored(s);var g=Open(st);
            Need(ExpansionEconomy.MaximumTradeBatches(s,id)==stock/20,"Stock max "+stock);
            if(stock<40)NoIo(st,g,()=>g.GetExpansionBatchOffer(id,2));
            else
            {
                var q=g.GetExpansionBatchOffer(id,2);Need(q.BatchCount==2&&q.CoinsReward==40&&q.ExpectedSequence==s.sequence,"Literal batch terms");
                g.BuyExpansionBatch(q);Balances(g.State,id=="trade.parts"?stock-40:stock,id=="trade.potatoes"?stock-40:stock,47,"Quantity2 exact");
            }
            // Quantity one deliberately stays the literal legacy buy path.
            st=Stored(s);g=Open(st);
            if(stock>=20)
            {
                var q=g.GetExpansionOffer(id);Need(q.Action=="buy"&&q.Slot==0&&q.CoinsReward==20,"Legacy quote terms");
                string expected=Codec.Encode(GameLogic.ApplyPending(ExpansionEconomy.Reserve(s,q)));
                g.BuyExpansion(q);Need(Codec.Encode(g.State)==expected&&st.Writes==2,"Quantity1 legacy equivalence");
            }
        }
        foreach(string id in new[]{"trade.parts","trade.potatoes"})
        {
            var s=Stock();var g=Open(Stored(s));g.BuyExpansionBatch(g.GetExpansionBatchOffer(id,3));
            Balances(g.State,id=="trade.parts"?35:95,id=="trade.potatoes"?23:83,67,"Quantity3 exact");
            s=Stock(95,95);g=Open(Stored(s));int max=ExpansionEconomy.MaximumTradeBatches(s,id);Need(max==4,"95 max4");g.BuyExpansionBatch(g.GetExpansionBatchOffer(id,max));
            Balances(g.State,id=="trade.parts"?15:95,id=="trade.potatoes"?15:95,87,"Maximum remainder");
            s=Stock(299,281,7,Input("Checks/OrderVariety/fixtures/potatoes-ready12.json"),true);
            Need(ExpansionEconomy.MaximumTradeBatches(s,id)==14,"Upgraded capacity max14");g=Open(Stored(s));g.BuyExpansionBatch(g.GetExpansionBatchOffer(id,14));
            Balances(g.State,id=="trade.parts"?19:299,id=="trade.potatoes"?1:281,287,"Upgraded remainder");
        }
    }
    static void InvalidAndStale()
    {
        var s=Stock();var st=Stored(s);var g=Open(st);
        foreach(int n in new[]{int.MinValue,-1,0,1,5,int.MaxValue})NoIo(st,g,()=>g.GetExpansionBatchOffer("trade.parts",n));
        foreach(string id in new[]{null,"","module.salvage","trade","trade.energy"})NoIo(st,g,()=>g.GetExpansionBatchOffer(id,2));
        NoIo(st,g,()=>g.BuyExpansionBatch(null));
        var old=g.GetExpansionBatchOffer("trade.parts",3);g.ExecuteChapterSpin(g.State.sequence+1);NoIo(st,g,()=>g.BuyExpansionBatch(old));
        foreach(Action<ModelState> change in new Action<ModelState>[] {x=>x.coins++,x=>{var d=new ExpansionDraft(x.expansion){Energy=2};x.expansion=d.Freeze();},x=>{var d=new ExpansionDraft(x.expansion){Parts=94};x.expansion=d.Freeze();}})
        {
            var changed=s.Copy();change(changed);st=Stored(changed);g=Open(st);var retained=ExpansionEconomy.QuoteBatch(s,"trade.parts",3);NoIo(st,g,()=>g.BuyExpansionBatch(retained));
        }
        s=Stock(95,83,long.MaxValue-39);st=Stored(s);g=Open(st);
        Need(ExpansionEconomy.MaximumTradeBatches(s,"trade.parts")==1,"Coin headroom limits max");NoIo(st,g,()=>g.GetExpansionBatchOffer("trade.parts",2));
        s=Stock(95,83,long.MaxValue-40);g=Open(Stored(s));g.BuyExpansionBatch(g.GetExpansionBatchOffer("trade.parts",2));Need(g.State.coins==long.MaxValue,"Exact coin boundary");
    }
    static void AtomicAndRecovery()
    {
        foreach(string id in new[]{"trade.parts","trade.potatoes"})
        {
            var s=Stock();var st=Stored(s);var g=Open(st);var q=g.GetExpansionBatchOffer(id,3);
            st.Observe=()=>Balances(g.State,95,83,7,"Public partial conversion during journal write");
            g.BuyExpansionBatch(q);Need(st.Writes==2,"One batch requires two writes");st.Observe=null;
            string committed=st.Primary;g.BuyExpansionBatch(q);Need(st.Writes==2&&st.Primary==committed,"Retained replay writes");
            g=Open(st);g.BuyExpansionBatch(q);Need(st.Writes==2&&st.Primary==committed,"Reopened replay writes");
            foreach(int fail in new[]{1,2})foreach(bool landed in new[]{false,true})
            {
                st=Stored(s);g=Open(st);q=g.GetExpansionBatchOffer(id,3);st.FailAt=fail;st.Land=landed;
                bool threw=false;try{g.BuyExpansionBatch(q);}catch(IOException){threw=true;}Need(threw&&g.RecoveryRequired,"Fault must block session");
                Balances(g.State,95,83,7,"Failed transaction publicly converted");
                st.FailAt=-1;g=Open(st);bool applied=fail==2||landed;
                Balances(g.State,applied&&id=="trade.parts"?35:95,applied&&id=="trade.potatoes"?23:83,applied?67:7,"Recovery exact frozen result");
                if(applied){int writes=st.Writes;string raw=st.Primary;g.BuyExpansionBatch(q);Open(st);Need(st.Writes==writes&&st.Primary==raw,"Recovery duplicated credit");}
            }
        }
    }
    static void CodecAndIsolation()
    {
        foreach(string fixture in new[]{"Checks/Milestones/fixtures/v2-selected540.json","Checks/EraMigration/R8/Fixtures/pending-canonical-spin.json","Checks/ArcadeCore/Evidence/memory-partial.json"})
        {string raw=File.ReadAllText(Path.Combine(Root,fixture)).Trim();Need(Codec.Encode(Codec.Decode(raw))==raw,"Historical bytes changed "+fixture);}
        foreach(ModelState original in new[]{Input("Checks/Milestones/fixtures/v2-selected540.json"),Legacy600(),Input("Checks/ArcadeCore/Evidence/memory-partial.json")})
        {
            var s=Stock(95,83,7,original);var q=ExpansionEconomy.QuoteBatch(s,"trade.parts",3);var pending=ExpansionEconomy.ReserveBatch(s,q);
            string raw=Codec.Encode(pending);Need(Codec.Encode(Codec.Decode(raw))==raw,"Pending batch roundtrip");
            string token=ExpansionCodec.EncodeOperation(pending.pending.expansion);Need(token.StartsWith("expansion.op.v4~"),"Batch dispatch must win over state format");
            foreach(int field in new[]{0,1,2,3,6,7,10,11,12})
            {
                var f=token.Split('~');f[field]=field==0?"expansion.op.v1":field==1?ExpansionCatalog.RuleRevision:field==2?"expansion":field==3?"0":field==10||field==11?"999":f[field]+"!";
                f[f.Length-1]=ExpansionCodec.Hash(string.Join("~",f.Take(f.Length-1)));
                string forged=string.Join("~",f);Reject(()=>{var x=pending.Copy();x.pending.expansion=ExpansionCodec.DecodeOperation(forged);GameLogic.Validate(x);});
            }
            for(int i=0;i<token.Length;i++)if(token[i]=='~')
            {string malformed=token.Substring(0,i)+"!"+token.Substring(i+1);Reject(()=>ExpansionCodec.DecodeOperation(malformed));}
            var after=GameLogic.ApplyPending(Codec.Decode(raw));Balances(after,35,83,67,"Codec frozen result");
            var expected=s.Copy();expected.coins=67;expected.sequence=after.sequence;expected.revision=after.revision;expected.pending=null;expected.expansion=after.expansion;
            Need(Codec.Encode(expected)==Codec.Encode(after),"Unrelated lower state/RNG/selected price/ACK changed");
            var beforeDraft=new ExpansionDraft(s.expansion);var afterDraft=new ExpansionDraft(after.expansion);afterDraft.Parts=beforeDraft.Parts;afterDraft.LastCommand=beforeDraft.LastCommand;
            Need(ExpansionCodec.EncodeState(afterDraft.Freeze())==ExpansionCodec.EncodeState(s.expansion),"Unrelated expansion state/receipt/RNG changed");
        }
    }
    static ModelState Legacy600(){var s=ModelState.Fresh(1);s.coins=7;s.houseStage=1;s.autoHintSeen=1;s.selectedStage=2;GameLogic.Validate(s);return s;}
    static int Main(string[] args)
    {
        try{Root=Path.GetFullPath(args.Length>0?args[0]:Directory.GetCurrentDirectory());
            var groups=new Dictionary<string,Action>{{"boundaries",Boundaries},{"invalid-stale-overflow",InvalidAndStale},{"atomic-recovery-replay",AtomicAndRecovery},{"codec-isolation",CodecAndIsolation}};
            foreach(var group in groups){if(args.Length>1&&args[1]!=group.Key)continue;group.Value();Console.WriteLine("PASS "+group.Key);}Console.WriteLine("PASS independent batch groups; synthetic private memory storage; no Unity/device acceptance");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
