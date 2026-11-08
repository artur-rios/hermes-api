using ArturRios.Hermes.WebApi.Clients;

namespace ArturRios.Hermes.WebApi.Limits;

/// <summary>
/// Each client's usage, in memory and from zero after a restart. Windows are fixed: the clock's minute and the UTC
/// day. A message is counted before it is delivered, so a failed delivery still counts.
/// </summary>
public sealed class UsageCounters(TimeProvider clock)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Usage> _usage = new(StringComparer.Ordinal);

    /// <summary>Counts the message when the client is within both limits, and otherwise counts nothing.</summary>
    public LimitDecision TryCount(Client client)
    {
        lock (_lock)
        {
            // Read under the lock, so a request that waited for it cannot move a window backwards.
            var now = clock.GetUtcNow().UtcDateTime;
            var minute = new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerMinute), DateTimeKind.Utc);
            var day = now.Date;

            var usage = _usage.TryGetValue(client.Id, out var existing) ? existing : new Usage();

            usage.StartWindows(minute, day);

            _usage[client.Id] = usage;

            // The day first: when both are used up, retrying at the end of the minute would only be refused again.
            if (usage.UsedToday >= client.LimitPerDay)
            {
                return LimitDecision.Refuse(day.AddDays(1) - now);
            }

            if (usage.UsedThisMinute >= client.LimitPerMinute)
            {
                return LimitDecision.Refuse(minute.AddMinutes(1) - now);
            }

            usage.UsedThisMinute++;
            usage.UsedToday++;

            if (usage.AlertedToday || usage.UsedToday < client.LimitPerDay)
            {
                return LimitDecision.Allow;
            }

            usage.AlertedToday = true;

            return LimitDecision.AllowAndAlertOwner;
        }
    }

    private sealed class Usage
    {
        private DateTime _minute;
        private DateTime _day;

        public int UsedThisMinute { get; set; }

        public int UsedToday { get; set; }

        public bool AlertedToday { get; set; }

        public void StartWindows(DateTime minute, DateTime day)
        {
            if (_minute != minute)
            {
                _minute = minute;
                UsedThisMinute = 0;
            }

            if (_day != day)
            {
                _day = day;
                UsedToday = 0;
                AlertedToday = false;
            }
        }
    }
}
