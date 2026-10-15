using DuyTien.AssetManagement.Application.ExternalStorage;

namespace DuyTien.AssetManagement.Server.Authentication;

public sealed record AuthFailureDescription(string ExceptionType, int? StatusCode, string FailureCode, string CorrelationId);

public static class AuthFailureDiagnostics
{
    public static AuthFailureDescription Describe(Exception exception, string correlationId)
    {
        var authentication = exception as ExternalStorageAuthenticationException;
        var code = authentication?.FailureCode switch
        {
            "invalid_grant" => "invalid_grant",
            "invalid_client" => "invalid_client",
            "unauthorized_client" => "unauthorized_client",
            "invalid_request" => "invalid_request",
            "temporarily_unavailable" => "temporarily_unavailable",
            "access_denied" => "access_denied",
            _ => "unclassified"
        };
        var status = authentication?.StatusCode ?? (exception as ExternalStorageAccessDeniedException)?.StatusCode;
        if (status is null && exception is HttpRequestException { StatusCode: { } httpStatus }) status = (int)httpStatus;
        return new(exception.GetType().Name, status, code, correlationId);
    }
}
