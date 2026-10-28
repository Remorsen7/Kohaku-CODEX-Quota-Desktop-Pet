using System;
using System.Linq;
using System.Text.Json;
namespace CodexQuotaPet;

public record WindowQuota(decimal Remaining, DateTimeOffset? Reset);
public record PetQuota(WindowQuota? FiveHour, WindowQuota? Weekly, DateTimeOffset? Updated, string Pool);
public static class QuotaView
{
    public static PetQuota Parse(JsonElement json, DateTimeOffset now)
    {
        JsonElement snapshot=default; string pool="unknown";
        if(json.TryGetProperty("rateLimitsByLimitId",out var pools) && pools.ValueKind==JsonValueKind.Object && pools.EnumerateObject().Any())
        {
            if(pools.TryGetProperty("codex",out var codex)){snapshot=codex;pool="codex";}
            else if(pools.EnumerateObject().Count()==1){var only=pools.EnumerateObject().First();snapshot=only.Value;pool=only.Name;}
        }
        else if(json.TryGetProperty("rateLimits",out var legacy)){snapshot=legacy;pool="codex";}
        WindowQuota? Read(int duration)
        {
            if(snapshot.ValueKind!=JsonValueKind.Object)return null;
            foreach(var key in new[]{"primary","secondary"})
            {
                if(!snapshot.TryGetProperty(key,out var w) || w.ValueKind!=JsonValueKind.Object)continue;
                if(!w.TryGetProperty("windowDurationMins",out var d)||d.ValueKind!=JsonValueKind.Number||!d.TryGetInt32(out var mins)||mins!=duration)continue;
                if(!w.TryGetProperty("usedPercent",out var u)||u.ValueKind!=JsonValueKind.Number||!u.TryGetDecimal(out var used))continue;
                DateTimeOffset? reset=null;
                if(w.TryGetProperty("resetsAt",out var r)&&r.ValueKind==JsonValueKind.Number&&r.TryGetInt64(out var epoch))
                    try{reset=epoch>10_000_000_000?DateTimeOffset.FromUnixTimeMilliseconds(epoch):DateTimeOffset.FromUnixTimeSeconds(epoch);}catch(ArgumentOutOfRangeException){}
                return new(decimal.Clamp(100-used,0,100),reset);
            }
            return null;
        }
        return new(Read(300),Read(10080),now,pool);
    }
    public static bool IsStale(PetQuota quota, DateTimeOffset now) => quota.Updated is null || now-quota.Updated>TimeSpan.FromMinutes(2)
        || quota.FiveHour?.Reset is { } five && five<=now || quota.Weekly?.Reset is { } week && week<=now;
    public static string Beijing(DateTimeOffset? time) => time?.ToOffset(TimeSpan.FromHours(8)).ToString("MM月dd日 HH:mm")??"未知";
    public static string Countdown(DateTimeOffset? time, DateTimeOffset now)
    {
        if(time is null)return "未知";
        var left=time.Value-now;
        if(left<=TimeSpan.Zero)return "等待更新";
        if(left.TotalDays>=1)return $"{(int)left.TotalDays}天{left.Hours:00}小时";
        if(left.TotalHours>=1)return $"{(int)left.TotalHours}小时{left.Minutes:00}分";
        return $"{Math.Max(1,(int)Math.Ceiling(left.TotalMinutes))}分钟";
    }
}
