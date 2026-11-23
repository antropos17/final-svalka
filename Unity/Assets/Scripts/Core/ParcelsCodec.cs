using System;
using System.Globalization;
using System.Linq;

namespace JunkToNeon.Core
{
    internal static class ParcelsCodec
    {
        static string N(long n){return ExpansionCodec.N(n);}
        static long L(string s){if(!long.TryParse(s,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out long n)||N(n)!=s)throw new FormatException("Noncanonical Parcels integer");return n;}
        static int I(string s){return checked((int)L(s));}
        internal static string QuoteBody(ParcelsQuote q)
        {return string.Join(",",ParcelsQuote.Revision,q.Action,N(q.ExpectedSequence),N(q.ExpectedInstance),N(q.TokenId),N(q.TargetSlot),q.BoardId,q.ResultSlots,N(q.DiamondsCredit),q.Fingerprint);}
        internal static string QuoteToken(ParcelsQuote q){return q==null?"-":QuoteBody(q)+","+q.Key;}
        internal static ParcelsQuote Quote(string token)
        {
            if(token=="-")return null;var f=token.Split(',');if(f.Length!=11||f[0]!=ParcelsQuote.Revision||!ExpansionCodec.IsKey(f[10]))throw new FormatException("Invalid Parcels quote shape");
            var q=new ParcelsQuote(f[1],L(f[2]),L(f[3]),I(f[4]),I(f[5]),f[6],f[7],I(f[8]),f[9]);if(QuoteToken(q)!=token)throw new FormatException("Parcels quote identity mismatch");return q;
        }
        internal static string RunToken(ParcelsRun r)
        {return r==null?"-":string.Join("/",ParcelsCatalog.Revision,r.BoardId,N(r.Instance),string.Concat(ParcelsCatalog.TokenSymbols(r.BoardId).Select(NInt)),ParcelsCatalog.SlotsToken(r.Slots),r.Ready?"ready":"active",N(r.ReadyReward));}
        static string NInt(int n){return N(n);}
        internal static ParcelsRun Run(string token)
        {
            if(token=="-")return null;var f=token.Split('/');if(f.Length!=7||f[0]!=ParcelsCatalog.Revision||(f[5]!="ready"&&f[5]!="active"))throw new FormatException("Invalid Parcels run shape");
            var r=new ParcelsRun(L(f[2]),f[1],ParcelsCatalog.ReadSlots(f[4]),f[5]=="ready",I(f[6]));if(RunToken(r)!=token)throw new FormatException("Frozen Parcels board mismatch");return r;
        }
    }
}
