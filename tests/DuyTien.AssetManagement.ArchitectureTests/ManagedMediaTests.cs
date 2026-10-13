using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Application.Media;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class ManagedMediaTests
{
    [Fact]
    public async Task Photo_upload_primary_edit_delete_and_retention_keep_drive_content_and_owner_code()
    {
        var f=await ManagedMediaFixture.Start();var a=await f.Asset();var one=await f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),a.Version,"Ảnh giả",true,"one");var two=await f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Jpeg(),a.Version,"Ảnh giả hai",true,"two");
        Assert.Equal("AVAILABLE",one.File.State);Assert.Equal(2,f.Content.Creates);var rows=await f.Service.PhotosAsync("ADMIN",a.AssetId);Assert.Single(rows,x=>x.IsPrimary);Assert.Equal(two.File.FileId,rows.Single(x=>x.IsPrimary).FileId);
        one=await f.Service.UpdatePhotoAsync("ADMIN",one.File.FileId,new(rows.Single(x=>x.FileId==one.File.FileId).Version,"Sửa mô tả giả",true,"Giả"),"primary");Assert.True(one.File.IsPrimary);Assert.Equal(MediaBytes.Png().Bytes,(await f.Service.DownloadAsync("ADMIN",one.File.FileId)).Bytes);
        var deleted=await f.Service.DeletePhotoAsync("ADMIN",one.File.FileId,new(one.File.Version,"Giả"),"delete");Assert.True(deleted.File.IsDeleted);Assert.Equal(2,f.Content.Creates);Assert.Equal(2,f.Content.Files.Count);Assert.True((await f.Service.DeletePhotoAsync("ADMIN",one.File.FileId,new(one.File.Version,"Giả"),"delete")).Replayed);await Assert.ThrowsAsync<MediaNotFoundException>(()=>f.Service.DownloadAsync("ADMIN",one.File.FileId));Assert.Equal(a.ObjectCode,f.Owner.Store.State.Assets.Assets.Single().ObjectCode);
    }
    [Fact]
    public async Task Pending_is_durable_before_drive_and_failed_final_sheet_write_retries_same_drive_id_after_restart()
    {
        var f=await ManagedMediaFixture.Start();var a=await f.Asset();f.Store.FailAvailable=1;var error=await Assert.ThrowsAsync<MediaPendingException>(()=>f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"Giả",false,"retry"));Assert.Single(f.Store.State.Operations);Assert.Equal("PENDING",f.Store.State.Files.Single().State);Assert.Equal(1,f.Content.Creates);
        var retry=await f.NewService().UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"Giả",false,"retry");Assert.Equal(error.FileId,retry.File.FileId);Assert.Equal("AVAILABLE",retry.File.State);Assert.True(retry.Replayed);Assert.Equal(1,f.Content.Creates);Assert.Equal(2,f.Store.State.Operations.Count);MediaCatalogIntegrity.Validate(f.Store.State);
    }
    [Fact]
    public async Task Pending_sheet_failure_prevents_any_drive_create_and_drive_failure_keeps_operation_for_reconcile()
    {
        var f=await ManagedMediaFixture.Start();var a=await f.Asset();f.Store.FailPending=1;await Assert.ThrowsAsync<ExternalStorageException>(()=>f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"",false,"pending"));Assert.Equal(0,f.Content.Creates);Assert.Empty(f.Store.State.Files);
        f.Content.FailAfterCreate=true;var e=await Assert.ThrowsAsync<MediaPendingException>(()=>f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"",false,"drive"));Assert.Equal("FAILED",f.Store.State.Files.Single().State);var done=await f.Service.ReconcileAsync("ADMIN",e.FileId);Assert.Equal("AVAILABLE",done.File.State);Assert.Equal(1,f.Content.Creates);
    }
    [Fact]
    public async Task Cancel_failed_upload_preserves_operation_and_does_not_reactivate_or_delete_drive_file()
    {
        var f=await ManagedMediaFixture.Start();var a=await f.Asset();f.Content.FailAfterCreate=true;var e=await Assert.ThrowsAsync<MediaPendingException>(()=>f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"",false,"upload"));var file=f.Store.State.Files.Single();var cancel=await f.Service.CancelUploadAsync("ADMIN",e.FileId,new(file.Version,"Hủy giả"),"cancel");Assert.True(cancel.File.IsDeleted);Assert.Single(f.Content.Files);await Assert.ThrowsAsync<MediaConflictException>(()=>f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"",false,"upload"));
    }
    [Fact]
    public async Task Document_pair_is_same_version_explicit_demo_publication_and_previous_files_are_immutable()
    {
        var f=await ManagedMediaFixture.Start();var d=await f.Document();var v=await f.Version(d);await Assert.ThrowsAsync<MediaConflictException>(()=>f.Service.PublishVersionAsync("ADMIN",v.EntityId,new(1,2,DateOnly.FromDateTime(f.Owner.Base.Clock.GetUtcNow().UtcDateTime),"Giả",true),"missing"));
        var dx=await f.Service.UploadDocumentFileAsync("ADMIN",v.EntityId,"DOCX",MediaBytes.Docx(),1,"docx");Assert.Empty(await f.ViewerDocuments());await Assert.ThrowsAsync<MediaConflictException>(()=>f.Service.PublishVersionAsync("ADMIN",v.EntityId,new(2,2,DateOnly.FromDateTime(f.Owner.Base.Clock.GetUtcNow().UtcDateTime),"Giả",true),"half"));
        var pdf=await f.Service.UploadDocumentFileAsync("ADMIN",v.EntityId,"PDF",MediaBytes.Pdf(),2,"pdf");await Assert.ThrowsAsync<MediaValidationException>(()=>f.Service.PublishVersionAsync("ADMIN",v.EntityId,new(3,2,DateOnly.FromDateTime(f.Owner.Base.Clock.GetUtcNow().UtcDateTime),"Giả",false),"unconfirmed"));await f.Service.PublishVersionAsync("ADMIN",v.EntityId,new(3,2,DateOnly.FromDateTime(f.Owner.Base.Clock.GetUtcNow().UtcDateTime),"Giả",true),"publish");
        var view=await f.Service.DocumentAsync("USER",d.EntityId);Assert.Equal("DRAFT_DEMO",view.DataState);Assert.Equal(v.EntityId,view.CurrentPublishedVersionId);Assert.Equal("PUBLISHED_DEMO",view.Versions.Single().State);Assert.Equal(dx.File.FileId,view.Versions.Single().Docx!.FileId);Assert.Equal(pdf.File.FileId,view.Versions.Single().Pdf!.FileId);Assert.Equal(MediaBytes.Pdf().Bytes,(await f.Service.DownloadAsync("USER",pdf.File.FileId)).Bytes);
        await Assert.ThrowsAsync<MediaConflictException>(()=>f.Service.UploadDocumentFileAsync("ADMIN",v.EntityId,"PDF",MediaBytes.Pdf(),4,"overwrite"));var v2=await f.Service.CreateVersionAsync("ADMIN",d.EntityId,new("v2 fake",3),"version2");await f.Service.UploadDocumentFileAsync("ADMIN",v2.EntityId,"DOCX",MediaBytes.Docx("Nội dung giả v2"),1,"docx2");Assert.Single((await f.Service.DocumentAsync("USER",d.EntityId)).Versions);Assert.Equal(MediaBytes.Docx().Bytes,(await f.Service.DownloadAsync("USER",dx.File.FileId)).Bytes);Assert.Equal(3,f.Content.Creates);
    }
    [Fact]
    public async Task Future_effective_demo_version_is_hidden_until_date_and_archive_retains_files_and_retry_key()
    {
        var f=await ManagedMediaFixture.Start();var d=await f.Document();var v=await f.Version(d);await f.Service.UploadDocumentFileAsync("ADMIN",v.EntityId,"DOCX",MediaBytes.Docx(),1,"dx");await f.Service.UploadDocumentFileAsync("ADMIN",v.EntityId,"PDF",MediaBytes.Pdf(),2,"pdf");var tomorrow=DateOnly.FromDateTime(f.Owner.Base.Clock.GetUtcNow().UtcDateTime).AddDays(1);await f.Service.PublishVersionAsync("ADMIN",v.EntityId,new(3,2,tomorrow,"Giả",true),"pub");Assert.Empty(await f.ViewerDocuments());var now=await f.Service.DocumentAsync("ADMIN",d.EntityId);var request=new MediaLifecycleRequest(now.Version,"Ngừng dùng giả");await f.Service.ArchiveDocumentAsync("ADMIN",d.EntityId,request,"archive");Assert.True((await f.Service.ArchiveDocumentAsync("ADMIN",d.EntityId,request,"archive")).Replayed);Assert.Equal(2,f.Content.Files.Count);
    }
    [Fact]
    public async Task Viewer_outside_scope_cross_assignments_revoke_deleted_owner_and_unknown_file_are_denied_before_drive()
    {
        var f=await ManagedMediaFixture.Start();var a=await f.Asset();var photo=await f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"",false,"photo");await f.Owner.Base.Grant(AuthRole.Viewer,category:"CAT-OTHER");var reads=f.Content.Reads;
        await Assert.ThrowsAsync<MediaNotFoundException>(()=>f.Service.DownloadAsync("USER",photo.File.FileId));Assert.Equal(reads,f.Content.Reads);await Assert.ThrowsAsync<MediaForbiddenException>(()=>f.Service.UploadPhotoAsync("USER",a.AssetId,MediaBytes.Png(),1,"",false,"x"));
        await f.Owner.Base.Grant(AuthRole.AssetEditor,"OU-B","LOC-B","CAT-CHILD");await f.Owner.Base.Grant(AuthRole.DocumentManager,"ALL","ALL","ALL");await Assert.ThrowsAsync<MediaNotFoundException>(()=>f.Service.UploadPhotoAsync("USER",a.AssetId,MediaBytes.Png(),1,"",false,"wrong"));
        await f.Owner.Base.Grant(AuthRole.AssetEditor);await f.Owner.Base.Auth.MutateAsync(s=>{s.RoleAssignments.RemoveAll(x=>x.UserId=="USER");return true;});await Assert.ThrowsAsync<MediaForbiddenException>(()=>f.Service.UploadPhotoAsync("USER",a.AssetId,MediaBytes.Png(),1,"",false,"revoked"));
        await f.Owner.AssetService.DeleteAsync("ADMIN",a.AssetId,new(1,"Giả"),"asset-del");await Assert.ThrowsAsync<MediaNotFoundException>(()=>f.Service.DownloadAsync("ADMIN",photo.File.FileId));Assert.Single(f.Content.Files);await Assert.ThrowsAsync<MediaNotFoundException>(()=>f.Service.DownloadAsync("ADMIN","UNKNOWN"));
    }
    [Fact]
    public async Task Latest_effective_published_pair_uses_durable_journal_order_when_timestamps_are_equal()
    {
        var f=await ManagedMediaFixture.Start();var d=await f.Document();var one=await f.Version(d);var day=DateOnly.FromDateTime(f.Owner.Base.Clock.GetUtcNow().UtcDateTime);await f.Service.UploadDocumentFileAsync("ADMIN",one.EntityId,"DOCX",MediaBytes.Docx(),1,"dx1");await f.Service.UploadDocumentFileAsync("ADMIN",one.EntityId,"PDF",MediaBytes.Pdf(),2,"pdf1");await f.Service.PublishVersionAsync("ADMIN",one.EntityId,new(3,2,day,"Giả",true),"pub1");
        var two=await f.Service.CreateVersionAsync("ADMIN",d.EntityId,new("v2",3),"v2");var dx=await f.Service.UploadDocumentFileAsync("ADMIN",two.EntityId,"DOCX",MediaBytes.Docx("v2 giả"),1,"dx2");await f.Service.UploadDocumentFileAsync("ADMIN",two.EntityId,"PDF",MediaBytes.Pdf(),2,"pdf2");await f.Service.PublishVersionAsync("ADMIN",two.EntityId,new(3,4,day,"Giả",true),"pub2");await f.ViewerDocuments();
        f.Store.State.Versions.Reverse();var view=await f.NewService().DocumentAsync("USER",d.EntityId);Assert.Equal(two.EntityId,view.CurrentPublishedVersionId);Assert.Equal(dx.File.FileId,view.Versions.Single(v=>v.VersionId==view.CurrentPublishedVersionId).Docx!.FileId);Assert.Equal(f.Store.State.Versions[0].PublishedAt,f.Store.State.Versions[1].PublishedAt);
    }
    [Fact]
    public async Task Concurrent_same_upload_key_is_one_file_and_primary_switch_is_atomic()
    {
        var f=await ManagedMediaFixture.Start();var a=await f.Asset();var results=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"Giả",true,"same")));Assert.Single(results.Select(x=>x.File.FileId).Distinct());Assert.Equal(1,f.Content.Creates);Assert.Equal(2,f.Store.State.Operations.Count);Assert.Equal(7,results.Count(x=>x.Replayed));
        await Task.WhenAll(f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"Giả",true,"new-one"),f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Jpeg(),1,"Giả",true,"new-two"));Assert.Single(f.Store.State.Files,f=>f.IsPrimary&&f.State=="AVAILABLE"&&!f.IsDeleted);MediaCatalogIntegrity.Validate(f.Store.State);
    }
    [Fact]
    public async Task Versions_stale_keys_different_payload_and_invalid_metadata_never_overwrite()
    {
        var f=await ManagedMediaFixture.Start();var a=await f.Asset();await Assert.ThrowsAsync<MediaConflictException>(()=>f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),0,"",false,"stale"));Assert.Equal(0,f.Content.Creates);var p=await f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"",false,"photo");await Assert.ThrowsAsync<MediaConflictException>(()=>f.Service.UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Jpeg(),1,"",false,"photo"));await Assert.ThrowsAsync<MediaConflictException>(()=>f.Service.UpdatePhotoAsync("ADMIN",p.File.FileId,new(1,"",true,"Giả"),"old"));
        await Assert.ThrowsAsync<MediaValidationException>(()=>f.Service.CreateDocumentAsync("ADMIN",new(new("","","CAT-MACHINE"),0),"bad"));var d=await f.Document();await Assert.ThrowsAsync<MediaConflictException>(()=>f.Service.CreateVersionAsync("ADMIN",d.EntityId,new("v1",0),"stale-v"));await f.Version(d);await Assert.ThrowsAsync<MediaConflictException>(()=>f.Service.CreateVersionAsync("ADMIN",d.EntityId,new("v1 fake",2),"duplicate"));
    }
}
internal sealed class ManagedMediaFixture
{
    public TreeMutationFixture Owner{get;}public ManagedMediaMemoryStore Store{get;}=new();public ManagedMediaMemoryContent Content{get;}=new();public MediaLibraryService Service{get;}
    private ManagedMediaFixture(TreeMutationFixture owner){Owner=owner;Service=NewService();}
    public static async Task<ManagedMediaFixture> Start()=>new(await TreeMutationFixture.Start());
    public MediaLibraryService NewService()=>new(Store,Content,Owner.Store.AssetAdapter,Owner.Store.CategoryAdapter,Owner.Base.Auth,Owner.Writer,Owner.Base.Clock,new());
    public async Task<DuyTien.AssetManagement.Application.Assets.AssetRecord> Asset()=>(await Owner.AssetService.CreateAsync("ADMIN",Owner.AssetRequest(),"fixture-asset")).Asset;
    public Task<MediaMutationResult> Document()=>Service.CreateDocumentAsync("ADMIN",new(new("Mẫu giả TASK009","FAKE.BM009","CAT-CHILD","Không phải mẫu công ty"),0),"doc");
    public Task<MediaMutationResult> Version(MediaMutationResult doc)=>Service.CreateVersionAsync("ADMIN",doc.EntityId,new("v1 fake",doc.Version),"version");
    public async Task<IReadOnlyList<DocumentView>> ViewerDocuments(){if(!await Owner.Base.Auth.ReadAsync(s=>s.RoleAssignments.Any(a=>a.UserId=="USER"&&a.RoleCode==AuthRole.Viewer)))await Owner.Base.Grant(AuthRole.Viewer);return await Service.DocumentsAsync("USER");}
}
internal sealed class ManagedMediaMemoryStore:IMediaCatalogStore
{
    public MediaCatalogState State{get;private set;}=new();public int FailAvailable{get;set;}public int FailPending{get;set;}public bool FailRead{get;set;}
    public Task<MediaCatalogState> ReadAsync(CancellationToken ct=default)=>FailRead?throw new ExternalStorageException("PRIVATE-MEDIA-CANARY"):Task.FromResult(State.Clone());
    public Task CommitAsync(MediaCatalogState candidate,long expectedRevision,CancellationToken ct=default){if(State.Revision!=expectedRevision)throw new MediaConflictException("Fake revision");MediaCatalogIntegrity.Validate(candidate);var action=candidate.Operations.Last().Action;if(action=="UPLOAD_AVAILABLE"&&FailAvailable>0){FailAvailable--;throw new ExternalStorageException("PRIVATE-MEDIA-CANARY");}if(action=="UPLOAD_PENDING"&&FailPending>0){FailPending--;throw new ExternalStorageException("PRIVATE-MEDIA-CANARY");}State=candidate.Clone();return Task.CompletedTask;}
}
internal sealed class ManagedMediaMemoryContent:IManagedMediaContentStore
{
    private int reserved;public int Creates{get;private set;}public int Reads{get;private set;}public bool FailAfterCreate{get;set;}public Dictionary<string,byte[]> Files{get;}=[];
    public Task<string> AllocateIdAsync(CancellationToken ct=default)=>Task.FromResult("DRIVE-FAKE-"+ ++reserved);
    public Task EnsureUploadedAsync(ManagedFile f,ReadOnlyMemory<byte> bytes,CancellationToken ct=default){if(!Files.ContainsKey(f.DriveFileId)){Files.Add(f.DriveFileId,bytes.ToArray());Creates++;}if(FailAfterCreate){FailAfterCreate=false;throw new ExternalStorageTransientException("Synthetic lost upload response",true);}return Task.CompletedTask;}
    public Task<byte[]> DownloadVerifiedAsync(ManagedFile f,CancellationToken ct=default){Reads++;if(!Files.TryGetValue(f.DriveFileId,out var data)||Convert.ToHexString(SHA256.HashData(data))!=f.Sha256)throw new ExternalStorageException("Synthetic absent/corrupt media");return Task.FromResult(data.ToArray());}
}
internal static class MediaBytes
{
    public static MediaFileContent Png()
    {
        using var s=new MemoryStream();s.Write([137,80,78,71,13,10,26,10]);var header=new byte[13];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0,4),1);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4,4),1);header[8]=8;header[9]=6;Chunk(s,"IHDR",header);using var packed=new MemoryStream();using(var z=new ZLibStream(packed,CompressionLevel.SmallestSize,true))z.Write([0,255,0,0,255]);Chunk(s,"IDAT",packed.ToArray());Chunk(s,"IEND",[]);return new("fixture-gia.png","image/png",s.ToArray());
    }
    private static void Chunk(Stream s,string type,byte[] data){var count=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(count,data.Length);s.Write(count);var body=Encoding.ASCII.GetBytes(type).Concat(data).ToArray();s.Write(body);uint crc=uint.MaxValue;foreach(var b in body){crc^=b;for(var j=0;j<8;j++)crc=(crc>>1)^((crc&1)==0?0:0xedb88320u);}System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(count,~crc);s.Write(count);}
    public static MediaFileContent Jpeg()
    {
        var b=new List<byte>{255,216};void Segment(byte marker,byte[] data){b.AddRange([255,marker,(byte)((data.Length+2)>>8),(byte)(data.Length+2)]);b.AddRange(data);}Segment(219,new byte[]{0}.Concat(Enumerable.Repeat((byte)1,64)).ToArray());Segment(192,[8,0,1,0,1,1,1,17,0]);Segment(196,new byte[]{0,1}.Concat(new byte[15]).Concat(new byte[]{0}).ToArray());Segment(196,new byte[]{16,1}.Concat(new byte[15]).Concat(new byte[]{0}).ToArray());Segment(218,[1,1,0,0,63,0]);b.AddRange([63,255,217]);return new("fixture-gia.jpg","image/jpeg",b.ToArray());
    }
    public static MediaFileContent Docx(string label="Fixture giả TASK009",bool external=false,bool macro=false)
    {
        using var s=new MemoryStream();using(var z=new ZipArchive(s,ZipArchiveMode.Create,true))
        {void Add(string path,string value){var e=z.CreateEntry(path);e.LastWriteTime=new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero);using var w=new StreamWriter(e.Open(),new UTF8Encoding(false));w.Write(value);}Add("[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");Add("_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"r1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\" "+(external?"TargetMode=\"External\"":"")+"/></Relationships>");Add("word/document.xml","<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>"+System.Security.SecurityElement.Escape(label)+"</w:t></w:r></w:p></w:body></w:document>");if(macro)Add("word/vbaProject.bin","fake-macro");}return new("fixture-gia.docx",MediaFileValidation.DocxMime,s.ToArray());
    }
    public static MediaFileContent Pdf()=>new("fixture-gia.pdf","application/pdf",Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >> endobj\nxref\n0 1\n0000000000 65535 f \ntrailer << /Root 1 0 R /Size 4 >>\nstartxref\n191\n%%EOF\n"));
}
