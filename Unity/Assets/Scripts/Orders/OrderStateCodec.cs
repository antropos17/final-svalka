using System;
using System.Globalization;
using JunkToNeon.Contracts;

namespace JunkToNeon.Orders
{
    // Canonical bounded integration payload, never a file or independent save.
    public static class OrderStateCodec
    {
        public const int MaximumCharacters = 4096;
        public static string Encode(OrderState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            string result = string.Join("|", "orders-state.v1", N(state.InstanceSequence), state.ActiveOrderId ?? "-", N(state.Progress), N(state.Cursor),
                N(state.LastCommandSequence), state.LastOperationKey ?? "-", EventToken(state.LastEvent), ClaimToken(state.LatestClaim));
            if (result.Length > MaximumCharacters) throw new ArgumentException("Order payload exceeds bound.");
            return result;
        }
        // Null represents an ABSENT integration field only. The owner must reject a PRESENT
        // null field before calling this helper. Empty/whitespace/present malformed data rejects.
        public static OrderState DecodeOrDefault(string payload)
        {
            if (payload == null) return OrderState.Empty;
            if (payload.Length == 0 || payload.Length > MaximumCharacters) throw new FormatException("Invalid order payload length.");
            var fields = payload.Split('|');
            if (fields.Length != 9 || fields[0] != "orders-state.v1") throw new FormatException("Unknown order payload shape/version.");
            try
            {
                OrderEvent lastEvent = null; OrderClaimRecord claim = null;
                if (fields[7] != "-")
                {
                    var e = fields[7].Split(','); if (e.Length != 6) throw new FormatException("Invalid event shape.");
                    lastEvent = new OrderEvent(L(e[0]), L(e[1]), new SpinOutcome(I(e[2]), I(e[3]), I(e[4])), I(e[5]));
                }
                if (fields[8] != "-")
                {
                    var c = fields[8].Split(','); if (c.Length != 4) throw new FormatException("Invalid claim shape.");
                    claim = new OrderClaimRecord(c[0], L(c[1]), L(c[2]), L(c[3]));
                }
                var state = new OrderState(L(fields[1]), fields[2] == "-" ? null : fields[2], I(fields[3]), L(fields[4]), lastEvent,
                    L(fields[5]), fields[6] == "-" ? null : fields[6], claim);
                if (Encode(state) != payload) throw new FormatException("Noncanonical order payload.");
                return state;
            }
            catch (ArgumentException error) { throw new FormatException("Invalid order checkpoint.", error); }
        }
        internal static string N(long value) { return value.ToString(CultureInfo.InvariantCulture); }
        internal static string EventToken(OrderEvent e)
        { return e == null ? "-" : string.Join(",", N(e.OperationSequence), N(e.ExpectedCursor), N(e.Outcome.Left), N(e.Outcome.Middle), N(e.Outcome.Right), N(e.CompletedDeliveries)); }
        static string ClaimToken(OrderClaimRecord c)
        { return c == null ? "-" : string.Join(",", c.OrderId, N(c.InstanceSequence), N(c.ClaimSequence), N(c.RewardCoins)); }
        static long L(string value)
        {
            long parsed;
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) || N(parsed) != value)
                throw new FormatException("Expected canonical nonnegative integer.");
            return parsed;
        }
        static int I(string value)
        { long parsed = L(value); if (parsed > int.MaxValue) throw new FormatException("Integer outside range."); return (int)parsed; }
    }
}
