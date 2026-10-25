using Microsoft.AspNetCore.Identity;

namespace ArtMarketplace.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public bool IsEnabled { get; set; } = true;

    public long AuthorizationVersion { get; set; } = 1;
}
