using System;
using System.Globalization;
using System.Text;

namespace JunkToNeon.Core
{
    // Internal raw state includes unrevealed challenge data. Runtime uses masked ArcadeView.
    internal static class ArcadeCodec
    {
        internal const int MaximumCharacters=3072;
        static string N(long value){return ExpansionCodec.N(value);}
        static long L(string value){if(!long.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out long n)||N(n)!=value)throw new FormatException("Noncanonical Arcade integer");return n;}
        static int I(string value){return checked((int)L(value));}
        static uint U(string value){return checked((uint)L(value));}
        internal static string QuoteBody(ArcadeQuote q)
        {return string.Join(",","arcade.quote.v1",q.RuleRevision,N(q.ExpectedSequence),q.Action,q.Id,N(q.ExpectedInstance),N(q.ExpectedCursor),q.ExpectedPhase,N(q.Target),N(q.PartsCost),N(q.PotatoesCost),N(q.DiamondsCost),N(q.TicketsCost),N(q.PartsCredit),N(q.PotatoesCredit),N(q.DiamondsCredit),q.Fingerprint);}
        internal static string QuoteToken(ArcadeQuote q){return q==null?"-":QuoteBody(q)+","+q.Key;}
        internal static ArcadeQuote Quote(string token)
        {
            if(token=="-")return null;var f=token.Split(',');if(f.Length!=18||f[0]!="arcade.quote.v1"||f[1]!=ExpansionCatalog.ArcadeRuleRevision||!ExpansionCodec.IsKey(f[17]))throw new FormatException("Unknown Arcade quote shape/version");
            var q=new ArcadeQuote(L(f[2]),f[3],f[4],L(f[5]),I(f[6]),f[7],I(f[8]),I(f[9]),I(f[10]),L(f[11]),I(f[12]),I(f[13]),I(f[14]),I(f[15]),f[16]);
            if(q.Key!=f[17]||QuoteToken(q)!=token)throw new FormatException("Arcade quote identity mismatch");return q;
        }
        static string EventToken(ArcadeEvent r)
        {return r==null?"-":string.Join(",",N(r.Sequence),r.Kind,N(r.InstanceSequence),N(r.PartsAdded),N(r.PotatoesAdded),N(r.EnergyAdded),N(r.TicketsAdded),N(r.DiamondsAdded),N(r.CoinIncome),N(r.ConstructionDebit),N(r.SlotPartsIncluded),N(r.SlotPotatoesIncluded),N(r.CoinsBefore),N(r.CoinsAfter));}
        static ArcadeEvent Event(string token)
        {
            if(token=="-")return null;var f=token.Split(',');if(f.Length!=14)throw new FormatException("Invalid Arcade event shape");
            return new ArcadeEvent(L(f[0]),f[1],L(f[2]),I(f[3]),I(f[4]),I(f[5]),I(f[6]),I(f[7]),L(f[8]),L(f[9]),I(f[10]),I(f[11]),L(f[12]),L(f[13]));
        }
        internal static bool SameEvent(ArcadeEvent a,ArcadeEvent b){return EventToken(a)==EventToken(b);}
        internal static bool Same(ArcadeState a,ArcadeState b){return Encode(a)==Encode(b);}
        internal static string Encode(ArcadeState state)
        {
            if(state==null)throw new ArgumentNullException(nameof(state));var r=state.Run;
            string run=r==null?"-":string.Join("/",ArcadeCatalog.RuleRevision,r.Id,N(r.Instance),N(r.SeedBefore),N(r.SeedAfter),r.BoardToken,r.Transcript);
            string raw=string.Join(";",state.Parcels==null?"arcade.state.v1":"arcade.state.v2",N(state.AdoptedSequence),N(state.ForegroundSpins),N(state.Diamonds),N(state.Tickets),N(state.TicketProgress),N(state.SlotLevel),N(state.ChallengeSeed),run,QuoteToken(state.LastCommand),QuoteToken(state.LastClaim),EventToken(state.LastResourceReceipt),EventToken(state.LastEvent));
            if(state.Parcels!=null)raw+=";"+N(state.Parcels.NextBoardIndex)+";"+ParcelsCodec.RunToken(state.Parcels.Run)+";"+ParcelsCodec.QuoteToken(state.Parcels.LastCommand)+";"+ParcelsCodec.QuoteToken(state.Parcels.LastClaim);
            string result=Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));if(result.Length>MaximumCharacters)throw new FormatException("Arcade state exceeds bound");return result;
        }
        internal static ArcadeState Decode(string token)
        {
            if(token==null||token.Length==0||token.Length>MaximumCharacters)throw new FormatException("Invalid present Arcade payload length");
            try
            {
                string raw=new UTF8Encoding(false,true).GetString(Convert.FromBase64String(token));var f=raw.Split(';');bool v2=f.Length==17&&f[0]=="arcade.state.v2";if(!v2&&(f.Length!=13||f[0]!="arcade.state.v1"))throw new FormatException("Unknown Arcade state shape/version");
                ArcadeRun run=null;if(f[8]!="-"){var r=f[8].Split('/');if(r.Length!=7||r[0]!=ArcadeCatalog.RuleRevision)throw new FormatException("Unknown frozen challenge shape/rule");run=new ArcadeRun(r[1],L(r[2]),U(r[3]),U(r[4]),r[5],r[6]);}
                var d=new ArcadeDraft{AdoptedSequence=L(f[1]),ForegroundSpins=L(f[2]),Diamonds=L(f[3]),Tickets=I(f[4]),TicketProgress=I(f[5]),SlotLevel=I(f[6]),ChallengeSeed=U(f[7]),Run=run,LastCommand=Quote(f[9]),LastClaim=Quote(f[10]),LastResourceReceipt=Event(f[11]),LastEvent=Event(f[12]),Parcels=v2?new ParcelsState(I(f[13]),ParcelsCodec.Run(f[14]),ParcelsCodec.Quote(f[15]),ParcelsCodec.Quote(f[16])):null};
                var result=d.Freeze();if(Encode(result)!=token)throw new FormatException("Noncanonical Arcade payload");return result;
            }
            catch(ArgumentException e){throw new FormatException("Invalid Arcade checkpoint",e);}catch(OverflowException e){throw new FormatException("Arcade numeric range",e);}
        }
    }
}
