namespace ArtMarketplace.Contracts.System;

public sealed record ServiceStatusResponse(string Service, string Status, DateTimeOffset TimestampUtc);
