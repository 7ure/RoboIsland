using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RoboIsland;

public class SettingsWindow : Window
{
    readonly Settings s;
    readonly Action changed;

    public SettingsWindow(Settings settings, Action onChanged)
    {
        s = settings; changed = onChanged;
        Width = 380; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)); Foreground = Brushes.White;
        Build();
    }

    string L(string k) => Loc.T(k, s.Lang);

    void Build()
    {
        Title = "Robo Island – " + L("settings");
        FlowDirection = s.Lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        var p = new StackPanel { Margin = new Thickness(20) };
        void Row(FrameworkElement e) { e.Margin = new Thickness(0, 5, 0, 5); p.Children.Add(e); }
        void Chk(string k, bool v, Action<bool> set)
        {
            var c = new CheckBox { Content = L(k), IsChecked = v, Foreground = Brushes.White };
            RoutedEventHandler h = (_, _) => { set(c.IsChecked == true); s.Save(); changed(); };
            c.Checked += h; c.Unchecked += h; Row(c);
        }
        void Label(string k) => Row(new TextBlock { Text = L(k), Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 0) });

        Label("language");
        var lang = new ComboBox { SelectedIndex = s.Lang == "ar" ? 0 : 1 };
        lang.Items.Add("العربية"); lang.Items.Add("English");
        lang.SelectionChanged += (_, _) => { s.Lang = lang.SelectedIndex == 0 ? "ar" : "en"; s.Save(); changed(); Build(); };
        Row(lang);

        Chk("robo", s.RoboEnabled, v => s.RoboEnabled = v);
        Chk("startup", s.Startup, v => { s.Startup = v; Integrations.Startup(v); });

        Label("position");
        var keys = new[] { "left", "center", "right" };
        var pos = new ComboBox { SelectedIndex = Array.IndexOf(keys, s.Position) };
        foreach (var k in keys) pos.Items.Add(L(k));
        pos.SelectionChanged += (_, _) => { if (pos.SelectedIndex >= 0) { s.Position = keys[pos.SelectedIndex]; s.Save(); changed(); } };
        Row(pos);

        Label("speed");
        var sp = new Slider { Minimum = .5, Maximum = 2, Value = s.Speed };
        sp.ValueChanged += (_, _) => { s.Speed = sp.Value; s.Save(); };
        Row(sp);

        Label("nStart");
        p.Children.RemoveAt(p.Children.Count - 1);
        Chk("nStart", s.NotifyStart, v => s.NotifyStart = v);
        Chk("nDone", s.NotifyDone, v => s.NotifyDone = v);
        Chk("nApproval", s.NotifyApproval, v => s.NotifyApproval = v);
        Chk("nError", s.NotifyError, v => s.NotifyError = v);
        Chk("claude", s.Claude, v => s.Claude = v);
        Chk("codex", s.Codex, v => s.Codex = v);

        Button Btn(string k, Action a)
        {
            var b = new Button { Content = L(k), Padding = new Thickness(10, 5, 10, 5) };
            b.Click += (_, _) => a(); Row(b); return b;
        }
        Btn("connectClaude", () => MessageBox.Show(L(Integrations.Claude() ? "connected" : "failed")));
        Btn("connectCodex", () => MessageBox.Show(L(Integrations.Codex() ? "connected" : "failed")));
        Btn("reset", () => { s.Reset(); Integrations.Startup(false); changed(); Build(); });

        Content = new ScrollViewer { Content = p };
    }
}
