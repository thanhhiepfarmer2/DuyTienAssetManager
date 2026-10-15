namespace DuyTien.AssetManagement.Infrastructure.Google.Http;

// Enforced before credentials, pacing, transport and CLI business requests.
public interface IGoogleConnectivityGuard
{
    Task EnsureAllowedAsync(CancellationToken ct);
}
