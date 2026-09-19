using System.Globalization;

namespace ChromeBookmarksManager.Chrome;

public static class ChromeBookmarkTime
{
    private static readonly DateTimeOffset WindowsEpoch =
        new(1601, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static string ToRaw(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var ticksSinceWindowsEpoch = checked(utc.Ticks - WindowsEpoch.Ticks);

        if (ticksSinceWindowsEpoch < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Chrome bookmark timestamps cannot predate the Windows epoch.");
        }

        var microseconds = ticksSinceWindowsEpoch / 10;
        return microseconds.ToString(CultureInfo.InvariantCulture);
    }
}
