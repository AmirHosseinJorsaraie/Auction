using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArtMarketplace.Infrastructure.Identity;

public sealed class PostgresTicketStore : ITicketStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDataProtector _protector;
    private readonly byte[] _hmacKey;
    private readonly string _surface;

    public PostgresTicketStore(
        IServiceScopeFactory scopeFactory,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<BffSessionStoreOptions> options)
    {
        _scopeFactory = scopeFactory;
        _surface = options.Value.Surface;
        _hmacKey = Encoding.UTF8.GetBytes(options.Value.HandleHmacSecret);
        _protector = dataProtectionProvider.CreateProtector(
            "ArtMarketplace.BffSession",
            _surface,
            "v1");
    }

    public Task<string> StoreAsync(AuthenticationTicket ticket) =>
        StoreAsync(ticket, CancellationToken.None);

    public async Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        var handle = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        var session = new BffSession
        {
            Id = Guid.NewGuid(),
            Surface = _surface,
            HandleDigest = Digest(handle),
            ProtectedTicket = _protector.Protect(TicketSerializer.Default.Serialize(ticket)),
            CreatedAtUtc = now,
            ExpiresAtUtc = ticket.Properties.ExpiresUtc ?? now.AddMinutes(30),
            Version = 1
        };

        await using var scope = _scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<Persistence.MarketplaceDbContext>();
        database.BffSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
        return handle;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) =>
        RenewAsync(key, ticket, CancellationToken.None);

    public async Task RenewAsync(
        string key,
        AuthenticationTicket ticket,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<Persistence.MarketplaceDbContext>();
        var session = await FindActiveAsync(database, key, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.ProtectedTicket = _protector.Protect(TicketSerializer.Default.Serialize(ticket));
        session.ExpiresAtUtc = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddMinutes(30);
        session.Version = checked(session.Version + 1);
        await database.SaveChangesAsync(cancellationToken);
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) =>
        RetrieveAsync(key, CancellationToken.None);

    public async Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<Persistence.MarketplaceDbContext>();
        var session = await FindActiveAsync(database, key, cancellationToken);
        if (session is null)
        {
            return null;
        }

        try
        {
            return TicketSerializer.Default.Deserialize(_protector.Unprotect(session.ProtectedTicket));
        }
        catch (CryptographicException)
        {
            session.RevokedAtUtc = DateTimeOffset.UtcNow;
            session.Version = checked(session.Version + 1);
            await database.SaveChangesAsync(cancellationToken);
            return null;
        }
    }

    public Task RemoveAsync(string key) => RemoveAsync(key, CancellationToken.None);

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<Persistence.MarketplaceDbContext>();
        var digest = Digest(key);
        var session = await database.BffSessions.SingleOrDefaultAsync(
            item => item.Surface == _surface && item.HandleDigest == digest,
            cancellationToken);
        if (session is null || session.RevokedAtUtc is not null)
        {
            return;
        }

        session.RevokedAtUtc = DateTimeOffset.UtcNow;
        session.Version = checked(session.Version + 1);
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<BffSession?> FindActiveAsync(
        Persistence.MarketplaceDbContext database,
        string handle,
        CancellationToken cancellationToken)
    {
        var digest = Digest(handle);
        var now = DateTimeOffset.UtcNow;
        return await database.BffSessions.SingleOrDefaultAsync(
            item => item.Surface == _surface
                && item.HandleDigest == digest
                && item.RevokedAtUtc == null
                && item.ExpiresAtUtc > now,
            cancellationToken);
    }

    private string Digest(string handle)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(handle)));
    }
}
