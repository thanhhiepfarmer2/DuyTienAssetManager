using System.Net;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Mobile.Client;

namespace DuyTien.AssetManagement.ArchitectureTests;

// Permission/cancel tests exercise the workflow via fake picker; OS permission dialog is NOT_RUN.
public sealed class MobilePhotoTests
{
    [Theory]
    [InlineData(PhotoSource.Camera)]
    [InlineData(PhotoSource.Library)]
    public async Task Picker_cancel_keeps_existing_preview_and_metadata(PhotoSource source)
    {
        var f = new MobileFake(); var draft = new MobilePhotoDraft(new(f.Session), "ASSET-FAKE", 1) { Description = "giữ form", IsPrimary = true };
        var selected = new PhotoSelection(MediaBytes.Png().Bytes); draft.Select(selected);
        var picker = new FakeMobilePicker(() => Task.FromResult<PhotoSelection?>(null));
        Assert.False(await draft.PickAsync(picker, source)); Assert.Same(selected, draft.Photo); Assert.Equal("giữ form", draft.Description); Assert.True(draft.IsPrimary); Assert.Empty(f.Calls); Assert.Equal(source, picker.LastSource);
    }
    [Theory]
    [InlineData(PhotoSource.Camera)]
    [InlineData(PhotoSource.Library)]
    public async Task Picker_permission_denied_keeps_form_and_does_not_upload(PhotoSource source)
    {
        var f = new MobileFake(); var draft = new MobilePhotoDraft(new(f.Session), "ASSET-FAKE", 1) { Description = "giữ form" };
        draft.Select(new(MediaBytes.Png().Bytes)); var before = draft.Photo;
        var picker = new FakeMobilePicker(() => throw new MobileApiException(403, "Quyền giả bị từ chối"));
        Assert.Equal(403, (await Assert.ThrowsAsync<MobileApiException>(() => draft.PickAsync(picker, source))).Status);
        Assert.Same(before, draft.Photo); Assert.Equal("SELECTED", draft.State); Assert.Equal("giữ form", draft.Description); Assert.Empty(f.Calls);
    }
    [Fact]
    public async Task Concurrent_pick_and_upload_cannot_replace_a_pending_camera_operation()
    {
        var f = new MobileFake(); var draft = new MobilePhotoDraft(new(f.Session), "ASSET-FAKE", 1);
        var result = new TaskCompletionSource<PhotoSelection?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pick = draft.PickAsync(new FakeMobilePicker(() => result.Task), PhotoSource.Camera);
        await Assert.ThrowsAsync<MobileApiException>(() => draft.PickAsync(new FakeMobilePicker(() => Task.FromResult<PhotoSelection?>(null)), PhotoSource.Library));
        await Assert.ThrowsAsync<MobileApiException>(() => draft.UploadAsync());
        result.SetResult(new(MediaBytes.Png().Bytes)); Assert.True(await pick); Assert.Equal("SELECTED", draft.State); Assert.Empty(f.Calls);
    }
    [Fact]
    public async Task Stream_cap_format_and_immutable_photo_bytes_prevent_invalid_upload_payload()
    {
        var bytes = MediaBytes.Png().Bytes; await using var stream = new MemoryStream(bytes); var photo = await PhotoSelection.ReadAsync(stream);
        Assert.Equal("image/png", photo.MimeType); Assert.EndsWith(".png", photo.FileName); Assert.DoesNotContain("/", photo.FileName);
        bytes[0] = 0; var exposed = photo.Bytes; exposed[0] = 0; Assert.Equal(137, photo.Bytes[0]);
        Assert.Equal(400, Assert.Throws<MobileApiException>(() => new PhotoSelection([])).Status);
        Assert.Equal(400, Assert.Throws<MobileApiException>(() => new PhotoSelection(MediaBytes.Pdf().Bytes)).Status);
        await using var tooLarge = new MemoryStream(new byte[PhotoSelection.MaxBytes + 1]);
        Assert.Equal(413, (await Assert.ThrowsAsync<MobileApiException>(() => PhotoSelection.ReadAsync(tooLarge))).Status);
        Assert.Equal("image/jpeg", new PhotoSelection(MediaBytes.Jpeg().Bytes).MimeType);
    }
    [Fact]
    public async Task Non_available_upload_result_never_shows_success_and_retains_operation()
    {
        var f = new MobileFake(); await f.Login(); var draft = new MobilePhotoDraft(new(f.Session), "ASSET-FAKE", 1); draft.Select(new(MediaBytes.Png().Bytes));
        f.Handler.Respond = (_, _) => Task.FromResult(MobileFake.Json(new MediaUploadResult(new("FILE-FAKE", "ASSET", "ASSET-FAKE", "PHOTO", "fake.png", "image/png", 10, "PENDING", false, false, "", 1, "UPLOAD-FAKE"))));
        Assert.Equal(503, (await Assert.ThrowsAsync<MobileApiException>(() => draft.UploadAsync())).Status);
        Assert.Equal("RETRY", draft.State); Assert.True(draft.HasPending); Assert.Equal("FILE-FAKE", draft.FileId);
    }
    [Fact]
    public async Task Definite_validation_error_keeps_photo_but_allows_correction_and_new_request()
    {
        var f = new MobileFake(); await f.Login(); var draft = new MobilePhotoDraft(new(f.Session), "ASSET-FAKE", 1); draft.Select(new(MediaBytes.Png().Bytes));
        f.Handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge));
        Assert.Equal(413, (await Assert.ThrowsAsync<MobileApiException>(() => draft.UploadAsync())).Status);
        Assert.False(draft.HasPending); Assert.NotNull(draft.Photo); Assert.Equal("SELECTED", draft.State); draft.Select(new(MediaBytes.Jpeg().Bytes));
    }
    [Fact]
    public async Task Photo_cannot_rebase_or_replace_uncertain_upload()
    {
        var f = new MobileFake(); await f.Login(); var draft = new MobilePhotoDraft(new(f.Session), "ASSET-FAKE", 1); draft.Select(new(MediaBytes.Png().Bytes));
        f.Handler.Respond = (_, _) => throw new HttpRequestException("fake offline");
        await Assert.ThrowsAsync<MobileApiException>(() => draft.UploadAsync());
        await Assert.ThrowsAsync<MobileApiException>(() => draft.RebaseAsync());
        await Assert.ThrowsAsync<MobileApiException>(() => draft.PickAsync(new FakeMobilePicker(() => Task.FromResult<PhotoSelection?>(new(MediaBytes.Jpeg().Bytes))), PhotoSource.Library));
        Assert.True(draft.HasPending); Assert.Equal("image/png", draft.Photo!.MimeType);
    }
}
internal sealed class FakeMobilePicker(Func<Task<PhotoSelection?>> choose) : IMobilePhotoPicker
{
    public PhotoSource? LastSource { get; private set; }
    public Task<PhotoSelection?> SelectAsync(PhotoSource source) { LastSource = source; return choose(); }
}
