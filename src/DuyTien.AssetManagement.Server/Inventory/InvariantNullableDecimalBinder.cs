using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace DuyTien.AssetManagement.Server.Inventory;

// HTML input[type=number] submits invariant numeric text even when the server uses vi-VN.
public sealed class InvariantNullableDecimalBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) { context.Result = ModelBindingResult.Success(null); return Task.CompletedTask; }
        context.ModelState.SetModelValue(context.ModelName, value);
        if (string.IsNullOrWhiteSpace(value.FirstValue)) context.Result = ModelBindingResult.Success(null);
        else if (value.Length == 1 && decimal.TryParse(value.FirstValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            context.Result = ModelBindingResult.Success(parsed);
        else context.ModelState.TryAddModelError(context.ModelName, "Số thập phân không hợp lệ; không dùng ký tự phân cách hàng nghìn.");
        return Task.CompletedTask;
    }
}
