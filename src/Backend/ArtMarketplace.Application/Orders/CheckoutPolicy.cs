namespace ArtMarketplace.Application.Orders;

public sealed record BuyerCheckoutContext(bool AccountEnabled, bool EmailConfirmed, bool PhoneConfirmed, bool HasCompleteAddress);
public sealed record BuyerCheckoutDecision(bool Allowed, string? Reason)
{
    public static BuyerCheckoutDecision Permit() => new(true, null);
    public static BuyerCheckoutDecision Deny(string reason) => new(false, reason);
}

public static class CheckoutPolicy
{
    public static BuyerCheckoutDecision CanCheckout(BuyerCheckoutContext context)
    {
        if (!context.AccountEnabled) return BuyerCheckoutDecision.Deny("The buyer account is disabled.");
        if (!context.EmailConfirmed) return BuyerCheckoutDecision.Deny("A confirmed email is required.");
        if (!context.PhoneConfirmed) return BuyerCheckoutDecision.Deny("A confirmed phone number is required.");
        if (!context.HasCompleteAddress) return BuyerCheckoutDecision.Deny("A complete shipping address is required.");
        return BuyerCheckoutDecision.Permit();
    }
}
