namespace ArtMarketplace.Domain.Modules.Profiles;

public sealed class UserProfile
{
    private UserProfile()
    {
    }

    public UserProfile(Guid userId, string displayName, string locale)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user identifier is required.", nameof(userId));
        }

        UserId = userId;
        SetDisplayName(displayName);
        SetLocale(locale);
    }

    public Guid UserId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string Locale { get; private set; } = "fa-IR";

    public string? PublicSellerBio { get; private set; }

    public void Update(string displayName, string locale, string? publicSellerBio)
    {
        SetDisplayName(displayName);
        SetLocale(locale);
        PublicSellerBio = NormalizeOptional(publicSellerBio, 1_000);
    }

    private void SetDisplayName(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 2 or > 120)
        {
            throw new ArgumentException("Display name must contain 2 to 120 characters.", nameof(value));
        }

        DisplayName = normalized;
    }

    private void SetLocale(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 2 or > 16)
        {
            throw new ArgumentException("Locale must contain 2 to 16 characters.", nameof(value));
        }

        Locale = normalized;
    }

    private static string? NormalizeOptional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", nameof(value));
        }

        return normalized;
    }
}
