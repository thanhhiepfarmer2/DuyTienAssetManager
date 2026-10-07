using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Application.ProofOfConcept;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;

namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

public sealed class GoogleSheetsDataStore(
    GoogleWorkspaceOptions options,
    GoogleApiRequestExecutor api) :
    IAssetDataStore,
    IPocAtomicDataStore,
    IPocFileStateStore
{
    public const string PocSheetTitle = "TASK004_POC";
    private const int ColumnCount = 9;
    private static readonly string[] Header =
        ["RecordType", "Key", "Value1", "Value2", "Value3", "Value4", "State", "TimestampUtc", "Version"];

    public async Task<StorageReadiness> CheckReadinessAsync(CancellationToken cancellationToken = default)
    {
        var missingSettings = options.GetMissingSheetsSettings();
        if (missingSettings.Count > 0)
        {
            return Readiness(live: false, missingSettings);
        }

        try
        {
            var spreadsheet = await GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
            return Readiness(
                live: string.Equals(spreadsheet.SpreadsheetId, options.SpreadsheetId, StringComparison.Ordinal),
                missingSettings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ExternalStorageException)
        {
            return Readiness(live: false, missingSettings);
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var spreadsheet = await GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        var sheet = spreadsheet.Sheets
            .Select(item => item.Properties)
            .SingleOrDefault(item => string.Equals(item.Title, PocSheetTitle, StringComparison.Ordinal));

        if (sheet is null)
        {
            var body = new
            {
                requests = new object[]
                {
                    new { addSheet = new { properties = new { title = PocSheetTitle } } }
                }
            };
            using var response = await SendBatchAsync(
                body,
                GoogleRequestSemantics.NonIdempotentWrite,
                cancellationToken).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var result = await JsonSerializer.DeserializeAsync<BatchUpdateResponse>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            sheet = result?.Replies?.FirstOrDefault()?.AddSheet?.Properties ??
                throw new ExternalStorageException("Sheets did not return the created PoC sheet id.");
        }

        var rows = await ReadRowsAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            var initializeBody = new
            {
                requests = new object[]
                {
                    new
                    {
                        updateCells = new
                        {
                            start = new { sheetId = sheet.SheetId, rowIndex = 0, columnIndex = 0 },
                            rows = new[]
                            {
                                MakeRow(Header),
                                MakeRow("COUNTER", "TASK004", "0", "POC", "", "", "READY", UtcNow(), "0")
                            },
                            fields = "userEnteredValue"
                        }
                    }
                }
            };
            using var response = await SendBatchAsync(
                initializeBody,
                GoogleRequestSemantics.IdempotentWrite,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!Header.SequenceEqual(rows[0].Values.Take(Header.Length), StringComparer.Ordinal))
        {
            throw new ExternalStorageException(
                $"Sheet '{PocSheetTitle}' exists but its header is not the TASK-004 schema.");
        }

        if (!rows.Any(row => row.Kind == "COUNTER" && row.Key == "TASK004"))
        {
            await AppendRowsAsync(
                sheet.SheetId,
                [MakeRow("COUNTER", "TASK004", "0", "POC", "", "", "READY", UtcNow(), "0")],
                GoogleRequestSemantics.NonIdempotentWrite,
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<PocCounterState> GetCounterAsync(
        string counterKey,
        CancellationToken cancellationToken = default)
    {
        var row = (await ReadRowsAsync(cancellationToken).ConfigureAwait(false))
            .SingleOrDefault(item => item.Kind == "COUNTER" && item.Key == counterKey) ??
            throw new ExternalStorageException($"PoC counter '{counterKey}' was not initialized.");
        return new PocCounterState(
            row.Key,
            ParseLong(row[2], "counter value"),
            row[3],
            ParseInt(row[8], "counter version"));
    }

    public async Task<PocOperationState?> FindOperationAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var row = (await ReadRowsAsync(cancellationToken).ConfigureAwait(false))
            .SingleOrDefault(item => item.Kind == "OPERATION" && item.Key == idempotencyKey);
        return row is null
            ? null
            : new PocOperationState(
                row.Key,
                row[2],
                row[3],
                row[4],
                row[5],
                row[6],
                ParseInt(row[8], "operation counter version"));
    }

    public async Task CommitAsync(PocAtomicWrite write, CancellationToken cancellationToken = default)
    {
        var spreadsheet = await GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        var sheetId = GetPocSheetId(spreadsheet);
        var rows = await ReadRowsAsync(cancellationToken).ConfigureAwait(false);
        var existing = rows.SingleOrDefault(
            item => item.Kind == "OPERATION" && item.Key == write.IdempotencyKey);
        if (existing is not null)
        {
            if (existing[5] == write.RequestHash && existing[6] == "COMMITTED")
            {
                return;
            }

            throw new IdempotencyConflictException(
                "The idempotency key already exists with different content or state.");
        }

        var counter = rows.SingleOrDefault(
            item => item.Kind == "COUNTER" && item.Key == write.CounterKey) ??
            throw new ExternalStorageException($"PoC counter '{write.CounterKey}' was not initialized.");
        var actualVersion = ParseInt(counter[8], "counter version");
        if (actualVersion != write.ExpectedCounterVersion)
        {
            throw new VersionConflictException(
                $"Counter version conflict. Expected {write.ExpectedCounterVersion}, actual {actualVersion}.");
        }

        var timestamp = write.Timestamp.UtcDateTime.ToString("O");
        var body = new
        {
            requests = new object[]
            {
                new
                {
                    updateCells = new
                    {
                        start = new { sheetId, rowIndex = counter.RowIndex, columnIndex = 0 },
                        rows = new[]
                        {
                            MakeRow(
                                "COUNTER", write.CounterKey, write.NextCounterValue.ToString(), counter[3],
                                "", "", "READY", timestamp, (actualVersion + 1).ToString())
                        },
                        fields = "userEnteredValue"
                    }
                },
                new
                {
                    appendCells = new
                    {
                        sheetId,
                        rows = new[]
                        {
                            MakeRow(
                                "ENTITY", write.EntityId, write.ObjectCode, write.Payload,
                                write.OperationId, write.RequestHash, "ACTIVE", timestamp, "1"),
                            MakeRow(
                                "OPERATION", write.IdempotencyKey, write.OperationId, write.EntityId,
                                write.ObjectCode, write.RequestHash, "COMMITTED", timestamp,
                                (actualVersion + 1).ToString()),
                            MakeRow(
                                "AUDIT", Guid.NewGuid().ToString("N"), write.OperationId, write.EntityId,
                                "CREATE_POC", write.Actor, "COMMITTED", timestamp, write.RequestHash)
                        },
                        fields = "userEnteredValue"
                    }
                }
            }
        };

        using var response = await SendBatchAsync(
            body,
            GoogleRequestSemantics.NonIdempotentWrite,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<PocFileState> BeginFileAsync(
        PocFileState pendingFile,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindFileAsync(pendingFile.OperationId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var spreadsheet = await GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AppendRowsAsync(
                GetPocSheetId(spreadsheet),
                [MakeFileRow(pendingFile)],
                GoogleRequestSemantics.NonIdempotentWrite,
                cancellationToken).ConfigureAwait(false);
            return pendingFile;
        }
        catch (ExternalStorageTransientException exception) when (exception.CommitOutcomeUnknown)
        {
            return await FindFileAsync(pendingFile.OperationId, cancellationToken).ConfigureAwait(false) ??
                throw new ReconciliationRequiredException(
                    "The initial PENDING file state is not visible after an ambiguous Sheets response.",
                    exception);
        }
    }

    public async Task<PocFileState?> FindFileAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var row = (await ReadRowsAsync(cancellationToken).ConfigureAwait(false))
            .SingleOrDefault(item => item.Kind == "FILE" && item.Key == operationId);
        return row is null ? null : ToFileState(row);
    }

    public async Task<PocFileState> UpdateFileAsync(
        string operationId,
        PocFileStatus status,
        string? driveFileId,
        string? failureCode,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rows = await ReadRowsAsync(cancellationToken).ConfigureAwait(false);
        var row = rows.SingleOrDefault(item => item.Kind == "FILE" && item.Key == operationId) ??
            throw new ExternalStorageException($"PoC file operation '{operationId}' was not found.");
        var current = ToFileState(row);
        if (current.Version != expectedVersion)
        {
            throw new VersionConflictException(
                $"File state version conflict. Expected {expectedVersion}, actual {current.Version}.");
        }

        var updated = current with
        {
            Status = status,
            DriveFileId = driveFileId,
            FailureCode = failureCode,
            Version = current.Version + 1
        };
        var spreadsheet = await GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        var body = new
        {
            requests = new object[]
            {
                new
                {
                    updateCells = new
                    {
                        start = new
                        {
                            sheetId = GetPocSheetId(spreadsheet),
                            rowIndex = row.RowIndex,
                            columnIndex = 0
                        },
                        rows = new[] { MakeFileRow(updated) },
                        fields = "userEnteredValue"
                    }
                }
            }
        };
        using var response = await SendBatchAsync(
            body,
            GoogleRequestSemantics.IdempotentWrite,
            cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private async Task<SpreadsheetResponse> GetSpreadsheetAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var relative = $"spreadsheets/{Uri.EscapeDataString(options.SpreadsheetId!)}" +
            "?fields=spreadsheetId,properties.title,sheets.properties(sheetId,title)";
        using var response = await api.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, BuildSheetsUri(relative)),
            GoogleRequestSemantics.SafeRead,
            cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<SpreadsheetResponse>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false) ??
            throw new ExternalStorageException("Google Sheets returned an empty metadata response.");
    }

    private async Task<IReadOnlyList<PocRow>> ReadRowsAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var range = Uri.EscapeDataString($"'{PocSheetTitle}'!A:I");
        var relative = $"spreadsheets/{Uri.EscapeDataString(options.SpreadsheetId!)}/values/{range}" +
            "?majorDimension=ROWS&valueRenderOption=UNFORMATTED_VALUE";
        using var response = await api.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, BuildSheetsUri(relative)),
            GoogleRequestSemantics.SafeRead,
            cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var valueRange = await JsonSerializer.DeserializeAsync<ValueRangeResponse>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        return (valueRange?.Values ?? [])
            .Select((values, index) => new PocRow(index, Normalize(values)))
            .ToArray();
    }

    private async Task AppendRowsAsync(
        int sheetId,
        object[] rows,
        GoogleRequestSemantics semantics,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            requests = new object[]
            {
                new { appendCells = new { sheetId, rows, fields = "userEnteredValue" } }
            }
        };
        using var response = await SendBatchAsync(body, semantics, cancellationToken).ConfigureAwait(false);
    }

    private Task<HttpResponseMessage> SendBatchAsync(
        object body,
        GoogleRequestSemantics semantics,
        CancellationToken cancellationToken)
    {
        var relative = $"spreadsheets/{Uri.EscapeDataString(options.SpreadsheetId!)}:batchUpdate";
        return api.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, BuildSheetsUri(relative))
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            },
            semantics,
            cancellationToken);
    }

    private static object MakeRow(params string[] values) => new
    {
        values = values.Select(value => new
        {
            userEnteredValue = new { stringValue = value }
        }).ToArray()
    };

    private static object MakeFileRow(PocFileState state) => MakeRow(
        "FILE",
        state.OperationId,
        state.AttachmentId,
        state.DriveFileId ?? string.Empty,
        state.FileName,
        $"{state.ContentType}|{state.Sha256}",
        state.Status.ToString().ToUpperInvariant(),
        state.FailureCode ?? string.Empty,
        state.Version.ToString());

    private static PocFileState ToFileState(PocRow row)
    {
        var contentParts = row[5].Split('|', 2);
        if (contentParts.Length != 2)
        {
            throw new ExternalStorageException("PoC file metadata is malformed.");
        }

        if (!Enum.TryParse<PocFileStatus>(row[6], ignoreCase: true, out var status))
        {
            throw new ExternalStorageException("PoC file state is malformed.");
        }

        return new PocFileState(
            row.Key,
            row[2],
            row[4],
            contentParts[0],
            contentParts[1],
            status,
            string.IsNullOrWhiteSpace(row[3]) ? null : row[3],
            string.IsNullOrWhiteSpace(row[7]) ? null : row[7],
            ParseInt(row[8], "file version"));
    }

    private int GetPocSheetId(SpreadsheetResponse spreadsheet) =>
        spreadsheet.Sheets
            .Select(item => item.Properties)
            .SingleOrDefault(item => string.Equals(item.Title, PocSheetTitle, StringComparison.Ordinal))
            ?.SheetId ??
        throw new ExternalStorageException(
            $"Sheet '{PocSheetTitle}' is missing. Run the explicit TASK-004 init command first.");

    private Uri BuildSheetsUri(string relative) => new(new Uri(options.SheetsApiBaseUrl), relative);

    private void EnsureConfigured()
    {
        var missing = options.GetMissingSheetsSettings();
        if (missing.Count > 0)
        {
            throw new ExternalStorageException(
                $"Google Sheets configuration is incomplete: {string.Join(", ", missing)}.");
        }
    }

    private static StorageReadiness Readiness(
        bool live,
        IReadOnlyCollection<string> missingSettings) =>
        new(
            "Google Sheets via company operator OAuth",
            ConfigurationPresent: missingSettings.Count == 0,
            LiveConnectionVerified: live,
            MissingSettings: missingSettings);

    private static string[] Normalize(JsonElement[] values)
    {
        var normalized = Enumerable.Repeat(string.Empty, ColumnCount).ToArray();
        for (var index = 0; index < Math.Min(values.Length, ColumnCount); index++)
        {
            normalized[index] = values[index].ValueKind switch
            {
                JsonValueKind.String => values[index].GetString() ?? string.Empty,
                JsonValueKind.Number => values[index].GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => string.Empty
            };
        }

        return normalized;
    }

    private static int ParseInt(string value, string field) =>
        int.TryParse(value, out var parsed)
            ? parsed
            : throw new ExternalStorageException($"Invalid {field} in the PoC sheet.");

    private static long ParseLong(string value, string field) =>
        long.TryParse(value, out var parsed)
            ? parsed
            : throw new ExternalStorageException($"Invalid {field} in the PoC sheet.");

    private static string UtcNow() => DateTimeOffset.UtcNow.UtcDateTime.ToString("O");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record PocRow(int RowIndex, string[] Values)
    {
        public string Kind => this[0];
        public string Key => this[1];
        public string this[int index] => Values[index];
    }

    private sealed record SpreadsheetResponse(string SpreadsheetId, SheetResponse[] Sheets);
    private sealed record SheetResponse(SheetProperties Properties);
    private sealed record SheetProperties(int SheetId, string Title);
    private sealed record ValueRangeResponse(JsonElement[][] Values);
    private sealed record BatchUpdateResponse(BatchReply[]? Replies);
    private sealed record BatchReply(AddSheetReply? AddSheet);
    private sealed record AddSheetReply(SheetProperties Properties);
}
