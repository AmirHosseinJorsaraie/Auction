namespace ArtMarketplace.Application.Authorization;

public static class MarketplacePolicies
{
    public const string StaffWithMfa = "staff-with-mfa";
    public const string VerifySellers = "verify-sellers";
    public const string SuperAdminWithMfa = "superadmin-with-mfa";
    public const string ReviewsDecide = "reviews-decide";
    public const string ReviewsOverride = "reviews-override";
}

public static class MarketplacePermissions
{
    public const string ClaimType = "permission";
    public const string ReviewsDecide = "Reviews.Decide";
    public const string ReviewsOverride = "Reviews.Override";
}
