using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
namespace DuyTien.AssetManagement.Application.Media;

public static class MediaFileValidation
{
    public const string DocxMime="application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public static string Validate(string kind,MediaFileContent input,MediaUploadOptions limits)
    {
        limits.Validate();if(input is null || input.Bytes is null)throw new MediaValidationException("Thiếu file.");
        var cap=kind=="PHOTO"?limits.MaxPhotoBytes:limits.MaxDocumentBytes;
        if(input.Bytes.Length>cap)throw new MediaTooLargeException();if(input.Bytes.Length==0)throw new MediaValidationException("File rỗng.");
        if(string.IsNullOrWhiteSpace(input.FileName)||input.FileName.Length>180||input.FileName.Any(c=>char.IsControl(c)||"/\\:\"<>|?*".Contains(c)))throw new MediaValidationException("Tên file không hợp lệ, không nhận đường dẫn.");
        var extension=Path.GetExtension(input.FileName).ToLowerInvariant();string mime;
        try
        {
            if(kind=="PHOTO" && extension==".png"){Png(input.Bytes);mime="image/png";}
            else if(kind=="PHOTO" && extension is ".jpg" or ".jpeg"){Jpeg(input.Bytes);mime="image/jpeg";}
            else if(kind=="DOCX" && extension==".docx"){Docx(input.Bytes);mime=DocxMime;}
            else if(kind=="PDF" && extension==".pdf"){Pdf(input.Bytes);mime="application/pdf";}
            else throw new MediaValidationException("Chỉ nhận PNG/JPEG cho ảnh, DOCX hoặc PDF đúng loại của slot.");
        }
        catch(Exception e) when(e is InvalidDataException or IOException or XmlException or ArgumentException or OverflowException){throw new MediaValidationException("Nội dung file bị lỗi hoặc format không được hỗ trợ.");}
        if(!string.Equals(input.MimeType,mime,StringComparison.OrdinalIgnoreCase) && input.MimeType!="application/octet-stream")throw new MediaValidationException("MIME không khớp nội dung/loại file.");
        return mime;
    }
    private static void Png(byte[] bytes)
    {
        ReadOnlySpan<byte> b=bytes;if(b.Length<45||!b[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))Bad();
        var offset=8;long rowBytes=0;uint height=0;bool header=false,end=false,palette=false,needsPalette=false;using var packed=new MemoryStream();
        while(offset+12<=b.Length)
        {
            var length=checked((int)BinaryPrimitives.ReadUInt32BigEndian(b.Slice(offset,4)));if(length>b.Length-offset-12)Bad();var type=Encoding.ASCII.GetString(b.Slice(offset+4,4));var data=b.Slice(offset+8,length);
            if(Crc(b.Slice(offset+4,length+4))!=BinaryPrimitives.ReadUInt32BigEndian(b.Slice(offset+8+length,4)))Bad();
            if(!header && type!="IHDR")Bad();
            if(type=="IHDR")
            {
                if(header||length!=13)Bad();header=true;var width=BinaryPrimitives.ReadUInt32BigEndian(data[..4]);height=BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4,4));Dimensions(width,height);var depth=data[8];var color=data[9];
                var channels=color switch{0=>1,2=>3,3=>1,4=>2,6=>4,_=>0};var valid=color switch{0=>depth is 1 or 2 or 4 or 8 or 16,3=>depth is 1 or 2 or 4 or 8,2 or 4 or 6=>depth is 8 or 16,_=>false};if(!valid||channels==0||data[10]!=0||data[11]!=0||data[12]!=0)throw new MediaValidationException("PNG hỏng hoặc interlace chưa hỗ trợ trong subset local.");
                rowBytes=((long)width*channels*depth+7)/8;needsPalette=color==3;if((rowBytes+1)*height>128*1024*1024)Bad();
            }
            else if(type=="PLTE"){if(length==0||length>768||length%3!=0||packed.Length>0)Bad();palette=true;}
            else if(type=="IDAT"){if(needsPalette&&!palette)Bad();packed.Write(data);}
            else if(type=="IEND"){if(length!=0||offset+12!=b.Length)Bad();end=true;break;}
            else if(type.Length!=4 || char.IsUpper(type[0]))Bad();
            offset+=length+12;
        }
        if(!end||packed.Length==0)Bad();packed.Position=0;using var decoded=new ZLibStream(packed,CompressionMode.Decompress);var buffer=new byte[8192];
        for(var y=0u;y<height;y++){var filter=decoded.ReadByte();if(filter is <0 or >4)Bad();var left=rowBytes;while(left>0){var n=decoded.Read(buffer,0,(int)Math.Min(left,buffer.Length));if(n==0)Bad();left-=n;}}
        if(decoded.ReadByte()!=-1)Bad();
    }
    private static void Jpeg(byte[] b)
    {
        if(b.Length<20||b[0]!=255||b[1]!=216)Bad();var i=2;bool frame=false,scan=false,quant=false,huff=false,entropy=false;
        while(i<b.Length)
        {
            if(b[i++]!=255)Bad();while(i<b.Length&&b[i]==255)i++;if(i>=b.Length)Bad();var marker=b[i++];
            if(marker==217){if(i!=b.Length||!frame||!scan||!quant||!huff||!entropy)Bad();return;}
            if(marker is 216 or 0 || marker is >=208 and <=215)Bad();if(i+2>b.Length)Bad();var length=BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(i,2));if(length<2||i+length>b.Length)Bad();
            if(marker is 192 or 194){if(frame||length<8)Bad();Dimensions(BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(i+5,2)),BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(i+3,2)));var components=b[i+7];if(components is not (1 or 3 or 4)||length!=8+3*components)Bad();frame=true;}
            else if(marker is >=193 and <=207 && marker is not (196 or 200))throw new MediaValidationException("JPEG coding mode chưa hỗ trợ trong subset local.");
            if(marker==219)quant=true;if(marker==196)huff=true;i+=length;
            if(marker==218)
            {
                if(!frame)Bad();scan=true;
                while(i<b.Length){if(b[i]!=255){entropy=true;i++;continue;}if(i+1>=b.Length)Bad();if(b[i+1]==0||b[i+1] is >=208 and <=215){i+=2;continue;}break;}
            }
        }
        Bad();
    }
    private static void Docx(byte[] bytes)
    {
        using var stream=new MemoryStream(bytes,false);using var zip=new ZipArchive(stream,ZipArchiveMode.Read);if(zip.Entries.Count is <3 or >256)Bad();long unpacked=0;var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var e in zip.Entries)
        {
            if(!names.Add(e.FullName)||e.FullName.StartsWith('/')||e.FullName.Contains('\\')||e.FullName.Split('/').Any(p=>p=="..")||e.FullName.Contains("vba",StringComparison.OrdinalIgnoreCase)||e.FullName.Contains("activeX",StringComparison.OrdinalIgnoreCase)||e.FullName.Contains("embeddings/",StringComparison.OrdinalIgnoreCase))Bad();
            unpacked+=e.Length;if(unpacked>100*1024*1024||e.Length>25*1024*1024)Bad();
            if(e.FullName.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)||e.FullName.EndsWith(".rels",StringComparison.OrdinalIgnoreCase))
            {var xml=ReadXml(e);if(xml.Descendants().Any(x=>x.Attribute("TargetMode")?.Value=="External"))throw new MediaValidationException("DOCX có relationship bên ngoài, không nhận trong subset mẫu local.");}
        }
        var contentTypes=ReadXml(zip.GetEntry("[Content_Types].xml")??throw new MediaValidationException("Không phải DOCX."));
        if(!contentTypes.Descendants().Any(x=>x.Name.LocalName=="Override"&&x.Attribute("PartName")?.Value=="/word/document.xml"&&x.Attribute("ContentType")?.Value=="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"))Bad();
        var main=ReadXml(zip.GetEntry("word/document.xml")??throw new MediaValidationException("DOCX thiếu nội dung."));if(main.Root?.Name!=XName.Get("document","http://schemas.openxmlformats.org/wordprocessingml/2006/main"))Bad();
        var relationships=ReadXml(zip.GetEntry("_rels/.rels")??throw new MediaValidationException("DOCX thiếu relationships."));if(!relationships.Descendants().Any(x=>x.Attribute("Type")?.Value=="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" && x.Attribute("Target")?.Value is "word/document.xml" or "/word/document.xml"))Bad();
    }
    private static XDocument ReadXml(ZipArchiveEntry e)
    {using var input=e.Open();using var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=10*1024*1024});return XDocument.Load(reader);}
    private static void Pdf(byte[] bytes)
    {
        if(bytes.Length<50||!bytes.AsSpan().StartsWith("%PDF-"u8))Bad();var text=Encoding.Latin1.GetString(bytes);if(!text.TrimEnd().EndsWith("%%EOF",StringComparison.Ordinal)||!text.Contains("/Catalog",StringComparison.Ordinal)||!text.Contains("/Page",StringComparison.Ordinal)||!text.Contains("startxref",StringComparison.Ordinal))Bad();
    }
    private static void Dimensions(uint width,uint height){if(width==0||height==0||width>16384||height>16384||(long)width*height>40000000)Bad();}
    private static uint Crc(ReadOnlySpan<byte> b){uint crc=uint.MaxValue;foreach(var value in b){crc^=value;for(var i=0;i<8;i++)crc=(crc>>1)^((crc&1)==1?0xedb88320u:0);}return ~crc;}
    private static void Bad()=>throw new MediaValidationException("Signature/cấu trúc nội dung file không hợp lệ.");
}
