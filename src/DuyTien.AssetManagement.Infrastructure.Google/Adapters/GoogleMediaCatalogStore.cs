using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;
public sealed class GoogleMediaCatalogStore(GoogleWorkspaceOptions options,GoogleApiRequestExecutor api):IMediaCatalogStore
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    private static readonly JsonSerializerOptions ResponseJson=new(JsonSerializerDefaults.Web);
    public static readonly IReadOnlyDictionary<string,string[]> Headers=new Dictionary<string,string[]>(StringComparer.Ordinal)
    { ["MediaSchemaVersions"]=["SchemaVersion","Revision"],["Files"]=["FileId","RecordJson"],["Documents"]=["DocumentId","RecordJson"],["DocumentVersions"]=["VersionId","RecordJson"],["MediaOperations"]=["OperationId","RecordJson"],["MediaAuditEvents"]=["OperationId","Actor","Action","Reason","Timestamp"] };
    public async Task<MediaCatalogState> ReadAsync(CancellationToken ct=default)=>await Load(await Metadata(ct),ct);
    public async Task CommitAsync(MediaCatalogState candidate,long expectedRevision,CancellationToken ct=default)
    {
        var meta=await Metadata(ct);var previous=await Load(meta,ct);if(previous.Revision!=expectedRevision||candidate.Revision!=expectedRevision+1)throw new MediaConflictException("Registry revision đã đổi.");
        if(candidate.Operations.Count!=previous.Operations.Count+1||JsonSerializer.Serialize(previous.Operations,Json)!=JsonSerializer.Serialize(candidate.Operations.Take(previous.Operations.Count),Json))throw new MediaConflictException("Không ghi đè journal file.");MediaCatalogIntegrity.Validate(candidate);
        var tables=Tables(candidate);var used=meta.Sheets.Select(s=>s.Properties.SheetId).ToHashSet();var requests=new List<object>();
        foreach(var pair in tables)
        {
            var rows=pair.Value;if(rows.Count>5000||rows.Any(row=>row.Any(v=>v.Length>45000)))throw new MediaValidationException("Registry vượt giới hạn5000rows/45000chars-cell; chưa ghi.");var existing=meta.Sheets.SingleOrDefault(s=>s.Properties.Title==pair.Key)?.Properties;var id=existing?.SheetId??NextId(used);
            if(existing is null)requests.Add(new{addSheet=new{properties=new{sheetId=id,title=pair.Key,gridProperties=new{rowCount=Math.Max(100,rows.Count),columnCount=Headers[pair.Key].Length}}}});
            else {if(existing.GridProperties.ColumnCount<Headers[pair.Key].Length)throw new ExternalStorageException("Registry schema thiếu cột; không overwrite.");if(existing.GridProperties.RowCount<rows.Count)requests.Add(new{appendDimension=new{sheetId=id,dimension="ROWS",length=rows.Count-existing.GridProperties.RowCount}});}
            var start=pair.Key is "MediaOperations" or "MediaAuditEvents"&&previous.Revision>0?(int)previous.Revision+1:0;var write=rows.Skip(start).ToArray();
            if(write.Length>0)requests.Add(new{updateCells=new{start=new{sheetId=id,rowIndex=start,columnIndex=0},rows=write.Select(row=>new{values=row.Select(v=>new{userEnteredValue=new{stringValue=v}}).ToArray()}).ToArray(),fields="userEnteredValue"}});
        }
        var body=JsonSerializer.SerializeToUtf8Bytes(new{requests},ResponseJson);if(body.Length>1800*1024)throw new MediaValidationException("Registry atomic batch vượt1800KiB, chưa ghi.");
        try{using var response=await api.SendAsync(()=>new HttpRequestMessage(HttpMethod.Post,UriFor(":batchUpdate")){Content=new ByteArrayContent(body){Headers={ContentType=new("application/json")}}},GoogleRequestSemantics.NonIdempotentWrite,ct);}
        catch(ExternalStorageException){var actual=await ReadAsync(ct);if(actual.Revision==candidate.Revision&&JsonSerializer.Serialize(actual.Operations.LastOrDefault(),Json)==JsonSerializer.Serialize(candidate.Operations.Last(),Json))return;throw;}
    }
    private async Task<MediaCatalogState> Load(Spreadsheet meta,CancellationToken ct)
    {
        var present=Headers.Keys.Where(k=>meta.Sheets.Any(s=>s.Properties.Title==k)).ToArray();if(present.Length==0)return new();if(present.Length!=Headers.Count)throw new ExternalStorageException("Partial file registry schema; không overwrite.");var query=string.Join('&',present.Select(t=>"ranges="+Uri.EscapeDataString("'"+t+"'!A1:AZ5001")));
        using var response=await api.SendAsync(()=>new HttpRequestMessage(HttpMethod.Get,UriFor("/values:batchGet?"+query+"&valueRenderOption=UNFORMATTED_VALUE")),GoogleRequestSemantics.SafeRead,ct);var values=await ReadJson<Batch>(response,ct);if(values.ValueRanges?.Length!=present.Length)throw new ExternalStorageException("Registry trả thiếu bảng.");var tables=new Dictionary<string,string[][]>(StringComparer.Ordinal);
        for(var i=0;i<present.Length;i++)
        {var title=present[i];var range=values.ValueRanges[i];if(range?.Range is null||!range.Range.StartsWith("'"+title+"'!",StringComparison.Ordinal)&&!range.Range.StartsWith(title+"!",StringComparison.Ordinal)||range.Values is null||range.Values.Any(row=>row is null))throw new ExternalStorageException("Registry range/null rows không hợp lệ.");var rows=range.Values.Select(row=>row.Select(v=>v.ValueKind==JsonValueKind.String?v.GetString()!:v.GetRawText()).ToArray()).ToArray();if(rows.Length is <1 or >5000||!rows[0].SequenceEqual(Headers[title])||rows.Skip(1).Any(row=>row.Length!=Headers[title].Length||row.Any(v=>v.Length>45000)))throw new ExternalStorageException("Registry header/cell/size không hợp lệ.");tables[title]=rows.Skip(1).ToArray();}
        try
        {
            var marker=tables["MediaSchemaVersions"].Single();if(marker[0]!="MEDIA_V1")throw new ExternalStorageException("Registry schema chưa hỗ trợ.");var s=new MediaCatalogState{Revision=long.Parse(marker[1],CultureInfo.InvariantCulture)};
            foreach(var row in tables["Files"]){var f=JsonSerializer.Deserialize<ManagedFile>(row[1],Json)??throw new MediaValidationException("File row null.");if(f.FileId!=row[0])throw new MediaValidationException("File identity mismatch.");s.Files.Add(f);}
            foreach(var row in tables["Documents"]){var d=JsonSerializer.Deserialize<LibraryDocument>(row[1],Json)??throw new MediaValidationException("Document row null.");if(d.DocumentId!=row[0])throw new MediaValidationException("Document identity mismatch.");s.Documents.Add(d);}
            foreach(var row in tables["DocumentVersions"]){var v=JsonSerializer.Deserialize<LibraryDocumentVersion>(row[1],Json)??throw new MediaValidationException("Version row null.");if(v.VersionId!=row[0])throw new MediaValidationException("Version identity mismatch.");s.Versions.Add(v);}
            foreach(var row in tables["MediaOperations"]){var op=JsonSerializer.Deserialize<MediaOperation>(row[1],Json)??throw new MediaValidationException("Operation row null.");if(op.OperationId!=row[0])throw new MediaValidationException("Operation identity mismatch.");s.Operations.Add(op);}
            MediaCatalogIntegrity.Validate(s);var expected=Tables(s);
            foreach(var t in tables){var actual=t.Value.Select(row=>string.Join("\0",row));var wanted=expected[t.Key].Skip(1).Select(row=>string.Join("\0",row));if(t.Key is "Files" or "Documents" or "DocumentVersions"){actual=actual.Order(StringComparer.Ordinal);wanted=wanted.Order(StringComparer.Ordinal);}if(!actual.SequenceEqual(wanted))throw new ExternalStorageException("Registry journal/audit/materialized không khớp, không overwrite.");}return s;
        }
        catch(Exception e)when(e is JsonException or FormatException or OverflowException or InvalidOperationException or ArgumentException or MediaValidationException){throw new ExternalStorageException("Registry dữ liệu không hợp lệ; cần đối soát.");}
    }
    private static Dictionary<string,List<string[]>> Tables(MediaCatalogState s)
    {var t=Headers.ToDictionary(p=>p.Key,p=>new List<string[]>{p.Value},StringComparer.Ordinal);t["MediaSchemaVersions"].Add(["MEDIA_V1",s.Revision.ToString(CultureInfo.InvariantCulture)]);t["Files"].AddRange(s.Files.OrderBy(f=>f.FileId,StringComparer.Ordinal).Select(f=>new[]{f.FileId,JsonSerializer.Serialize(f,Json)}));t["Documents"].AddRange(s.Documents.OrderBy(d=>d.DocumentId,StringComparer.Ordinal).Select(d=>new[]{d.DocumentId,JsonSerializer.Serialize(d,Json)}));t["DocumentVersions"].AddRange(s.Versions.OrderBy(v=>v.VersionId,StringComparer.Ordinal).Select(v=>new[]{v.VersionId,JsonSerializer.Serialize(v,Json)}));t["MediaOperations"].AddRange(s.Operations.Select(o=>new[]{o.OperationId,JsonSerializer.Serialize(o,Json)}));t["MediaAuditEvents"].AddRange(s.Operations.Select(o=>new[]{o.OperationId,o.Actor,o.Action,o.Reason,o.CompletedAt.ToString("O")}));return t;}
    private async Task<Spreadsheet> Metadata(CancellationToken ct)
    {if(options.GetMissingSheetsSettings().Count>0||string.IsNullOrWhiteSpace(options.SystemSpreadsheetId)||options.SystemSpreadsheetId==options.SpreadsheetId)throw new ExternalStorageException("Registry business spreadsheet phải khác system và đủ configuration.");using var response=await api.SendAsync(()=>new HttpRequestMessage(HttpMethod.Get,UriFor("?fields=sheets.properties(sheetId,title,gridProperties)")),GoogleRequestSemantics.SafeRead,ct);var value=await ReadJson<Spreadsheet>(response,ct);if(value.Sheets is null||value.Sheets.Any(s=>s?.Properties?.GridProperties is null||string.IsNullOrWhiteSpace(s.Properties.Title))||value.Sheets.Select(s=>s.Properties.Title).Distinct(StringComparer.Ordinal).Count()!=value.Sheets.Length)throw new ExternalStorageException("Registry metadata không hợp lệ.");return value;}
    private static async Task<T> ReadJson<T>(HttpResponseMessage response,CancellationToken ct)
    {try{return await response.Content.ReadFromJsonAsync<T>(ResponseJson,ct)??throw new ExternalStorageException("Registry response null.");}catch(JsonException){throw new ExternalStorageException("Registry JSON không hợp lệ.");}catch(IOException e){throw new ExternalStorageTransientException("Registry response lỗi đọc.",false,e);}catch(HttpRequestException e){throw new ExternalStorageTransientException("Registry response lỗi đọc.",false,e);}catch(OperationCanceledException e)when(!ct.IsCancellationRequested){throw new ExternalStorageTransientException("Registry response timeout.",false,e);}}
    private Uri UriFor(string suffix)=>new(new Uri(options.SheetsApiBaseUrl),"spreadsheets/"+Uri.EscapeDataString(options.SpreadsheetId!)+suffix);
    private static int NextId(HashSet<int> ids){var id=10000;while(!ids.Add(id))id++;return id;}
    private sealed record Spreadsheet(Sheet[] Sheets);private sealed record Sheet(Properties Properties);private sealed record Properties(int SheetId,string Title,Grid GridProperties);private sealed record Grid(int RowCount,int ColumnCount);private sealed record Batch(ValueRange[] ValueRanges);private sealed record ValueRange(string Range,JsonElement[][]? Values);
}
