namespace ArtMarketplace.Identity.Authorization;

public static class MarketplaceRoles
{
    public const string User = "User";
    public const string Expert = "Expert";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";

    public static readonly string[] All = [User, Expert, Admin, SuperAdmin];

    public static bool RequiresMfa(string role) =>
        role is Expert or Admin or SuperAdmin;
}
