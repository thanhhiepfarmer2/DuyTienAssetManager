namespace DuyTien.AssetManagement.Infrastructure.Google.OAuth;

public interface IGoogleAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    void Invalidate();
}
