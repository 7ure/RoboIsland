using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RoboIsland;

public class IslandWindow : Window
{
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);

    readonly Settings S;
    readonly Border island = new(), face = new();
    readonly Rectangle eL = new(), eR = new();
    readonly TextBlock title = new(), sub = new(), info = new();
    readonly StackPanel details = new();
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer hide = new() { Interval = TimeSpan.FromSeconds(8) };
    string state = "idle", app = "", task = "", tool = "";
    DateTime t0;
    bool open;
    public Action<string, string>? Balloon;

    string L(string k) => Loc.T(k, S.Lang);

    public IslandWindow(Settings s)
    {
        S = s;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = null;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Width = 440; Height = 170; Top = 0;
        SourceInitialized += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x08000000 | 0x80); // NOACTIVATE | TOOLWINDOW
        };

        island.Background = Brushes.Black; island.CornerRadius = new CornerRadius(20);
        island.VerticalAlignment = VerticalAlignment.Top; island.HorizontalAlignment = HorizontalAlignment.Center;
        island.Margin = new Thickness(0, 6, 0, 0); island.Cursor = Cursors.Hand; island.Width = 96; island.Height = 34;
        island.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, Opacity = .5, ShadowDepth = 2 };

        face.Width = 26; face.Height = 20; face.CornerRadius = new CornerRadius(7);
        face.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E));
        face.Margin = new Thickness(10, 7, 8, 7); face.RenderTransform = new TranslateTransform();
        var eyes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        foreach (var e in new[] { eL, eR })
        {
            e.Width = 4; e.Height = 7; e.RadiusX = 2; e.RadiusY = 2; e.Margin = new Thickness(2, 0, 2, 0);
            e.RenderTransformOrigin = new Point(.5, .5); e.RenderTransform = new ScaleTransform(1, 1);
            eyes.Children.Add(e);
        }
        face.Child = eyes;

        title.Foreground = Brushes.White; title.FontSize = 13; title.FontWeight = FontWeights.SemiBold;
        title.VerticalAlignment = VerticalAlignment.Center; title.MaxWidth = 200; title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.Margin = new Thickness(0, 0, 14, 0);
        sub.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB8)); sub.FontSize = 12; sub.TextWrapping = TextWrapping.Wrap; sub.MaxHeight = 36;
        info.Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x80)); info.FontSize = 11; info.Margin = new Thickness(0, 4, 0, 0);
        var dismiss = new Button { Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Tag = "d" };
        dismiss.Click += (_, _) => SetState("idle");
        details.Margin = new Thickness(14, 0, 14, 10);
        details.Children.Add(sub); details.Children.Add(info); details.Children.Add(dismiss);

        var top = new StackPanel { Orientation = Orientation.Horizontal, Height = 34 };
        top.Children.Add(face); top.Children.Add(title);
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        g.RowDefinitions.Add(new RowDefinition());
        g.Children.Add(top); Grid.SetRow(details, 1); g.Children.Add(details);
        island.Child = g;
        Content = island;

        island.MouseLeftButtonUp += (_, _) => { if (state != "idle") { open = !open; Apply(); } };
        hide.Tick += (_, _) => { hide.Stop(); SetState("idle"); };
        tick.Tick += (_, _) => UpdateInfo();

        Robo("idle");
        Listen();
    }

    public void Reload()
    {
        FlowDirection = S.Lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        ((Button)details.Children[2]).Content = L("dismiss");
        Apply();
    }

    string Elapsed() { var d = DateTime.Now - t0; return $"{(int)d.TotalMinutes:00}:{d.Seconds:00}"; }

    void UpdateInfo() => info.Text = app + (state == "working" ? $"  ·  {(tool.Length > 0 ? tool + "  ·  " : "")}{Elapsed()}" : "");

    void Apply()
    {
        bool idle = state == "idle";
        face.Visibility = S.RoboEnabled ? Visibility.Visible : Visibility.Collapsed;
        title.Visibility = idle ? Visibility.Collapsed : Visibility.Visible;
        var t = task.Length > 22 ? task[..22] + "…" : task;
        title.Text = L(state) + (!open && t.Length > 0 ? " · " + t : "");
        sub.Text = task; sub.Visibility = task.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateInfo();
        details.Visibility = open && !idle ? Visibility.Visible : Visibility.Collapsed;
        double w = idle ? (S.RoboEnabled ? 96 : 60) : open ? 400 : 250;
        double h = idle ? 34 : open ? 130 : 34;
        var dur = TimeSpan.FromMilliseconds(260 / Math.Max(.25, S.Speed));
        island.BeginAnimation(WidthProperty, new DoubleAnimation(w, dur) { EasingFunction = new QuadraticEase() });
        island.BeginAnimation(HeightProperty, new DoubleAnimation(h, dur) { EasingFunction = new QuadraticEase() });
        var wa = SystemParameters.WorkArea;
        Left = S.Position == "left" ? wa.Left + 16 : S.Position == "right" ? wa.Right - Width - 16 : wa.Left + (wa.Width - Width) / 2;
    }

    void SetState(string st, string? t = null, string? a = null)
    {
        hide.Stop();
        if (st != state)
        {
            if (st == "working") t0 = DateTime.Now;
            state = st; Robo(st);
        }
        if (t != null) task = t;
        if (a != null) app = a;
        if (st == "attention" || st == "error") open = true;
        if (st == "idle") { open = false; task = ""; tool = ""; }
        if (st == "success") hide.Start();
        if (st == "working") tick.Start(); else tick.Stop();
        Apply();
    }

    void Robo(string st)
    {
        var c = st switch { "working" => "#FFC83D", "success" => "#4ADE80", "attention" => "#FB923C", "error" => "#F87171", _ => "#FFFFFF" };
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(c)!;
        eL.Fill = b; eR.Fill = b;
        foreach (var e in new[] { eL, eR }) e.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        var ft = (TranslateTransform)face.RenderTransform;
        ft.BeginAnimation(TranslateTransform.YProperty, null); ft.BeginAnimation(TranslateTransform.XProperty, null);
        var ms = TimeSpan.FromMilliseconds(500 / Math.Max(.25, S.Speed));
        switch (st)
        {
            case "idle":
                var k = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
                k.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.8))));
                k.KeyFrames.Add(new LinearDoubleKeyFrame(.1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.9))));
                k.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.0))));
                foreach (var e in new[] { eL, eR }) e.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, k);
                break;
            case "working":
                foreach (var e in new[] { eL, eR })
                    e.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, .45, ms) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
                break;
            case "attention":
                ft.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -3, TimeSpan.FromMilliseconds(300)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
                break;
            case "error":
                ft.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-2, 2, TimeSpan.FromMilliseconds(70)) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(8) });
                break;
            case "success":
                foreach (var e in new[] { eL, eR })
                    e.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.4, ms) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(2) });
                break;
        }
    }

    async void Listen()
    {
        while (true)
        {
            try
            {
                using var s = new NamedPipeServerStream("RoboIsland", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await s.WaitForConnectionAsync();
                using var r = new StreamReader(s);
                var line = await r.ReadLineAsync();
                var e = line == null ? null : JsonSerializer.Deserialize<Evt>(line);
                if (e != null) Handle(e);
            }
            catch { }
        }
    }

    void Handle(Evt e)
    {
        if ((e.App == "claude" && !S.Claude) || (e.App == "codex" && !S.Codex)) return;
        var an = e.App == "claude" ? "Claude Code" : "Codex";
        switch (e.Kind)
        {
            case "start":
                tool = ""; SetState("working", e.Text, an);
                if (S.NotifyStart) Balloon?.Invoke(an, L("working") + ": " + e.Text);
                break;
            case "working":
                tool = e.Text; SetState("working", null, an); break;
            case "attention":
                SetState("attention", e.Text, an);
                if (S.NotifyApproval) Balloon?.Invoke(an, L("attention") + (e.Text.Length > 0 ? ": " + e.Text : ""));
                break;
            case "done":
                SetState("success", e.Text.Length > 0 ? e.Text : null, an);
                if (S.NotifyDone) Balloon?.Invoke(an, L("success"));
                break;
            case "error":
                SetState("error", e.Text, an);
                if (S.NotifyError) Balloon?.Invoke(an, L("error") + (e.Text.Length > 0 ? ": " + e.Text : ""));
                break;
            case "end": SetState("idle"); break;
        }
    }
}
