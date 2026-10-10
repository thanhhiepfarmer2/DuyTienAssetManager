using DuyTien.AssetManagement.Application.ExternalStorage;

namespace DuyTien.AssetManagement.Server.Authentication;

public static class AuthStorageAvailabilityExtensions
{
    public static IApplicationBuilder UseTask005SessionAuthentication(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (ExternalStorageException exception) when (!context.Response.HasStarted)
            {
                context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("AuthStorageAvailability")
                    .LogWarning("External storage unavailable with {ExceptionType}; no request data was logged.",
                        exception.GetType().Name);
                await WriteUnavailableAsync(context);
            }
        });
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            if (context.Items.ContainsKey(OpaqueSessionAuthenticationHandler.StorageUnavailableItem))
            {
                await WriteUnavailableAsync(context);
                return;
            }

            await next();
        });
        return app;
    }

    private static async Task WriteUnavailableAsync(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.RetryAfter = "30";
        const string message = "Dịch vụ dữ liệu tạm thời chưa đáp ứng. Vui lòng chờ rồi tải lại trang.";
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            await context.Response.WriteAsJsonAsync(new { title = message, status = 503 },
                context.RequestAborted);
        }
        else
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(
                $"<!doctype html><html lang=\"vi\"><meta charset=\"utf-8\"><title>Dịch vụ tạm thời chưa đáp ứng</title><body><h1>Dịch vụ tạm thời chưa đáp ứng</h1><p>{message}</p></body></html>",
                context.RequestAborted);
        }
    }
}
