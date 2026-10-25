using System.ComponentModel.DataAnnotations;

namespace ArtMarketplace.Infrastructure.Identity;

public sealed class BffSessionStoreOptions
{
    public const string SectionName = "BffSession";

    [Required]
    [MinLength(32)]
    public string HandleHmacSecret { get; set; } = string.Empty;

    [Required]
    public string Surface { get; set; } = string.Empty;
}
