using DuyTien.AssetManagement.Application.Media;

namespace DuyTien.AssetManagement.Mobile.Client;

public enum PhotoSource { Camera, Library }
public interface IMobilePhotoPicker { Task<PhotoSelection?> SelectAsync(PhotoSource source); }
public sealed class PhotoSelection
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public string FileName { get; }
    public string MimeType { get; }
    private readonly byte[] bytes;
    public byte[] Bytes => bytes.ToArray(); // No caller can mutate the frozen upload payload.
    public PhotoSelection(byte[] bytes)
    {
        MimeType = Validate(bytes); this.bytes = bytes.ToArray();
        // Do not leak device paths, accept dangerous filenames, or mismatch extension and bytes.
        FileName = "photo-" + Guid.NewGuid().ToString("N") + (MimeType == "image/png" ? ".png" : ".jpg");
    }
    public static string Validate(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new MobileApiException(413, "Ảnh vượt10MiB. Chọn ảnh nhỏ hơn.");
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return "image/jpeg";
        throw new MobileApiException(400, "Chỉ nhận ảnh PNG/JPEG. Backend tiếp tục kiểm tra file.");
    }
    public static async Task<PhotoSelection> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream(); var chunk = new byte[81920]; int n;
        while ((n = await stream.ReadAsync(chunk, ct)) > 0)
        { if (buffer.Length + n > MaxBytes) throw new MobileApiException(413, "Ảnh vượt10MiB. Chọn ảnh nhỏ hơn."); buffer.Write(chunk, 0, n); }
        return new(buffer.ToArray());
    }
}
public sealed class MobilePhotoDraft(MobileAssets api, string assetId, long version)
{
    public PhotoSelection? Photo { get; private set; }
    public string Description { get; set; } = "";
    public bool IsPrimary { get; set; }
    public long ExpectedVersion { get; private set; } = version;
    public string State { get; private set; } = "EMPTY";
    public string? FileId { get; private set; }
    public bool HasPending => key is not null;
    public int? LastError { get; private set; }
    private string? key;
    private string frozenDescription = "";
    private bool frozenPrimary;
    private bool busy;
    public void Select(PhotoSelection photo)
    {
        if (HasPending || busy) throw new MobileApiException(409, "Upload chưa xác nhận. Retry/đối soát ảnh đang giữ trước khi chọn ảnh khác.");
        Photo = photo; State = "SELECTED"; FileId = null; LastError = null;
    }
    public async Task<bool> PickAsync(IMobilePhotoPicker picker, PhotoSource source)
    {
        if (busy || HasPending) throw new MobileApiException(409, "Giữ ảnh của yêu cầu upload đang chờ.");
        busy = true;
        try
        {
            var photo = await picker.SelectAsync(source); if (photo is null) return false;
            Photo = photo; State = "SELECTED"; FileId = null; LastError = null; return true;
        }
        finally { busy = false; }
    }
    public async Task<MediaUploadResult> UploadAsync(CancellationToken ct = default)
    {
        if (busy) throw new MobileApiException(409, "Đang upload; chờ kết quả.");
        if (Photo is null) throw new MobileApiException(400, "Chụp hoặc chọn ảnh trước upload.");
        if (State == "AVAILABLE") throw new MobileApiException(409, "Ảnh đã lưu; không gửi thêm lần nữa.");
        if (key is null) { key = Guid.NewGuid().ToString("N"); frozenDescription = Description; frozenPrimary = IsPrimary; }
        else if (Description != frozenDescription || IsPrimary != frozenPrimary)
            throw new MobileApiException(409, "Metadata cũ chưa xác nhận. Retry cùng mô tả/ảnh chính đã gửi.");
        busy = true; State = "UPLOADING";
        try { return Accept(await api.UploadAsync(assetId, Photo, ExpectedVersion, frozenDescription, frozenPrimary, key, ct)); }
        catch (MobileApiException e)
        {
            FileId ??= e.FileId; LastError = e.Status; State = e.Status == 409 ? "CONFLICT" : "RETRY";
            if (e.Status is 400 or 413 or 422) { key = null; State = "SELECTED"; }
            throw;
        }
        finally { busy = false; }
    }
    public async Task<MediaUploadResult> ReconcileAsync(CancellationToken ct = default)
    {
        if (busy || FileId is null) throw new MobileApiException(409, "Chưa có fileId để đối soát; retry cùng upload.");
        busy = true; State = "VERIFYING";
        try { return Accept(await api.ReconcileAsync(FileId, ct)); }
        catch { State = "RETRY"; throw; }
        finally { busy = false; }
    }
    public async Task RebaseAsync(CancellationToken ct = default)
    {
        if (busy || LastError != 409 || FileId is not null)
            throw new MobileApiException(409, "Chỉ nạp owner version mới sau409 chưa đăng ký file; yêu cầu chưa xác nhận phải retry/đối soát.");
        busy = true;
        try
        {
            var fresh = await api.DetailAsync(assetId, ct);
            ExpectedVersion = fresh.Asset.Version; key = null; LastError = null; State = "SELECTED";
        }
        finally { busy = false; }
    }
    private MediaUploadResult Accept(MediaUploadResult result)
    {
        if (result.File is null || result.File.OwnerType != "ASSET" || result.File.OwnerId != assetId || result.File.Kind != "PHOTO" || string.IsNullOrEmpty(result.File.FileId))
        { State = "RETRY"; throw new MobileApiException(503, "Phản hồi upload không đúng tài sản/contract; chưa xác nhận lưu."); }
        FileId = result.File.FileId;
        if (result.File.State != "AVAILABLE" || result.File.IsDeleted)
        { State = "RETRY"; throw new MobileApiException(503, "Ảnh chưa AVAILABLE, chưa xác nhận lưu thành công.", FileId); }
        State = "AVAILABLE"; key = null; LastError = null; return result;
    }
}
