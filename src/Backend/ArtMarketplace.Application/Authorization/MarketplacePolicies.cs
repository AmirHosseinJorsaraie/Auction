namespace ArtMarketplace.Application.Authorization;

public static class MarketplacePolicies
{
    public const string StaffWithMfa = "staff-with-mfa";
    public const string VerifySellers = "verify-sellers";
    public const string SuperAdminWithMfa = "superadmin-with-mfa";
    public const string ReviewsDecide = "reviews-decide";
    public const string ReviewsOverride = "reviews-override";
    public const string AdministrationRead = "administration-read";
    public const string UsersManage = "users-manage";
    public const string RolesManage = "roles-manage";
    public const string CatalogManage = "catalog-manage";
    public const string ContentManage = "content-manage";
    public const string AuditRead = "audit-read";
    public const string PaymentsRead = "payments-read";
}

public static class MarketplacePermissions
{
    public const string ClaimType = "permission";
    public const string ReviewsDecide = "Reviews.Decide";
    public const string ReviewsOverride = "Reviews.Override";
    public const string AdministrationRead = "Administration.Read";
    public const string UsersManage = "Users.Manage";
    public const string RolesManage = "Roles.Manage";
    public const string CatalogManage = "Catalog.Manage";
    public const string ContentManage = "Content.Manage";
    public const string AuditRead = "Audit.Read";
    public const string PaymentsRead = "Payments.Read";
}
