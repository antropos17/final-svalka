using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JunkToNeon.Contracts;

namespace JunkToNeon.Core
{
    // Bounded canonical payloads inside the existing JSON envelope; no extra file,
    // unbounded history or relaxation of the owning JSON parser's depth/array limits.
    public static class ExpansionCodec
    {
        public const int MaximumStateCharacters=4096, MaximumOperationCharacters=12288;
        internal static string N(long value){return value.ToString(CultureInfo.InvariantCulture);}
        static long L(string value)
        {
            if(!long.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out long parsed)||N(parsed)!=value)throw new FormatException("Noncanonical expansion integer");
            return parsed;
        }
        static int I(string value){return checked((int)L(value));}
        static uint U(string value){return checked((uint)L(value));}
        static bool B(string value){if(value=="0")return false;if(value=="1")return true;throw new FormatException("Invalid expansion flag");}
        internal static bool IsKey(string value)
        {if(value==null||value.Length!=64)return false;foreach(char c in value)if(!(c>='0'&&c<='9')&&!(c>='a'&&c<='f'))return false;return true;}
        internal static string Hash(string value)
        {
            byte[] bytes;using(var sha=SHA256.Create())bytes=sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var result=new StringBuilder(64);foreach(byte b in bytes)result.Append(b.ToString("x2",CultureInfo.InvariantCulture));return result.ToString();
        }
        internal static string ReceiptToken(ExpansionResourceReceipt r)
        {return r==null?"-":string.Join(",",N(r.Sequence),N(r.Parts),N(r.Potatoes),N(r.Energy),N(r.BonusSymbol),N(r.BonusParts),N(r.BonusPotatoes),r.Boosted?"1":"0");}
        static ExpansionResourceReceipt Receipt(string token)
        {
            if(token=="-")return null;var f=token.Split(',');if(f.Length!=8)throw new FormatException("Invalid expansion resource receipt shape");
            return new ExpansionResourceReceipt(L(f[0]),I(f[1]),I(f[2]),I(f[3]),I(f[4]),I(f[5]),I(f[6]),B(f[7]));
        }
        static string CommandToken(ExpansionCommandReceipt r)
        {return r==null?"-":string.Join(",",N(r.Sequence),r.Key,r.Action,r.Id,N(r.Slot));}
        static ExpansionCommandReceipt Command(string token)
        {
            if(token=="-")return null;var f=token.Split(',');if(f.Length!=5)throw new FormatException("Invalid expansion command receipt shape");
            return new ExpansionCommandReceipt(L(f[0]),f[1],f[2],f[3],I(f[4]));
        }
        public static string EncodeState(ExpansionState s)
        {
            if(s==null)throw new ArgumentNullException(nameof(s));
            string modules=string.Join(",",Enumerable.Range(1,3).Select(slot=>s.ModuleAt(slot)??"-"));
            string equipment=string.Join(",",FamilyIds.All.Select(id=>N(s.EquipmentLevel(id))));
            string result=string.Join("|",s.FormatVersion==1?"expansion.state.v1":s.FormatVersion==2?"expansion.state.v2":"expansion.state.v3",N(s.Parts),N(s.Potatoes),N(s.Energy),N(s.ModuleMask),N(s.ThemeMask),N(s.SocketCount),N(s.PotatoYieldLevel),
                s.BonusUnlocked?"1":"0",s.BoostArmed?"1":"0",N(s.BonusProgress),N(s.BonusRolls),N(s.BonusSeed),N(s.ForegroundSpins),s.ThemeId,N(s.AdoptedSequence),modules,equipment,
                ReceiptToken(s.LastResourceReceipt),CommandToken(s.LastCommand),s.SpeedId);
            if(s.FormatVersion>=2)result+="|"+N(s.BonusWorkbenchLevel)+"|"+s.BonusFocusId;
            if(s.FormatVersion==3)result+="|"+ArcadeCodec.Encode(s.ArcadeData);
            if(result.Length>MaximumStateCharacters)throw new FormatException("Expansion state exceeds bound");return result;
        }
        public static ExpansionState DecodeState(string token)
        {
            if(token==null||token.Length==0||token.Length>MaximumStateCharacters)throw new FormatException("Invalid present expansion state length");
            var f=token.Split('|');bool v2=f.Length==23&&f[0]=="expansion.state.v2";
            bool v3=f.Length==24&&f[0]=="expansion.state.v3";
            if(!v2&&!v3&&(f.Length!=21||f[0]!="expansion.state.v1"))throw new FormatException("Unknown expansion state shape/version");
            try
            {
                var modules=f[16].Split(',');var equipment=f[17].Split(',');if(modules.Length!=3||equipment.Length!=6)throw new FormatException("Invalid expansion array shape");
                var d=new ExpansionDraft {Parts=I(f[1]),Potatoes=I(f[2]),Energy=I(f[3]),ModuleMask=I(f[4]),ThemeMask=I(f[5]),SocketCount=I(f[6]),PotatoYieldLevel=I(f[7]),
                    BonusUnlocked=B(f[8]),BoostArmed=B(f[9]),BonusProgress=I(f[10]),BonusRolls=L(f[11]),BonusSeed=U(f[12]),ForegroundSpins=L(f[13]),ThemeId=f[14],AdoptedSequence=L(f[15]),
                    Modules=modules.Select(m=>m=="-"?null:m).ToArray(),Equipment=equipment.Select(I).ToArray(),LastResourceReceipt=Receipt(f[18]),LastCommand=Command(f[19]),SpeedId=f[20],
                    FormatVersion=v3?3:v2?2:1,BonusWorkbenchLevel=v2||v3?I(f[21]):0,BonusFocusId=v2||v3?f[22]:ExpansionCatalog.FocusBalanced,ArcadeData=v3?ArcadeCodec.Decode(f[23]):null};
                var state=d.Freeze();if(EncodeState(state)!=token)throw new FormatException("Noncanonical expansion state");return state;
            }
            catch(ArgumentException e){throw new FormatException("Invalid expansion checkpoint",e);}
            catch(OverflowException e){throw new FormatException("Expansion integer out of range",e);}
        }
        static string QuoteToken(ExpansionQuote q)
        {return q==null?"-":string.Join("^",q.Action,q.Id,N(q.Slot),N(q.ExpectedSequence),N(q.PartsCost),N(q.PotatoesCost),N(q.EnergyCost),N(q.CoinsCost),N(q.CoinsReward),q.Fingerprint,q.Key);}
        static ExpansionQuote Quote(string token,string revision)
        {
            if(token=="-")return null;var f=token.Split('^');if(f.Length!=11||!IsKey(f[9])||!IsKey(f[10]))throw new FormatException("Invalid expansion quote shape");
            var q=new ExpansionQuote(f[0],f[1],I(f[2]),L(f[3]),I(f[4]),I(f[5]),I(f[6]),L(f[7]),L(f[8]),f[9],revision);
            if(q.Key!=f[10])throw new FormatException("Expansion quote identity mismatch");return q;
        }
        static string BatchToken(ExpansionBatchQuote q)
        {return string.Join("^",q.Id,N(q.BatchCount),N(q.ExpectedSequence),N(q.PartsCost),N(q.PotatoesCost),N(q.CoinsReward),q.Fingerprint,q.Key);}
        static ExpansionBatchQuote Batch(string token)
        {
            var f=token.Split('^');if(f.Length!=8||!IsKey(f[6])||!IsKey(f[7]))throw new FormatException("Invalid batch quote shape");
            var q=new ExpansionBatchQuote(f[0],I(f[1]),L(f[2]),I(f[3]),I(f[4]),L(f[5]),f[6]);
            if(q.Key!=f[7])throw new FormatException("Batch quote identity mismatch");return q;
        }
        internal static string OperationBody(ExpansionOperation op)
        {
            if(op.ParcelsQuote!=null)return string.Join("~","expansion.op.v5",ParcelsQuote.Revision,op.Kind,N(op.Sequence),N(op.At),op.BeforeWasAbsent?"1":"0",EncodeState(op.Before),EncodeState(op.After),N(op.SeedBefore),N(op.SeedAfter),N(op.CoinsBefore),N(op.CoinsAfter),ParcelsCodec.QuoteToken(op.ParcelsQuote));
            if(op.BatchQuote!=null)return string.Join("~","expansion.op.v4",ExpansionBatchQuote.Revision,op.Kind,N(op.Sequence),N(op.At),op.BeforeWasAbsent?"1":"0",EncodeState(op.Before),EncodeState(op.After),N(op.SeedBefore),N(op.SeedAfter),N(op.CoinsBefore),N(op.CoinsAfter),BatchToken(op.BatchQuote));
            string result=string.Join("~",op.RuleRevision==ExpansionCatalog.RuleRevision?"expansion.op.v1":op.RuleRevision==ExpansionCatalog.NextRuleRevision?"expansion.op.v2":"expansion.op.v3",op.RuleRevision,op.Kind,N(op.Sequence),N(op.At),op.BeforeWasAbsent?"1":"0",EncodeState(op.Before),EncodeState(op.After),N(op.SeedBefore),N(op.SeedAfter),N(op.CoinsBefore),N(op.CoinsAfter),QuoteToken(op.Quote));
            return op.RuleRevision==ExpansionCatalog.ArcadeRuleRevision?result+"~"+ArcadeCodec.QuoteToken(op.ArcadeQuote):result;
        }
        public static string EncodeOperation(ExpansionOperation op)
        {
            if(op==null)throw new ArgumentNullException(nameof(op));string result=OperationBody(op)+"~"+op.Key;
            if(result.Length>MaximumOperationCharacters)throw new FormatException("Expansion operation exceeds bound");return result;
        }
        public static ExpansionOperation DecodeOperation(string token)
        {
            if(token==null||token.Length==0||token.Length>MaximumOperationCharacters)throw new FormatException("Invalid present expansion operation length");
            var f=token.Split('~');bool v3=f.Length==15&&f[0]=="expansion.op.v3"&&f[1]==ExpansionCatalog.ArcadeRuleRevision;
            bool v4=f.Length==14&&f[0]=="expansion.op.v4"&&f[1]==ExpansionBatchQuote.Revision;
            bool v5=f.Length==14&&f[0]=="expansion.op.v5"&&f[1]==ParcelsQuote.Revision;
            if(!v5&&!v4&&!v3&&(f.Length!=14||!((f[0]=="expansion.op.v1"&&f[1]==ExpansionCatalog.RuleRevision)||
                (f[0]=="expansion.op.v2"&&f[1]==ExpansionCatalog.NextRuleRevision)))||!IsKey(f[f.Length-1]))throw new FormatException("Unknown expansion operation shape/revision");
            try
            {
                var op=new ExpansionOperation(f[2],L(f[3]),L(f[4]),B(f[5]),DecodeState(f[6]),DecodeState(f[7]),U(f[8]),U(f[9]),L(f[10]),L(f[11]),v5||v4?null:Quote(f[12],f[1]),v3?ArcadeCodec.Quote(f[13]):null,v4?Batch(f[12]):null,v5?ParcelsCodec.Quote(f[12]):null);
                if(op.Key!=f[f.Length-1]||EncodeOperation(op)!=token)throw new FormatException("Frozen expansion identity mismatch");return op;
            }
            catch(ArgumentException e){throw new FormatException("Invalid frozen expansion operation",e);}
            catch(OverflowException e){throw new FormatException("Expansion integer out of range",e);}
        }
    }
}
