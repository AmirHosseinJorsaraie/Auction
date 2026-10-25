namespace ArtMarketplace.Domain.Common;

public sealed record Money
{
    public Money(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        }

        if (currency.Length != 3 || currency.Any(character => !char.IsAsciiLetterUpper(character)))
        {
            throw new ArgumentException("Currency must be a three-letter uppercase ISO code.", nameof(currency));
        }

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }
}
