using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CodexQuotaPet;

public static class HostWindow
{
    [StructLayout(LayoutKind.Sequential)] public struct Rect{public int Left,Top,Right,Bottom;}
    delegate bool Callback(nint h,nint p);
    [DllImport("user32.dll")] static extern bool EnumWindows(Callback cb,nint p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint h);
    [DllImport("user32.dll")] public static extern bool IsIconic(nint h);
    [DllImport("user32.dll")] public static extern bool IsWindow(nint h);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint h,out Rect r);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint h,out uint p);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(nint h,StringBuilder text,int max);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(nint h,StringBuilder text,int max);
    public static uint Pid(nint h){GetWindowThreadProcessId(h,out var p);return p;}
    public static nint Find(nint previous)
    {
        var foreground=GetForegroundWindow();
        if(IsCandidate(foreground))return foreground;
        if(previous!=0&&IsWindow(previous)&&IsCandidate(previous))return previous;
        var candidates=new List<nint>();
        EnumWindows((h,_)=>{if(IsCandidate(h))candidates.Add(h);return true;},0);
        return candidates.Count==1?candidates[0]:0;
    }
    static bool IsCandidate(nint h)
    {
        if(h==0||!IsWindowVisible(h))return false;
        var cls=new StringBuilder(128);GetClassName(h,cls,128);
        if(!cls.ToString().StartsWith("Chrome_WidgetWin_",StringComparison.Ordinal))return false;
        try
        {
            using var p=Process.GetProcessById((int)Pid(h));
            if(!(p.ProcessName.Equals("ChatGPT",StringComparison.OrdinalIgnoreCase)||p.ProcessName.Equals("Codex",StringComparison.OrdinalIgnoreCase)))return false;
            if(!GetWindowRect(h,out var r)||r.Right-r.Left<500||r.Bottom-r.Top<350)return false;
            var title=new StringBuilder(512);GetWindowText(h,title,512);
            return title.Length>0;
        }
        catch{return false;}
    }
}
