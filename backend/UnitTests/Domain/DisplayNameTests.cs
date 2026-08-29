using Domain.ValueObjects;

namespace UnitTests.Domain;

public sealed class DisplayNameTests
{
    [Fact]
    public void Create_normalizes_outer_and_internal_whitespace()
    {
        var displayName = DisplayName.Create("  Ada   Lovelace\t ");

        Assert.Equal("Ada Lovelace", displayName.Value);
        Assert.Equal("Ada Lovelace", displayName.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_display_name(string? value)
    {
        Assert.Throws<ArgumentException>(() => DisplayName.Create(value!));
    }

    [Fact]
    public void Create_rejects_display_name_longer_than_maximum_length()
    {
        var value = new string('a', DisplayName.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => DisplayName.Create(value));
    }

    [Fact]
    public void TryCreate_returns_normalized_display_name_for_valid_input()
    {
        var created = DisplayName.TryCreate("  Grace   Hopper  ", out var displayName);

        Assert.True(created);
        Assert.NotNull(displayName);
        Assert.Equal("Grace Hopper", displayName.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_returns_false_for_invalid_input(string? value)
    {
        var created = DisplayName.TryCreate(value, out var displayName);

        Assert.False(created);
        Assert.Null(displayName);
    }

    [Fact]
    public void Equality_uses_normalized_value()
    {
        var first = DisplayName.Create("Ada   Lovelace");
        var second = DisplayName.Create(" Ada Lovelace ");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void CompareTo_orders_names_by_canonical_value()
    {
        var first = DisplayName.Create("Ada Lovelace");
        var second = DisplayName.Create("Grace Hopper");

        Assert.True(first.CompareTo(second) < 0);
        Assert.True(second.CompareTo(first) > 0);
        Assert.Equal(0, first.CompareTo(DisplayName.Create("Ada   Lovelace")));
    }
}
