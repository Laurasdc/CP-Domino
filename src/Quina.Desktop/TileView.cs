using System.Windows;
using System.Windows.Media;
using Quina.Core;

namespace Quina.Desktop;

/// <summary>Pedra vetorial com volume, porcelana, borda violeta e pontos em baixo-relevo.</summary>
public sealed class TileView : FrameworkElement
{
    public Tile Tile { get; }
    public bool Vertical { get; }
    private readonly bool selected;
    public TileView(Tile tile, bool vertical = true, bool selected = false)
    {
        Tile = tile; Vertical = vertical; this.selected = selected;
        Width = vertical ? 48 : 84; Height = vertical ? 88 : 48;
        ToolTip = $"Pedra {tile.ValorA} e {tile.ValorB}";
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var face = new Rect(3, 2, Width - 9, Height - 9);
        if (selected) dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(65, 181, 160, 255)), null, new Rect(0, 0, Width, Height), 8, 8);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), null, new Rect(5, 6, Width - 7, Height - 7), 5, 5);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(151, 149, 166)), null, new Rect(3, 5, Width - 9, Height - 8), 4, 4);
        var fill = new LinearGradientBrush(Color.FromRgb(253, 252, 255), Color.FromRgb(224, 222, 234), 55);
        dc.DrawRoundedRectangle(fill, new Pen(new SolidColorBrush(selected ? Color.FromRgb(181, 160, 255) : Color.FromRgb(245, 243, 252)), selected ? 2 : .8), face, 4, 4);
        double cx0 = face.X + face.Width / 2, cy0 = face.Y + face.Height / 2;
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(161, 155, 176)), .7), Vertical ? new Point(face.Left + 5, cy0) : new Point(cx0, face.Top + 5), Vertical ? new Point(face.Right - 5, cy0) : new Point(cx0, face.Bottom - 5));
        int[][] patterns = [[], [4], [0, 8], [0, 4, 8], [0, 2, 6, 8], [0, 2, 4, 6, 8], [0, 2, 3, 5, 6, 8]];
        for (int half = 0; half < 2; half++)
        {
            int value = half == 0 ? Tile.ValorA : Tile.ValorB;
            if (value is < 0 or > 6) continue;
            double cx = Vertical ? cx0 : face.X + face.Width / 4 + half * face.Width / 2;
            double cy = Vertical ? face.Y + face.Height / 4 + half * face.Height / 2 : cy0;
            foreach (var position in patterns[value])
            {
                var point = new Point(cx + (position % 3 - 1) * 8.5, cy + (position / 3 - 1) * 8.5);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 254, 255)), null, new Point(point.X, point.Y + .7), 3.1, 3.1);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(43, 35, 60)), new Pen(new SolidColorBrush(Color.FromRgb(100, 102, 91)), .5), point, 2.7, 2.7);
            }
        }
    }
}
