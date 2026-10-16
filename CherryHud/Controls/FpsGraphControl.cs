using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CherryHud.Controls;

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

    private static readonly IPen GraphLinePen = new Pen(
        new SolidColorBrush(Color.FromRgb(0, 230, 118)),
        1.5,
        lineCap: PenLineCap.Round,
        lineJoin: PenLineJoin.Round);

    private static readonly IBrush DotBrush = new SolidColorBrush(Color.FromRgb(0, 255, 127));
    private static readonly IBrush DotGlowBrush = new SolidColorBrush(Color.FromArgb(70, 0, 255, 127));

    private double _currentMaxScaleMs = 33.33;

    public FpsGraphControl()
    {
        RenderOptions.SetEdgeMode(this, EdgeMode.Antialias);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        var frametimes = Frametimes;
        if (w <= 4 || h <= 4 || frametimes == null || frametimes.Count < 2)
        {
            return;
        }

        // Dynamic scale based on frametimes
        double targetMs = TargetFrametimeMs > 0 ? TargetFrametimeMs : 16.67;
        double targetMaxScale = Math.Max(targetMs * 2.2, 33.33);

        int count = frametimes.Count;
        for (int i = 0; i < count; i++)
        {
            double ft = frametimes[i];
            if (ft > targetMaxScale && ft <= 200.0)
            {
                targetMaxScale = ft;
            }
        }
        targetMaxScale = Math.Min(targetMaxScale, 100.0); // cap max scale to 100ms

        // Smooth damping on vertical scale to avoid jarring vertical jumps
        if (_currentMaxScaleMs <= 0)
        {
            _currentMaxScaleMs = targetMaxScale;
        }
        else if (targetMaxScale > _currentMaxScaleMs)
        {
            // Rapidly expand when stutters appear
            _currentMaxScaleMs += (targetMaxScale - _currentMaxScaleMs) * 0.35;
        }
        else
        {
            // Gently glide back down
            _currentMaxScaleMs += (targetMaxScale - _currentMaxScaleMs) * 0.08;
        }

        double maxScaleMs = _currentMaxScaleMs;
        double stepX = (w - 6) / (count - 1);

        var lineGeometry = new StreamGeometry();
        Point lastPoint = default;

        using (var lineCtx = lineGeometry.Open())
        {
            for (int i = 0; i < count; i++)
            {
                double val = frametimes[i];
                double clampedVal = Math.Clamp(val, 0, maxScaleMs);
                // 0 ms at bottom, high ms at top
                double y = h - ((clampedVal / maxScaleMs) * (h - 8)) - 4;
                double x = 3 + (i * stepX);
                var pt = new Point(x, y);

                if (i == 0)
                {
                    lineCtx.BeginFigure(pt, isFilled: false);
                }
                else
                {
                    lineCtx.LineTo(pt);
                }

                if (i == count - 1)
                {
                    lastPoint = pt;
                }
            }

            lineCtx.EndFigure(isClosed: false);
        }

        // Draw crisp antialiased green line
        context.DrawGeometry(null, GraphLinePen, lineGeometry);

        // Draw glowing dot on the latest frametime (far right)
        context.DrawEllipse(DotGlowBrush, null, lastPoint, 4.0, 4.0);
        context.DrawEllipse(DotBrush, null, lastPoint, 2.0, 2.0);
    }
}
