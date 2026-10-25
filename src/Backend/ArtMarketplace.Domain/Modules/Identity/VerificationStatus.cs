namespace ArtMarketplace.Domain.Modules.Identity;

public enum VerificationStatus
{
    Draft = 0,
    Pending = 1,
    ChangesRequested = 2,
    Approved = 3,
    Rejected = 4,
    Revoked = 5,
    Expired = 6,
}
