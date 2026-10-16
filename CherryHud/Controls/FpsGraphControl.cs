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

    public static readonly StyledProperty<Color> LineColorProperty =
        AvaloniaProperty.Register<FpsGraphControl, Color>(nameof(LineColor), defaultValue: Color.FromRgb(112, 208, 120));

    public static readonly StyledProperty<Color> DotColorProperty =
        AvaloniaProperty.Register<FpsGraphControl, Color>(nameof(DotColor), defaultValue: Color.FromRgb(212, 22, 48));

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

    public Color LineColor
    {
        get => GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    public Color DotColor
    {
        get => GetValue(DotColorProperty);
        set => SetValue(DotColorProperty, value);
    }

    static FpsGraphControl()
    {
        AffectsRender<FpsGraphControl>(FrametimesProperty, TargetFrametimeMsProperty, LineColorProperty, DotColorProperty);
    }

    private Color _cachedLineColor;
    private Color _cachedDotColor;
    private IPen? _linePen;
    private IBrush? _dotBrush;
    private IBrush? _dotGlowBrush;

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

        // Ensure cached drawing resources match current LineColor & DotColor with 0 allocations per frame
        var lineColor = LineColor;
        var dotColor = DotColor;
        if (_cachedLineColor != lineColor || _cachedDotColor != dotColor || _linePen == null || _dotBrush == null)
        {
            _cachedLineColor = lineColor;
            _cachedDotColor = dotColor;
            _linePen = new Pen(
                new SolidColorBrush(lineColor),
                1.5,
                lineCap: PenLineCap.Round,
                lineJoin: PenLineJoin.Round);

            _dotBrush = new SolidColorBrush(dotColor);
            _dotGlowBrush = new SolidColorBrush(Color.FromArgb(90, dotColor.R, dotColor.G, dotColor.B));
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

        // 1. Draw crisp antialiased sparkline
        if (_linePen != null)
        {
            context.DrawGeometry(null, _linePen, lineGeometry);
        }

        // 2. Draw glowing dot on the latest frametime (far right)
        if (_dotGlowBrush != null && _dotBrush != null)
        {
            context.DrawEllipse(_dotGlowBrush, null, lastPoint, 4.5, 4.5);
            context.DrawEllipse(_dotBrush, null, lastPoint, 2.2, 2.2);
        }
    }
}
