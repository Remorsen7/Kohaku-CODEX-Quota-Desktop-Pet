using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CodexQuotaPet;

public sealed class PetApp : Application
{
    static Mutex? singleton;
    QuotaService? service;
    System.Windows.Forms.NotifyIcon? tray;
    [STAThread] public static void Main(string[] args)
    {
        singleton=new Mutex(true,"Local\\CodexQuotaPet-v1",out bool first);
        if(!first)return;
        var app=new PetApp{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.DispatcherUnhandledException+=(_,e)=>{try{File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"error.log"),e.Exception.GetType().Name+": "+e.Exception.Message+Environment.NewLine);}catch{} e.Handled=true;};
        app.Startup+=(_,_)=>app.Start(args);
        app.Exit+=(_,_)=>{app.tray?.Dispose();app.service?.Dispose();singleton?.Dispose();};
        app.Run();
    }
    void Start(string[] args)
    {
        var pet=new PetWindow(); MainWindow=pet; pet.Show();
        service=new QuotaService();
        service.Changed+=(q,error)=>Dispatcher.InvokeAsync(()=>pet.Receive(q,error));
        pet.RefreshRequested+=()=>service.Refresh();
        tray=new System.Windows.Forms.NotifyIcon{Text="Codex 额度桌宠 · 点击查看",Icon=System.Drawing.SystemIcons.Information,Visible=true};
        var menu=new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("查看额度",null,(_,_)=>Dispatcher.Invoke(()=>pet.OpenCard(true)));
        menu.Items.Add("立即刷新",null,(_,_)=>service.Refresh());
        menu.Items.Add("恢复默认位置",null,(_,_)=>pet.ResetPosition());
        menu.Items.Add("退出桌宠",null,(_,_)=>Shutdown());
        tray.ContextMenuStrip=menu;
        tray.MouseClick+=(_,e)=>{if(e.Button==System.Windows.Forms.MouseButtons.Left)Dispatcher.Invoke(()=>pet.OpenCard(true));};
        if(args.Contains("--inspect"))pet.EnableInspection();
    }
}

public sealed class PetWindow : Window
{
    readonly CardWindow card=new();
    readonly DispatcherTimer follow=new(){Interval=TimeSpan.FromMilliseconds(250)};
    readonly TextBlock chip;
    PetQuota? quota;
    string? error;
    bool expanded,dragging,detached;
    Point? start;
    nint host;
    double rightOffset=18,bottomOffset=118;
    double petScale=1;
    readonly string settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexQuotaPet","position.json");
    public event Action? RefreshRequested;
    public PetWindow()
    {
        Title="Codex 额度桌宠";Width=142;Height=214;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
        ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;
        AutomationProperties.SetName(this,"Codex 额度桌宠");
        var root=new Grid{Margin=new Thickness(5)};
        root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(178)});
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var image=new Image{Source=new BitmapImage(new Uri("pack://application:,,,/assets/character.png")),Stretch=Stretch.Uniform,Cursor=Cursors.Hand};
        AutomationProperties.SetName(image,"点击角色查看额度，拖动调整位置");
        image.ToolTip="点击查看 5 小时 / 1 周额度\n拖动调整位置 · 右键更多操作";
        var movement=new TranslateTransform();image.RenderTransform=movement;
        if(SystemParameters.ClientAreaAnimation)movement.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(-2,2,TimeSpan.FromSeconds(2.3)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever});
        root.Children.Add(image);
        chip=new TextBlock{Text="点击查看额度",Foreground=B("#6F3453"),FontSize=11,HorizontalAlignment=HorizontalAlignment.Center};
        var pill=new Border{Background=B("#F7F0F5"),BorderBrush=B("#E9D6E1"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(13),Padding=new Thickness(10,4,10,4),HorizontalAlignment=HorizontalAlignment.Center,Child=chip,Cursor=Cursors.Hand};
        Grid.SetRow(pill,1);root.Children.Add(pill);
        var canvas=new Grid{Width=142,Height=214};canvas.Children.Add(root);
        Content=new Viewbox{Child=canvas,Stretch=Stretch.Uniform};
        var menu=new ContextMenu();
        MenuItem Item(string label,Action action){var item=new MenuItem{Header=label};item.Click+=(_,_)=>action();return item;}
        menu.Items.Add(Item("刷新额度",()=>RefreshRequested?.Invoke()));
        menu.Items.Add(Item("放大（+10%）",()=>SetPetScale(petScale+.1)));
        menu.Items.Add(Item("缩小（−10%）",()=>SetPetScale(petScale-.1)));
        menu.Items.Add(Item("恢复默认大小（100%）",()=>SetPetScale(1)));
        menu.Items.Add(Item("恢复默认位置",ResetPosition));
        menu.Items.Add(Item("退出桌宠",()=>Application.Current.Shutdown()));root.ContextMenu=menu;
        root.MouseLeftButtonDown+=(_,e)=>{start=PointToScreen(e.GetPosition(this));dragging=false;root.CaptureMouse();e.Handled=true;};
        root.MouseMove+=(_,e)=>
        {
            if(start==null||e.LeftButton!=MouseButtonState.Pressed)return;
            var now=PointToScreen(e.GetPosition(this));
            if((now-start.Value).Length<5)return;
            dragging=true;var m=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice??Matrix.Identity;
            var delta=m.Transform(now)-m.Transform(start.Value);Left+=delta.X;Top+=delta.Y;start=now;
            expanded=false;card.Hide();
        };
        root.MouseLeftButtonUp+=(_,e)=>
        {
            root.ReleaseMouseCapture();start=null;
            if(dragging){SavePosition();dragging=false;}else{if(expanded){expanded=false;card.Hide();}else OpenCard();}
            e.Handled=true;
        };
        card.RefreshRequested+=()=>RefreshRequested?.Invoke();card.CloseRequested+=()=>{expanded=false;card.Hide();};
        try{if(File.Exists(settingsPath)){using var p=JsonDocument.Parse(File.ReadAllText(settingsPath));rightOffset=Math.Clamp(p.RootElement.GetProperty("right").GetDouble(),0,5000);bottomOffset=Math.Clamp(p.RootElement.GetProperty("bottom").GetDouble(),0,5000);if(p.RootElement.TryGetProperty("scale",out var s)&&s.TryGetDouble(out var v)&&double.IsFinite(v))petScale=Math.Clamp(v,.5,2);}}catch{}
        Width=142*petScale;Height=214*petScale;
        Loaded+=(_,_)=>{card.Owner=this;Follow();follow.Tick+=(_,_)=>{Follow();card.Render(quota,error);};follow.Start();};
    }
    public static SolidColorBrush B(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
    public void EnableInspection(){ShowInTaskbar=true;card.ShowInTaskbar=true;}
    public void Receive(PetQuota? q,string? message)
    {
        if(q!=null)quota=q;error=message;card.Render(quota,error);
        chip.Text=q==null&&message!=null?"暂时离线 · 点我":"点击查看额度";
    }
    public void SetPetScale(double value)
    {
        if(!double.IsFinite(value))return;
        petScale=Math.Clamp(Math.Round(value,2),.5,2);
        Width=142*petScale;Height=214*petScale;
        SaveSettings();Follow();if(expanded)PositionCard();
    }
    public void ResetPosition(){rightOffset=18;bottomOffset=118;SaveSettings();Follow();}
    void SaveSettings()
    {
        try{Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);File.WriteAllText(settingsPath,JsonSerializer.Serialize(new{right=rightOffset,bottom=bottomOffset,scale=petScale}));}catch{}
    }
    void SavePosition()
    {
        if(host==0||!HostWindow.GetWindowRect(host,out var r))return;
        var m=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice??Matrix.Identity;
        var end=m.Transform(new Point(r.Right,r.Bottom));rightOffset=Math.Max(0,end.X-Left-Width);bottomOffset=Math.Max(0,end.Y-Top-Height);
        SaveSettings();
    }
    public void OpenCard(bool force=false)
    {
        detached=force;expanded=true;
        if(!IsVisible){Left=Math.Max(0,SystemParameters.WorkArea.Right-Width-30);Top=Math.Max(0,SystemParameters.WorkArea.Bottom-Height-70);Show();}
        PositionCard();card.Render(quota,error);card.Show();card.Activate();RefreshRequested?.Invoke();
    }
    void Follow()
    {
        if(dragging)return;
        host=HostWindow.Find(host);
        var fg=HostWindow.GetForegroundWindow();
        bool ours=HostWindow.Pid(fg)==(uint)Environment.ProcessId;
        bool visible=host!=0&&!HostWindow.IsIconic(host)&&(fg==host||ours);
        if(detached&&ours){if(expanded)PositionCard();return;}
        detached=false;
        if(!visible){if(IsVisible)Hide();card.Hide();expanded=false;return;}
        if(!HostWindow.GetWindowRect(host,out var rect))return;
        var matrix=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice??Matrix.Identity;
        var leftTop=matrix.Transform(new Point(rect.Left,rect.Top));var rightBottom=matrix.Transform(new Point(rect.Right,rect.Bottom));
        var screen=System.Windows.Forms.Screen.FromHandle(host).WorkingArea;
        var screenStart=matrix.Transform(new Point(screen.Left,screen.Top));var screenEnd=matrix.Transform(new Point(screen.Right,screen.Bottom));
        Left=Math.Clamp(rightBottom.X-Width-rightOffset,Math.Max(leftTop.X,screenStart.X),Math.Max(Math.Max(leftTop.X,screenStart.X),Math.Min(rightBottom.X,screenEnd.X)-Width));
        Top=Math.Clamp(rightBottom.Y-Height-bottomOffset,Math.Max(leftTop.Y+35,screenStart.Y),Math.Max(Math.Max(leftTop.Y+35,screenStart.Y),Math.Min(rightBottom.Y,screenEnd.Y)-Height));
        if(!IsVisible)Show();
        if(expanded){PositionCard();if(!card.IsVisible)card.Show();}
    }
    void PositionCard()
    {
        var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)PointToScreen(new Point(0,0)).X,(int)PointToScreen(new Point(0,0)).Y)).WorkingArea;
        var m=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice??Matrix.Identity;
        var lo=m.Transform(new Point(screen.Left,screen.Top));var hi=m.Transform(new Point(screen.Right,screen.Bottom));
        card.Left=Math.Clamp(Left-card.Width-8,lo.X+8,Math.Max(lo.X+8,hi.X-card.Width-8));
        card.Top=Math.Clamp(Top+Height-card.Height,lo.Y+8,Math.Max(lo.Y+8,hi.Y-card.Height-8));
    }
}

public sealed class CardWindow : Window
{
    readonly TextBlock status,updated;
    readonly QuotaRow five=new("5 小时剩余用量","#6571C5");
    readonly QuotaRow week=new("1 周剩余用量","#AD5B7C");
    public event Action? RefreshRequested;
    public event Action? CloseRequested;
    public CardWindow()
    {
        Title="Codex 额度详情";Width=336;Height=386;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
        ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;
        var border=new Border{Background=PetWindow.B("#FFF9FC"),BorderBrush=PetWindow.B("#E7DCE4"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(22),Padding=new Thickness(23)};
        var panel=new StackPanel();border.Child=panel;Content=border;
        var head=new DockPanel();
        var close=new Button{Content="×",Width=25,Height=25,FontSize=18,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=PetWindow.B("#826B7A"),Cursor=Cursors.Hand,ToolTip="收起"};
        close.Click+=(_,_)=>CloseRequested?.Invoke();DockPanel.SetDock(close,Dock.Right);head.Children.Add(close);
        head.Children.Add(new TextBlock{Text="额度小管家",FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=PetWindow.B("#302D46")});panel.Children.Add(head);
        status=new TextBlock{Text="正在连接本机 Codex…",FontSize=11,Foreground=PetWindow.B("#84788B"),Margin=new Thickness(0,6,0,19),TextWrapping=TextWrapping.Wrap};panel.Children.Add(status);
        panel.Children.Add(five.Element);panel.Children.Add(new Border{Height=1,Background=PetWindow.B("#EDE5EC"),Margin=new Thickness(0,17,0,17)});panel.Children.Add(week.Element);
        var bottom=new DockPanel{Margin=new Thickness(0,20,0,0)};
        var refresh=new Button{Content="刷新 ↻",Padding=new Thickness(10,5,10,5),Background=PetWindow.B("#EEEAF8"),Foreground=PetWindow.B("#59548A"),BorderThickness=new Thickness(0),Cursor=Cursors.Hand};
        refresh.Click+=(_,_)=>{status.Text="正在刷新…";RefreshRequested?.Invoke();};DockPanel.SetDock(refresh,Dock.Right);bottom.Children.Add(refresh);
        updated=new TextBlock{Text="北京时间 · 本机读取",FontSize=10,Foreground=PetWindow.B("#998898"),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap};bottom.Children.Add(updated);panel.Children.Add(bottom);
    }
    public void Render(PetQuota? quota,string? error)
    {
        var now=DateTimeOffset.UtcNow;bool stale=quota==null||QuotaView.IsStale(quota,now);bool failed=error!=null;
        five.Render(quota?.FiveHour,now,stale||failed);week.Render(quota?.Weekly,now,stale||failed);
        status.Text=error??(quota==null?"正在连接本机 Codex…":stale?"数据已过期 · 等待刷新":quota.FiveHour==null&&quota.Weekly==null?"此账号暂未返回可识别额度":"● 已连接 · "+(quota.Pool=="codex"?"Codex 额度池":"额度池："+quota.Pool));
        status.Foreground=PetWindow.B(stale||failed?"#A56C45":"#617B72");
        updated.Text=quota?.Updated is {} t?$"更新 {t.ToOffset(TimeSpan.FromHours(8)):HH:mm:ss}\n北京时间 · 本机读取":"北京时间 · 本机读取";
    }
    sealed class QuotaRow
    {
        public StackPanel Element{get;}=new();
        readonly TextBlock value,reset,count;
        readonly ProgressBar bar;
        public QuotaRow(string label,string color)
        {
            var top=new DockPanel();
            value=new TextBlock{Text="—",FontSize=25,FontWeight=FontWeights.SemiBold,Foreground=PetWindow.B(color)};DockPanel.SetDock(value,Dock.Right);top.Children.Add(value);
            top.Children.Add(new TextBlock{Text=label,FontSize=13,Foreground=PetWindow.B("#4C435C"),VerticalAlignment=VerticalAlignment.Center});Element.Children.Add(top);
            bar=new ProgressBar{Minimum=0,Maximum=100,Height=7,Foreground=PetWindow.B(color),Background=PetWindow.B("#EAE4ED"),BorderThickness=new Thickness(0),Margin=new Thickness(0,7,0,10)};Element.Children.Add(bar);
            reset=new TextBlock{Text="重置时间：未知",FontSize=11,Foreground=PetWindow.B("#8F7D91")};Element.Children.Add(reset);
            count=new TextBlock{Text="等待额度数据",FontSize=11,Foreground=PetWindow.B("#8F7D91"),Margin=new Thickness(0,3,0,0)};Element.Children.Add(count);
        }
        public void Render(WindowQuota? data,DateTimeOffset now,bool stale)
        {
            value.Text=data==null||stale?"—":$"{data.Remaining:0.#}%";bar.Value=data==null||stale?0:(double)data.Remaining;
            reset.Text="重置时间："+QuotaView.Beijing(data?.Reset);
            count.Text=data==null?"此额度暂不可用":stale?"旧数据 · 等待刷新":"距离重置 "+QuotaView.Countdown(data.Reset,now);
        }
    }
}
