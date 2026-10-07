namespace DuyTien.AssetManagement.Infrastructure.Google.Http;

public enum GoogleRequestSemantics
{
    SafeRead,
    IdempotentWrite,
    NonIdempotentWrite
}
