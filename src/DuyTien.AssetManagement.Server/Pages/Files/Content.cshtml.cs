using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Server.Media;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Pages.Files;
[Authorize]
public sealed class ContentModel(MediaLibraryService service):PageModel
{
    public async Task<IActionResult> OnGetAsync(string id,bool? preview,CancellationToken ct)
    {Response.Headers.CacheControl="no-store";try{var file=await service.DownloadAsync(CategoryWeb.Actor(User),id,ct);MediaWeb.DownloadHeaders(Response,file,preview==true);return File(file.Bytes,file.MimeType);}catch(Exception e)when(MediaWeb.Expected(e)){if(e is MediaForbiddenException)return Forbid();return new ObjectResult(new{message=MediaWeb.Message(e)}){StatusCode=MediaWeb.Status(e)};}}
}
