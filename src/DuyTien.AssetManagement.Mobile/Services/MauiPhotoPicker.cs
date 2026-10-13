using DuyTien.AssetManagement.Mobile.Client;
namespace DuyTien.AssetManagement.Mobile.Services;

public sealed class MauiPhotoPicker : IMobilePhotoPicker
{
    public async Task<PhotoSelection?> SelectAsync(PhotoSource source)
    {
        try
        {
            FileResult? file;
            if (source == PhotoSource.Camera)
            {
                if (!MediaPicker.Default.IsCaptureSupported) throw new MobileApiException(400, "Thiết bị không có camera khả dụng. Chọn ảnh thay thế.");
                var permission = await Permissions.RequestAsync<Permissions.Camera>();
                if (permission != PermissionStatus.Granted) throw new MobileApiException(403, "Quyền camera bị từ chối. Có thể cấp lại trong Cài đặt hoặc chọn ảnh.");
                file = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = "Chụp ảnh tài sản demo" });
            }
            else
            {
                // System content picker grants the selected URI; no broad library permission on modern Android.
                file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Chọn một ảnh PNG/JPEG", FileTypes = FilePickerFileType.Images });
            }
            if (file is null) return null;
            await using var stream = await file.OpenReadAsync();
            return await PhotoSelection.ReadAsync(stream);
        }
        catch (PermissionException) { throw new MobileApiException(403, "Quyền camera/thư viện bị từ chối. Giữ form; cấp quyền trong Cài đặt hoặc chọn ảnh khác."); }
        catch (FeatureNotSupportedException) { throw new MobileApiException(400, "Chức năng ảnh không được hỗ trợ trên thiết bị này."); }
        catch (OperationCanceledException) { return null; }
        catch (IOException) { throw new MobileApiException(0, "Không đọc được ảnh. Giữ form và chọn lại."); }
    }
}
