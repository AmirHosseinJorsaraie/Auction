using System.ComponentModel.DataAnnotations;

namespace ArtMarketplace.Infrastructure.Payments;

public sealed class PaymentProviderOptions
{
    public const string SectionName = "Payments";
    [Required] public string Provider { get; set; } = "Fake";
    [Required] public string MerchantAccount { get; set; } = "development-merchant";
    [Required, MinLength(32)] public string CallbackSecret { get; set; } = "development-only-callback-secret-change-me";
    [Range(30, 900)] public int CallbackFreshnessSeconds { get; set; } = 300;
    [Required] public string PublicBaseUrl { get; set; } = "http://localhost:3000";
    public bool OwnershipTransferEnabled { get; set; }
}
