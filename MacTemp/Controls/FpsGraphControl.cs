using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MacTemp.Controls;

public class FpsGraphControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> FrametimesProperty =
        AvaloniaProperty.Register<FpsGraphControl, IReadOnlyList<double>?>(nameof(Frametimes));

    public static readonly StyledProperty<double> TargetFrametimeMsProperty =
        AvaloniaProperty.Register<FpsGraphControl, double>(nameof(TargetFrametimeMs), defaultValue: 16.67);

    public IReadOnlyList<double>? Frametimes
    {
        get => GetValue(FrametimesProperty);
        set => SetValue(FrametimesProperty, value);
    }

    public double TargetFrametimeMs
    {
        get => GetValue(TargetFrametimeMsProperty);
        set => SetValue(TargetFrametimeMsProperty, value);
    }

    static FpsGraphControl()
    {
        AffectsRender<FpsGraphControl>(FrametimesProperty, TargetFrametimeMsProperty);
    }

    private static readonly IPen GraphLinePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 230, 118)), 1.5);
    private static readonly IBrush DotBrush = new SolidColorBrush(Color.FromRgb(0, 255, 127));

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 4 || h <= 4 || Frametimes == null || Frametimes.Count < 2)
        {
            return;
        }

        // Dynamic scale based on frametimes
        double targetMs = TargetFrametimeMs > 0 ? TargetFrametimeMs : 16.67;
        double maxScaleMs = Math.Max(targetMs * 2.2, 33.33);

        foreach (var ft in Frametimes)
        {
            if (ft > maxScaleMs && ft <= 200.0)
            {
                maxScaleMs = ft;
            }
        }
        maxScaleMs = Math.Min(maxScaleMs, 100.0); // cap max scale to 100ms

        int count = Frametimes.Count;
        double stepX = (w - 6) / (count - 1);

        var points = new Point[count];
        for (int i = 0; i < count; i++)
        {
            double val = Frametimes[i];
            double clampedVal = Math.Clamp(val, 0, maxScaleMs);
            // 0 ms at bottom, high ms at top
            double y = h - ((clampedVal / maxScaleMs) * (h - 8)) - 4;
            double x = 3 + (i * stepX);
            points[i] = new Point(x, y);
        }

        // Draw Frametime Graph as a clean pure green line
        var lineGeometry = new StreamGeometry();
        using (var lineCtx = lineGeometry.Open())
        {
            lineCtx.BeginFigure(points[0], isFilled: false);
            for (int i = 1; i < count; i++)
            {
                lineCtx.LineTo(points[i]);
            }
            lineCtx.EndFigure(isClosed: false);
        }
        context.DrawGeometry(null, GraphLinePen, lineGeometry);

        // Draw glowing green dot on the latest frametime (far right)
        Point lastPoint = points[count - 1];
        context.DrawEllipse(DotBrush, null, lastPoint, 2.2, 2.2);
    }
}
