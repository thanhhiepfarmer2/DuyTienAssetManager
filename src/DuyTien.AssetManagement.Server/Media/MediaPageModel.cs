using DuyTien.AssetManagement.Application.Media;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Media;
public abstract class MediaPageModel:PageModel
{
    [BindProperty]public string? RequestKey{get;set;}
    public string? Error{get;protected set;}public string? Feedback{get;protected set;}
    protected bool JsonRequest=>Request.Headers.Accept.ToString().Contains("application/json",StringComparison.OrdinalIgnoreCase);
    protected string Key=>RequestKey??"";
    protected string Handler=>Request.Query["handler"].ToString();
    protected virtual string? RetryEntity=>null;
    public bool Retrying(string handler,string? entity=null)=>Feedback is null&&Error is not null&&HttpMethods.IsPost(Request.Method)&&string.Equals(Handler,handler,StringComparison.OrdinalIgnoreCase)&&(entity is null||entity==RetryEntity);
    public string FormKey(string handler,string? entity=null)=>Retrying(handler,entity)?Key:Guid.NewGuid().ToString("N");
    protected void NewKey(){RequestKey=Guid.NewGuid().ToString("N");ModelState.Remove(nameof(RequestKey));}
    protected IActionResult? JsonSuccess(object result)=>JsonRequest?new JsonResult(new{success=true,result}):null;
    protected IActionResult Failure(Exception e)
    {
        Error=MediaWeb.Message(e);var status=MediaWeb.Status(e);if(JsonRequest)return new JsonResult(new{success=false,message=Error,fileId=e is MediaPendingException p?p.FileId:null}){StatusCode=status};
        if(e is MediaForbiddenException)return Forbid();if(e is MediaNotFoundException)return NotFound();Response.StatusCode=Feedback is null?status:200;if(Feedback is not null)Error="Thao tác đã xác nhận nhưng màn hình chưa tải được; tải lại khi kết nối ổn định.";return Page();
    }
}
