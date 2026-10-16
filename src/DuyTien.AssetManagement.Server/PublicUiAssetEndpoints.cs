namespace DuyTien.AssetManagement.Server;

public static class PublicUiAssetEndpoints
{
    // Only build-manifest wwwroot assets. Business routes and Drive gateways never use this path.
    public static void MapPublicUiAssets(this IEndpointRouteBuilder endpoints, string? manifestPath = null)
        => endpoints.MapStaticAssets(manifestPath).ShortCircuit();
}
