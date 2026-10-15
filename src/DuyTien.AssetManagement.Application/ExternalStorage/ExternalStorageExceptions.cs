namespace DuyTien.AssetManagement.Application.ExternalStorage;

public class ExternalStorageException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class ExternalStorageTransientException(
    string message,
    bool commitOutcomeUnknown,
    Exception? innerException = null)
    : ExternalStorageException(message, innerException)
{
    public bool CommitOutcomeUnknown { get; } = commitOutcomeUnknown;
}

public sealed class ExternalStorageAuthenticationException(
    string message,
    Exception? innerException = null)
    : ExternalStorageException(message, innerException)
{
    public int? StatusCode { get; init; }
    // Set only to an allowlisted protocol code; never copy an arbitrary response field.
    public string FailureCode { get; init; } = "unclassified";
}

public sealed class ExternalStorageAccessDeniedException(
    string message,
    int statusCode)
    : ExternalStorageException(message)
{
    public int StatusCode { get; } = statusCode;
}
