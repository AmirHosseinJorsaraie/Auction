using ArtMarketplace.Application.Authorization;

namespace ArtMarketplace.Application.Tests;

public sealed class RoleManagementPolicyTests
{
    [Fact] public void SelfPromotionToSuperAdminIsRejected(){var id=Guid.NewGuid();Assert.NotNull(RoleManagementPolicy.Rejection(id,id,"SuperAdmin",true,false,1));}
    [Fact] public void LastSuperAdminRemovalIsRejected(){Assert.NotNull(RoleManagementPolicy.Rejection(Guid.NewGuid(),Guid.NewGuid(),"SuperAdmin",false,true,1));}
    [Fact] public void AssigningAdminToAnotherUserIsAllowed(){Assert.Null(RoleManagementPolicy.Rejection(Guid.NewGuid(),Guid.NewGuid(),"Admin",true,false,1));}
}
