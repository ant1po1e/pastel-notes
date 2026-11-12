using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace PastelNotes.Controls;

public class IndentGuides : FrameworkElement
{
    private const double MarkerX = 16;
    private const double ElbowY = 16;
    private const double Radius = 6;
    private const double StubStart = 29;

    public static readonly DependencyProperty LevelProperty = Register(nameof(Level), typeof(int), 0);
    public static readonly DependencyProperty MaskProperty = Register(nameof(Mask), typeof(int), 0);
    public static readonly DependencyProperty HasChildrenProperty = Register(nameof(HasChildren), typeof(bool), false);
    public static readonly DependencyProperty StepProperty = Register(nameof(Step), typeof(double), 26.0);
    public static readonly DependencyProperty BrushProperty = Register(nameof(Brush), typeof(Brush), null);

    private Brush? _penBrush;
    private Pen? _pen;

    public int Level { get => (int)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public int Mask { get => (int)GetValue(MaskProperty); set => SetValue(MaskProperty, value); }
    public bool HasChildren { get => (bool)GetValue(HasChildrenProperty); set => SetValue(HasChildrenProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public Brush? Brush { get => (Brush?)GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    private static DependencyProperty Register(string name, Type type, object? defaultValue)
        => DependencyProperty.Register(name, type, typeof(IndentGuides),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));

    private Pen? GetPen()
    {
        if (Brush == null) return null;
        if (_pen == null || !ReferenceEquals(_penBrush, Brush))
        {
            _penBrush = Brush;
            _pen = new Pen(Brush, 1.5) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        }
        return _pen;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var pen = GetPen();
        if (pen == null) return;

        int level = Math.Min(Math.Max(Level, 0), 31);
        double height = ActualHeight;

        for (int c = 0; c < level; c++)
        {
            double x = c * Step + MarkerX;
            bool continues = (Mask & (1 << c)) != 0;

            if (c < level - 1)
            {
                if (continues) drawingContext.DrawLine(pen, new Point(x, 0), new Point(x, height));
                continue;
            }

            var elbow = new StreamGeometry();
            using (var context = elbow.Open())
            {
                context.BeginFigure(new Point(x, 0), false, false);
                context.LineTo(new Point(x, ElbowY - Radius), true, false);
                context.ArcTo(new Point(x + Radius, ElbowY), new Size(Radius, Radius), 0, false,
                    SweepDirection.Counterclockwise, true, false);
                context.LineTo(new Point(level * Step + 5, ElbowY), true, false);
            }
            elbow.Freeze();
            drawingContext.DrawGeometry(null, pen, elbow);

            if (continues) drawingContext.DrawLine(pen, new Point(x, ElbowY), new Point(x, height));
        }

        if (HasChildren)
        {
            double x = level * Step + MarkerX;
            drawingContext.DrawLine(pen, new Point(x, StubStart), new Point(x, height));
        }
    }
}
