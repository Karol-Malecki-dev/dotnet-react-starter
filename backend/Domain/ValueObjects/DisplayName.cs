namespace Domain.ValueObjects;

/// <summary>
/// A normalized, non-empty display name used by the user domain.
/// </summary>
public sealed record DisplayName : IComparable<DisplayName>
{
    public const int MaxLength = 200;

    private DisplayName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static DisplayName Create(string value)
    {
        var normalizedValue = Normalize(value);

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            throw new ArgumentException("Display name is required.", nameof(value));
        }

        if (normalizedValue.Length > MaxLength)
        {
            throw new ArgumentException($"Display name cannot exceed {MaxLength} characters.", nameof(value));
        }

        return new DisplayName(normalizedValue);
    }

    public static bool TryCreate(string? value, out DisplayName? displayName)
    {
        try
        {
            displayName = Create(value ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            displayName = null;
            return false;
        }
    }

    public override string ToString() => Value;

    public int CompareTo(DisplayName? other)
        => other is null ? 1 : StringComparer.Ordinal.Compare(Value, other.Value);

    private static string Normalize(string value)
        => string.Join(
            ' ',
            (value ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
