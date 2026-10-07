using DuyTien.AssetManagement.ArchitectureTests.Fakes;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class FakeAdapterBoundaryTests
{
    [Fact]
    public async Task TestFakes_NeverClaimLiveGoogleVerification()
    {
        var sheets = await new FakeAssetDataStore().CheckReadinessAsync();
        var drive = await new FakeAssetFileStore().CheckReadinessAsync();

        Assert.False(sheets.LiveConnectionVerified);
        Assert.False(drive.LiveConnectionVerified);
    }
}
