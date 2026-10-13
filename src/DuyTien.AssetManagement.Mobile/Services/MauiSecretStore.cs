using DuyTien.AssetManagement.Mobile.Client;
namespace DuyTien.AssetManagement.Mobile.Services;

public sealed class MauiSecretStore : IMobileSecretStore
{
    private const string Key = "duytien.mobile.session.v1";
    public Task<string?> ReadAsync() => SecureStorage.Default.GetAsync(Key);
    public Task WriteAsync(string value) => SecureStorage.Default.SetAsync(Key, value);
    public Task RemoveAsync() { SecureStorage.Default.Remove(Key); return Task.CompletedTask; }
}
