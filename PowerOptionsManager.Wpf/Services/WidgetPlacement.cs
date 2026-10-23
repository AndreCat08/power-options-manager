using System.Windows;

namespace PowerOptionsManager.Services;

/// <summary>Where the desktop widget goes, in DIPs. Pure so it can be unit-tested.</summary>
public static class WidgetPlacement
{
    public const double EdgeMargin = 16;

    /// <summary>
    /// Uses the saved position when the widget would still be on a screen (clamped inside the
    /// virtual screen), otherwise the bottom-right corner of the primary work area.
    /// </summary>
    public static Point Resolve(double? savedLeft, double? savedTop, Size size, Rect workArea, Rect virtualScreen)
    {
        if (savedLeft is { } left && savedTop is { } top && IsOnScreen(left, top, size, virtualScreen))
        {
            return Clamp(new Point(left, top), size, virtualScreen);
        }
        return DefaultPosition(size, workArea);
    }

    public static Point DefaultPosition(Size size, Rect workArea) =>
        Clamp(new Point(workArea.Right - size.Width - EdgeMargin, workArea.Bottom - size.Height - EdgeMargin), size, workArea);

    /// <summary>Keeps the whole widget inside <paramref name="area"/> (top-left wins if it is larger than the area).</summary>
    public static Point Clamp(Point position, Size size, Rect area)
    {
        var x = Math.Max(area.Left, Math.Min(position.X, area.Right - size.Width));
        var y = Math.Max(area.Top, Math.Min(position.Y, area.Bottom - size.Height));
        return new Point(x, y);
    }

    /// <summary>True when at least a usable part of the widget (its top 32 DIPs, the drag area) is visible.</summary>
    private static bool IsOnScreen(double left, double top, Size size, Rect virtualScreen)
    {
        var grip = new Rect(left, top, Math.Max(size.Width, 1), Math.Min(Math.Max(size.Height, 1), 32));
        var visible = Rect.Intersect(grip, virtualScreen);
        return !visible.IsEmpty && visible.Width >= Math.Min(48, grip.Width) && visible.Height >= grip.Height / 2;
    }
}
