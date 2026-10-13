using DuyTien.AssetManagement.Application.Media;
namespace DuyTien.AssetManagement.ArchitectureTests;
public sealed class ManagedMediaFileValidationTests
{
    [Theory][InlineData("PNG")][InlineData("JPEG")][InlineData("DOCX")][InlineData("PDF")]
    public void Synthetic_real_format_bytes_are_accepted_with_matching_mime(string type)
    {var f=type switch{"PNG"=>MediaBytes.Png(),"JPEG"=>MediaBytes.Jpeg(),"DOCX"=>MediaBytes.Docx(),_=>MediaBytes.Pdf()};Assert.Equal(f.MimeType,MediaFileValidation.Validate(type is "PNG" or "JPEG"?"PHOTO":type,f,new()));}
    [Theory][InlineData("extension")][InlineData("mime")][InlineData("magic")][InlineData("crc")][InlineData("truncated")][InlineData("path")][InlineData("empty")][InlineData("jpeg-tail")][InlineData("pdf-tail")][InlineData("docx-external")][InlineData("docx-macro")]
    public void Spoofed_corrupt_path_and_unsafe_archive_bytes_are_rejected(string fault)
    {
        var file=MediaBytes.Png();var kind="PHOTO";
        switch(fault){case"extension":file=file with{FileName="fake.svg"};break;case"mime":file=file with{MimeType="image/jpeg"};break;case"magic":file=file with{Bytes="<html>FAKE</html>"u8.ToArray()};break;case"crc":file.Bytes[20]^=1;break;case"truncated":file=file with{Bytes=file.Bytes[..^4]};break;case"path":file=file with{FileName="../file.png"};break;case"empty":file=file with{Bytes=[]};break;case"jpeg-tail":file=MediaBytes.Jpeg() with{Bytes=MediaBytes.Jpeg().Bytes.Concat("HTML"u8.ToArray()).ToArray()};break;case"pdf-tail":kind="PDF";file=MediaBytes.Pdf() with{Bytes="%PDF-1.4 fake"u8.ToArray()};break;case"docx-external":kind="DOCX";file=MediaBytes.Docx(external:true);break;default:kind="DOCX";file=MediaBytes.Docx(macro:true);break;}
        Assert.Throws<MediaValidationException>(()=>MediaFileValidation.Validate(kind,file,new()));
    }
    [Fact]public void Configured_byte_limit_is_real_and_not_extension_based()=>Assert.Throws<MediaTooLargeException>(()=>MediaFileValidation.Validate("PHOTO",MediaBytes.Png(),new(){MaxPhotoBytes=1}));
}
