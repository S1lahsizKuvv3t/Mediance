namespace Mediance.Core.Settings;

public sealed record ReleaseVersion(int Major, int Minor, int Patch, string? Prerelease)
    : IComparable<ReleaseVersion>
{
    public static bool TryParse(string? value, out ReleaseVersion version)
    {
        version = new(0, 0, 0, null);
        if (string.IsNullOrWhiteSpace(value)) return false;
        var clean = value.Trim().TrimStart('v', 'V').Split('+', 2)[0];
        var parts = clean.Split('-', 2);
        var numbers = parts[0].Split('.');
        var patch = 0;
        if (numbers.Length < 2 || numbers.Length > 4 ||
            !int.TryParse(numbers[0], out var major) ||
            !int.TryParse(numbers[1], out var minor) ||
            (numbers.Length > 2 && !int.TryParse(numbers[2], out patch)))
            return false;
        version = new(major, minor, patch,
            parts.Length > 1 ? parts[1] : null);
        return true;
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        var core = Major.CompareTo(other.Major);
        if (core == 0) core = Minor.CompareTo(other.Minor);
        if (core == 0) core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;
        if (Prerelease is null) return other.Prerelease is null ? 0 : 1;
        if (other.Prerelease is null) return -1;
        var left = Prerelease.Split('.', '-');
        var right = other.Prerelease.Split('.', '-');
        for (var index = 0; index < Math.Max(left.Length, right.Length); index++)
        {
            if (index >= left.Length) return -1;
            if (index >= right.Length) return 1;
            var leftNumeric = int.TryParse(left[index], out var leftNumber);
            var rightNumeric = int.TryParse(right[index], out var rightNumber);
            var part = leftNumeric && rightNumeric
                ? leftNumber.CompareTo(rightNumber)
                : string.Compare(left[index], right[index], StringComparison.OrdinalIgnoreCase);
            if (part != 0) return part;
        }
        return 0;
    }
}
