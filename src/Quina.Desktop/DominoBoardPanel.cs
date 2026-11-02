using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quina.Desktop;

/// <summary>Exibe a ordem oficial em linhas alternadas. Os dois primeiros filhos são os encaixes.</summary>
public sealed class DominoBoardPanel : Panel
{
    private const int Columns = 7;
    private const double CellWidth = 116, RowHeight = 140, EndWidth = 108;
    private int Count => Math.Max(0, InternalChildren.Count - 2);
    private int UsedColumns => Math.Max(1, Math.Min(Columns, Count));
    private Point Position(int index)
    {
        int row = index / Columns, column = index % Columns;
        if (row % 2 == 1) column = Columns - 1 - column;
        return new Point(EndWidth + column * CellWidth, row * RowHeight);
    }
    public void ClearTiles()
    {
        if (Children.Count > 2) Children.RemoveRange(2, Children.Count - 2);
        InvalidateMeasure(); InvalidateVisual();
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        for (int i = 0; i < InternalChildren.Count; i++)
            InternalChildren[i].Measure(i < 2 ? new Size(EndWidth, 100) : new Size(CellWidth, RowHeight));
        return new Size(UsedColumns * CellWidth + 2 * EndWidth,
            Math.Max(1, (Count + Columns - 1) / Columns) * RowHeight);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count < 2) return finalSize;
        InternalChildren[0].Arrange(new Rect(0, 29, EndWidth - 8, 82));
        if (Count == 0)
            InternalChildren[1].Arrange(new Rect((finalSize.Width - EndWidth) / 2, 29, EndWidth, 82));
        else
        {
            var last = Position(Count - 1);
            bool reverse = ((Count - 1) / Columns) % 2 == 1;
            InternalChildren[1].Arrange(new Rect(reverse ? last.X - EndWidth : last.X + CellWidth + 8,
                last.Y + 29, EndWidth - 8, 82));
        }
        for (int i = 0; i < Count; i++)
        {
            var tile = InternalChildren[i + 2];
            tile.RenderTransformOrigin = new Point(.5, .5);
            tile.RenderTransform = new RotateTransform(i / Columns % 2 == 1 ? 180 : 0);
            tile.Arrange(new Rect(Position(i), new Size(CellWidth, RowHeight)));
        }
        return finalSize;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        // Une as linhas com um traço discreto, sem inventar peças na sequência.
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(125, 112, 157)), 3)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        for (int i = Columns; i < Count; i += Columns)
        {
            var previous = Position(i - 1);
            var next = Position(i);
            bool right = (i / Columns) % 2 == 1;
            double edge = right ? previous.X + CellWidth - 6 : previous.X + 6;
            double bend = edge + (right ? 23 : -23);
            var path = new StreamGeometry();
            using (var ctx = path.Open())
            {
                ctx.BeginFigure(new Point(edge, previous.Y + RowHeight / 2), false, false);
                ctx.BezierTo(new Point(bend, previous.Y + RowHeight / 2),
                    new Point(bend, next.Y + RowHeight / 2), new Point(edge, next.Y + RowHeight / 2), true, false);
            }
            dc.DrawGeometry(null, pen, path);
        }
    }
}
