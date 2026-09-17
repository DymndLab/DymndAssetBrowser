using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DymndAssetBrowser.App;

internal static class ThemeRegressionTests
{
    public static void Run(Action<bool, string> check)
    {
        var app = new App();
        app.InitializeComponent();
        SolidColorBrush Brush(string key) => (SolidColorBrush)app.FindResource(key);
        check(Brush("ActionAccentBrush").Color == Parse("#AE0AD8"), "Exact requested purple brand color");
        foreach (var key in new[] { "ActionAccentBrush", "ActionAccentHoverBrush", "ActionAccentPressedBrush" })
            check(Contrast(Brush("OnActionAccentBrush").Color, Brush(key).Color) >= 4.5,
                $"Action label contrast is at least 4.5:1 on {key}");
        foreach (var background in new[] { "#15181C", "#20242A", "#22272D", "#2A3038", "#303A44" })
            check(Contrast(Brush("AccentBrush").Color, Parse(background)) >= 4.5,
                $"Purple accent text contrast is at least 4.5:1 on {background}");
        foreach (var background in new[] { "#465568", "#39485A", "#3A4652" })
            check(Contrast(Brush("FocusAccentBrush").Color, Parse(background)) >= 3,
                $"Purple focus and selection contrast is at least 3:1 on {background}");

        var action = new Button { Content = "POPULATE BUILD", Style = (Style)app.FindResource("ActionAccentButton") };
        action.ApplyTemplate();
        var surface = (Border)action.Template.FindName("Surface", action);
        check(ReferenceEquals(surface.Background, Brush("ActionAccentBrush")) &&
              ReferenceEquals(action.Foreground, Brush("OnActionAccentBrush")), "Action button renders purple with white label");
        foreach (var (property, resource) in new[] {
            (UIElement.IsMouseOverProperty, "ActionAccentHoverBrush"),
            (ButtonBase.IsPressedProperty, "ActionAccentPressedBrush") })
        {
            var trigger = action.Template.Triggers.OfType<Trigger>().Single(t => t.Property == property);
            var setter = trigger.Setters.OfType<Setter>().Single(s => s.Property == Border.BackgroundProperty);
            check(ReferenceEquals(setter.Value, Brush(resource)), $"Action {property.Name} uses tested purple surface");
        }

        var toggle = new ToggleButton { Content = "Materials", IsChecked = true };
        toggle.ApplyTemplate();
        var toggleSurface = (Border)toggle.Template.FindName("Surface", toggle);
        check(ReferenceEquals(toggleSurface.BorderBrush, Brush("FocusAccentBrush")), "Open picker has purple selection outline");
        check(Contrast(((SolidColorBrush)toggle.Foreground).Color, ((SolidColorBrush)toggleSurface.Background).Color) >= 4.5,
            "Open picker retains readable label contrast");
        app.Shutdown();
    }

    private static Color Parse(string color) => (Color)ColorConverter.ConvertFromString(color);
    private static double Contrast(Color a, Color b)
    {
        var la = Luminance(a); var lb = Luminance(b);
        return (Math.Max(la, lb) + .05) / (Math.Min(la, lb) + .05);
    }
    private static double Luminance(Color color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }
}
