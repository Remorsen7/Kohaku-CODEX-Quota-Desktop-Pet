using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodexQuotaTitle.AppServer;

namespace CodexQuotaPet;

public sealed class QuotaService : IDisposable
{
    readonly CancellationTokenSource stop=new();
    readonly SemaphoreSlim refresh=new(0,1);
    readonly Task worker;
    public event Action<PetQuota?,string?>? Changed;
    public QuotaService()=>worker=Task.Run(RunAsync);
    public void Refresh(){if(refresh.CurrentCount==0)try{refresh.Release();}catch(SemaphoreFullException){}}
    public static string ResolveCodex()
    {
        foreach(var key in new[]{"CODEX_CLI_PATH","CODEXQUOTATITLE_CODEX_EXECUTABLE"})
            if(Environment.GetEnvironmentVariable(key) is { } p && File.Exists(p))return p;
        var bin=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin");
        if(Directory.Exists(bin))
        {
            var file=Directory.EnumerateFiles(bin,"codex.exe",SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if(file!=null)return file;
        }
        return "codex";
    }
    async Task RunAsync()
    {
        while(!stop.IsCancellationRequested)
        {
            try
            {
                using var startTimeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                startTimeout.CancelAfter(TimeSpan.FromSeconds(25));
                await using var client=await CodexAppServerClient.StartAsync(new CodexAppServerOptions(
                    Executable:ResolveCodex(),ClientName:"codex_quota_pet",ClientTitle:"Codex 额度桌宠",ClientVersion:"1.0.0",
                    CodexHome:Environment.GetEnvironmentVariable("CODEX_HOME")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex")),startTimeout.Token);
                while(!stop.IsCancellationRequested)
                {
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    var result=await client.ReadRateLimitsAsync(timeout.Token);
                    // Use the upstream parser only; never read session history or credentials.
                    var json=System.Text.Json.JsonSerializer.SerializeToElement(new{
                        rateLimits=result.RateLimits is null?null:Map(result.RateLimits),
                        rateLimitsByLimitId=result.RateLimitsByLimitId.ToDictionary(x=>x.Key,x=>Map(x.Value))});
                    Changed?.Invoke(QuotaView.Parse(json,DateTimeOffset.UtcNow),null);
                    await refresh.WaitAsync(TimeSpan.FromSeconds(60),stop.Token);
                }
            }
            catch(OperationCanceledException) when(stop.IsCancellationRequested){break;}
            catch(Exception){if(stop.IsCancellationRequested)break;Changed?.Invoke(null,"暂时无法读取额度，正在重试");}
            try{await refresh.WaitAsync(TimeSpan.FromSeconds(30),stop.Token);}catch(OperationCanceledException){break;}
        }
    }
    static object Map(CodexQuotaTitle.Quotas.QuotaSnapshot s)
    {
        object? W(CodexQuotaTitle.Quotas.RateLimitWindow? w)=>w==null?null:new{usedPercent=w.UsedPercent,windowDurationMins=w.WindowDurationMins,resetsAt=w.ResetsAt?.ToUnixTimeSeconds()};
        return new{limitId=s.LimitId,primary=W(s.Primary),secondary=W(s.Secondary)};
    }
    public void Dispose()
    {
        stop.Cancel();
        // The worker has no UI synchronization context, so waiting here cannot
        // deadlock the dispatcher. Its await-using closes/kills only our child.
        worker.GetAwaiter().GetResult();
        stop.Dispose();refresh.Dispose();
    }
}
