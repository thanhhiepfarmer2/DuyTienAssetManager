using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Pages.Help;

public sealed class IndexModel : PageModel { public void OnGet() { Response.Headers.CacheControl = "no-store"; } }
