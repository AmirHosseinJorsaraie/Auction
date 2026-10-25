namespace ArtMarketplace.Domain.Modules.Profiles;

public sealed class SavedAddress
{
    private SavedAddress()
    {
    }

    public SavedAddress(Guid id, Guid userId, string label, string recipientName, string addressLine, string postalCode)
    {
        if (id == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Address and user identifiers are required.");
        }

        Id = id;
        UserId = userId;
        Label = Require(label, 60);
        RecipientName = Require(recipientName, 120);
        AddressLine = Require(addressLine, 500);
        PostalCode = Require(postalCode, 32);
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public string RecipientName { get; private set; } = string.Empty;

    public string AddressLine { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    private static string Require(string value, int maximumLength)
    {
        var normalized = value.Trim();
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value must contain 1 to {maximumLength} characters.", nameof(value));
        }

        return normalized;
    }
}
