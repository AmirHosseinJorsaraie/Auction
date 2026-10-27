using ArtMarketplace.Domain.Modules.Administration;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Identity;

namespace ArtMarketplace.Domain.Tests;

public sealed class AdministrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 15, 0, 0, TimeSpan.Zero);
    [Fact] public void ContentUpdateUsesOptimisticVersion()
    { var actor=Guid.NewGuid();var page=new ManagedContentPage(Guid.NewGuid(),"about","درباره ما","متن اولیه",false,actor,Now);Assert.Throws<InvalidOperationException>(()=>page.Update("عنوان","متن",true,actor,2,Now));page.Update("عنوان","متن",true,actor,1,Now);Assert.True(page.IsPublished);Assert.Equal(2,page.Version); }
    [Fact] public void BannerRejectsInvalidWindow()
    { Assert.Throws<ArgumentException>(()=>new SiteBanner(Guid.NewGuid(),"پیام",Now,Now.AddMinutes(-1),true,Guid.NewGuid(),Now)); }
    [Fact] public void CategoryCanBeManagedWithoutReplacement()
    { var category=new Category(Guid.NewGuid(),"نقاشی","painting",1);category.Update("نقاشی مدرن","modern-painting",2,false);Assert.False(category.IsActive);Assert.Equal("modern-painting",category.Slug); }
    [Fact] public void ReactivatingAccountDoesNotRestoreSellerApproval()
    { var eligibility=new SellerEligibility(Guid.NewGuid());eligibility.Approve(Now.AddDays(1),Now);_ = eligibility.DisableAccount("account disabled",Now);eligibility.ReactivateAccount();Assert.True(eligibility.AccountEnabled);Assert.False(eligibility.VerificationApproved);Assert.False(eligibility.CanSellAt(Now)); }
}
