using System.Diagnostics;
using System.Runtime.InteropServices;
using CodexQuotaPet;

var ready=new TaskCompletionSource<PetQuota>(TaskCreationOptions.RunContinuationsAsynchronously);
var service=new QuotaService();
service.Changed+=(q,error)=>{if(q!=null)ready.TrySetResult(q);};
try
{
    var quota=await ready.Task.WaitAsync(TimeSpan.FromSeconds(35));
    if(quota.FiveHour==null||quota.Weekly==null)throw new Exception("Expected both real quota windows");
    var children=Native.Children((uint)Environment.ProcessId).ToArray();
    if(children.Length==0)throw new Exception("Expected dedicated app-server child");
    var timer=Stopwatch.StartNew();service.Dispose();
    foreach(var pid in children)
    {
        try{using var p=Process.GetProcessById((int)pid);if(!p.HasExited)throw new Exception("Child left running: "+pid);}
        catch(ArgumentException){}
    }
    Console.WriteLine($"PASS live quota: 5h={quota.FiveHour.Remaining}% week={quota.Weekly.Remaining}%");
    Console.WriteLine($"PASS disposal: {children.Length} dedicated child exited in {timer.ElapsedMilliseconds}ms");
}
catch{service.Dispose();throw;}

static class Native
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Entry
    {
        public uint Size,Usage,Pid;public nuint Heap;public uint Module,Threads,Parent;public int Priority;public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Exe;
    }
    [DllImport("kernel32.dll")]static extern nint CreateToolhelp32Snapshot(uint flags,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool Process32First(nint snapshot,ref Entry entry);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool Process32Next(nint snapshot,ref Entry entry);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(nint handle);
    public static List<uint> Children(uint parent)
    {
        var list=new List<uint>();var snap=CreateToolhelp32Snapshot(2,0);if(snap==-1)throw new Exception("Process snapshot failed");
        try{var e=new Entry{Size=(uint)Marshal.SizeOf<Entry>()};if(Process32First(snap,ref e))do{if(e.Parent==parent)list.Add(e.Pid);}while(Process32Next(snap,ref e));}
        finally{CloseHandle(snap);}return list;
    }
}
