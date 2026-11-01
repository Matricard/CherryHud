using Avalonia;
using CherryHud.Services;
using Xunit;

namespace CherryHud.Tests;

public class OverlayPositionTests
{
    private static PixelPoint CalculateCornerPosition(
        OverlayCorner corner,
        int screenX, int screenY, int screenW, int screenH,
        int winW, int winH, int margin = 8)
    {
        int x = corner switch
        {
            OverlayCorner.TopRight => screenX + screenW - winW - margin,
            OverlayCorner.BottomLeft => screenX + margin,
            OverlayCorner.BottomRight => screenX + screenW - winW - margin,
            _ => screenX + margin
        };

        int y = corner switch
        {
            OverlayCorner.BottomLeft => screenY + screenH - winH - margin,
            OverlayCorner.BottomRight => screenY + screenH - winH - margin,
            _ => screenY + margin
        };

        return new PixelPoint(x, y);
    }

    [Fact]
    public void TopLeftCorner_PositionsWithMargin()
    {
        var pos = CalculateCornerPosition(OverlayCorner.TopLeft, 0, 0, 1920, 1080, 200, 210, 8);
        Assert.Equal(new PixelPoint(8, 8), pos);
    }

    [Fact]
    public void TopRightCorner_PositionsCorrectly()
    {
        var pos = CalculateCornerPosition(OverlayCorner.TopRight, 0, 0, 1920, 1080, 200, 210, 8);
        Assert.Equal(new PixelPoint(1920 - 200 - 8, 8), pos);
    }

    [Fact]
    public void BottomLeftCorner_PositionsCorrectly()
    {
        var pos = CalculateCornerPosition(OverlayCorner.BottomLeft, 0, 0, 1920, 1080, 200, 210, 8);
        Assert.Equal(new PixelPoint(8, 1080 - 210 - 8), pos);
    }

    [Fact]
    public void BottomRightCorner_PositionsCorrectly()
    {
        var pos = CalculateCornerPosition(OverlayCorner.BottomRight, 0, 0, 1920, 1080, 200, 210, 8);
        Assert.Equal(new PixelPoint(1920 - 200 - 8, 1080 - 210 - 8), pos);
    }

    [Fact]
    public void SecondaryDisplayWithOffset_CalculatesRelativeToDisplayOrigin()
    {
        // Second monitor placed at X=1920, Y=0, 2560x1440
        var pos = CalculateCornerPosition(OverlayCorner.TopRight, 1920, 0, 2560, 1440, 200, 210, 8);
        Assert.Equal(new PixelPoint(1920 + 2560 - 200 - 8, 8), pos);
    }

    [Fact]
    public void WindowedMode_TopLeft_PositionsInsideWindow()
    {
        // Game running windowed at X=421, Y=57, 1600x928
        var bounds = new GameWindowBounds(421, 57, 1600, 928);
        var pos = CalculateCornerPosition(OverlayCorner.TopLeft, bounds.X, bounds.Y, bounds.Width, bounds.Height, 200, 210, 8);
        Assert.Equal(new PixelPoint(421 + 8, 57 + 8), pos);
    }

    [Fact]
    public void WindowedMode_TopRight_PositionsInsideWindow()
    {
        var bounds = new GameWindowBounds(421, 57, 1600, 928);
        var pos = CalculateCornerPosition(OverlayCorner.TopRight, bounds.X, bounds.Y, bounds.Width, bounds.Height, 200, 210, 8);
        Assert.Equal(new PixelPoint(421 + 1600 - 200 - 8, 57 + 8), pos);
    }

    [Fact]
    public void WindowedMode_BottomLeft_PositionsInsideWindow()
    {
        var bounds = new GameWindowBounds(421, 57, 1600, 928);
        var pos = CalculateCornerPosition(OverlayCorner.BottomLeft, bounds.X, bounds.Y, bounds.Width, bounds.Height, 200, 210, 8);
        Assert.Equal(new PixelPoint(421 + 8, 57 + 928 - 210 - 8), pos);
    }

    [Fact]
    public void WindowedMode_BottomRight_PositionsInsideWindow()
    {
        var bounds = new GameWindowBounds(421, 57, 1600, 928);
        var pos = CalculateCornerPosition(OverlayCorner.BottomRight, bounds.X, bounds.Y, bounds.Width, bounds.Height, 200, 210, 8);
        Assert.Equal(new PixelPoint(421 + 1600 - 200 - 8, 57 + 928 - 210 - 8), pos);
    }

    [Fact]
    public void WindowedMode_WindowMoved_FollowsNewCoordinates()
    {
        // User dragged window from (421, 57) to (550, 120)
        var initialBounds = new GameWindowBounds(421, 57, 1600, 928);
        var movedBounds = new GameWindowBounds(550, 120, 1600, 928);

        var initialPos = CalculateCornerPosition(OverlayCorner.TopLeft, initialBounds.X, initialBounds.Y, initialBounds.Width, initialBounds.Height, 200, 210, 8);
        var movedPos = CalculateCornerPosition(OverlayCorner.TopLeft, movedBounds.X, movedBounds.Y, movedBounds.Width, movedBounds.Height, 200, 210, 8);

        Assert.Equal(new PixelPoint(429, 65), initialPos);
        Assert.Equal(new PixelPoint(558, 128), movedPos);
        Assert.NotEqual(initialPos, movedPos);
    }
}
