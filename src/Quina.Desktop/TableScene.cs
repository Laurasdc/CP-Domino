using System.Windows;
using System.Windows.Media;

namespace Quina.Desktop;

/// <summary>Fundo abstrato vetorial para boas-vindas e salão.</summary>
public sealed class TableScene : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w < 10 || h < 10) return;
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h), 24, 24));
        dc.DrawRectangle(new LinearGradientBrush(C("#302448"), C("#191922"), 65), null, new Rect(0, 0, w, h));
        var glow = new RadialGradientBrush(C("#594181"), Colors.Transparent);
        dc.DrawEllipse(glow, null, new Point(w * .9, h * .15), w * .7, h * .7);
        var line = new Pen(new SolidColorBrush(Color.FromArgb(35, 190, 166, 255)), 1);
        for (int i = 0; i < 4; i++)
            dc.DrawEllipse(null, line, new Point(w * .95, h * .95), w * (.4 + i * .14), h * (.3 + i * .12));
        dc.DrawEllipse(new SolidColorBrush(C("#80DFC4")), null, new Point(w * .86, h * .18), 5, 5);
        dc.DrawEllipse(new SolidColorBrush(C("#B5A0FF")), null, new Point(w * .77, h * .11), 3, 3);
        dc.Pop();
    }
    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}
