namespace ArtMarketplace.Application.Authorization;

public static class RoleManagementPolicy
{
    public static string? Rejection(Guid actorUserId, Guid targetUserId, string role, bool assign, bool alreadyAssigned, int superAdminCount)
    {
        if (assign && role == "SuperAdmin" && actorUserId == targetUserId && !alreadyAssigned) return "Self-promotion to SuperAdmin is forbidden.";
        if (!assign && role == "SuperAdmin" && alreadyAssigned && superAdminCount <= 1) return "The last SuperAdmin role cannot be removed.";
        return null;
    }
}
