using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MacTemp.Controls;

public class FpsGraphControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> HistoryProperty =
        AvaloniaProperty.Register<FpsGraphControl, IReadOnlyList<double>?>(nameof(History));

    public static readonly StyledProperty<double> TargetFpsProperty =
        AvaloniaProperty.Register<FpsGraphControl, double>(nameof(TargetFps), defaultValue: 60.0);

    public IReadOnlyList<double>? History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    public double TargetFps
    {
        get => GetValue(TargetFpsProperty);
        set => SetValue(TargetFpsProperty, value);
    }

    static FpsGraphControl()
    {
        AffectsRender<FpsGraphControl>(HistoryProperty, TargetFpsProperty);
    }

    private static readonly IBrush SlotBackground = new SolidColorBrush(Color.FromArgb(90, 10, 12, 18));
    private static readonly IPen SlotBorder = new Pen(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), 1);
    private static readonly IPen TargetLinePen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1, DashStyle.Dash);
    private static readonly IPen LinePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 230, 118)), 1.5);
    private static readonly IBrush DotBrush = new SolidColorBrush(Color.FromRgb(0, 255, 127));

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 4 || h <= 4)
        {
            return;
        }

        var rect = new Rect(0, 0, w, h);
        var rrect = new RoundedRect(rect, 4);

        // 1. Draw Slot Background & Border
        context.DrawRectangle(SlotBackground, SlotBorder, rrect);

        // 2. Draw Target FPS line (e.g. 60 or 120 FPS)
        double maxDisplayFps = Math.Max(TargetFps, 60.0);
        if (History != null)
        {
            foreach (var val in History)
            {
                if (val > maxDisplayFps) maxDisplayFps = val;
            }
        }
        maxDisplayFps = Math.Ceiling(maxDisplayFps / 30.0) * 30.0; // round up to multiple of 30

        double targetY = h - (Math.Clamp(TargetFps / maxDisplayFps, 0, 1) * (h - 6)) - 3;
        context.DrawLine(TargetLinePen, new Point(3, targetY), new Point(w - 3, targetY));

        if (History == null || History.Count < 2)
        {
            return;
        }

        int count = History.Count;
        double stepX = (w - 6) / (count - 1);

        var points = new Point[count];
        for (int i = 0; i < count; i++)
        {
            double val = History[i];
            double clampedVal = Math.Clamp(val, 0, maxDisplayFps);
            double y = h - ((clampedVal / maxDisplayFps) * (h - 8)) - 4;
            double x = 3 + (i * stepX);
            points[i] = new Point(x, y);
        }

        // 3. Draw Gradient Fill under line (MangoHud style)
        var fillGeometry = new StreamGeometry();
        using (var fillCtx = fillGeometry.Open())
        {
            fillCtx.BeginFigure(new Point(points[0].X, h - 2), isFilled: true);
            fillCtx.LineTo(points[0]);

            for (int i = 1; i < count; i++)
            {
                fillCtx.LineTo(points[i]);
            }

            fillCtx.LineTo(new Point(points[count - 1].X, h - 2));
            fillCtx.EndFigure(isClosed: true);
        }

        var fillBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(70, 0, 230, 118), 0.0),
                new GradientStop(Color.FromArgb(5, 0, 230, 118), 1.0)
            }
        };

        context.DrawGeometry(fillBrush, null, fillGeometry);

        // 4. Draw Graph Line
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

        context.DrawGeometry(null, LinePen, lineGeometry);

        // 5. Draw glowing dot on the latest value (far right)
        Point lastPoint = points[count - 1];
        context.DrawEllipse(DotBrush, null, lastPoint, 2.0, 2.0);
    }
}
