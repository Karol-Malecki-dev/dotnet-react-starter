using Domain.ValueObjects;

namespace UnitTests.Domain;

public sealed class EmailAddressTests
{
    [Fact]
    public void Create_normalizes_email_and_domain()
    {
        var address = EmailAddress.Create("  User.Name@Example.COM  ");

        Assert.Equal("user.name@example.com", address.Value);
        Assert.Equal("example.com", address.Domain);
        Assert.Equal("user.name@example.com", address.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_email(string? email)
    {
        Assert.Throws<ArgumentException>(() => EmailAddress.Create(email!));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("user@example.com extra")]
    [InlineData("user @example.com")]
    public void Create_rejects_invalid_format(string email)
    {
        Assert.Throws<ArgumentException>(() => EmailAddress.Create(email));
    }

    [Fact]
    public void Create_rejects_email_longer_than_maximum_length()
    {
        var email = new string('a', EmailAddress.MaxLength) + "@example.com";

        Assert.Throws<ArgumentException>(() => EmailAddress.Create(email));
    }

    [Fact]
    public void TryCreate_returns_normalized_address_for_valid_input()
    {
        var created = EmailAddress.TryCreate(" User@Example.com ", out var address);

        Assert.True(created);
        Assert.NotNull(address);
        Assert.Equal("user@example.com", address.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public void TryCreate_returns_false_for_invalid_input(string? email)
    {
        var created = EmailAddress.TryCreate(email, out var address);

        Assert.False(created);
        Assert.Null(address);
    }

    [Fact]
    public void Equality_uses_normalized_value()
    {
        var first = EmailAddress.Create("User@Example.com");
        var second = EmailAddress.Create(" user@example.com ");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void CompareTo_orders_addresses_by_canonical_value()
    {
        var first = EmailAddress.Create("a@example.com");
        var second = EmailAddress.Create("b@example.com");

        Assert.True(first.CompareTo(second) < 0);
        Assert.True(second.CompareTo(first) > 0);
        Assert.Equal(0, first.CompareTo(EmailAddress.Create("A@EXAMPLE.COM")));
    }
}
