using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tunnel.Pages;

public class Profile : PageModel
{
    [BindProperty(SupportsGet = true)]
    public required string Id { get; set; }

    public bool ClientConnected()
    {
        return Tunnel.Stations.ContainsKey(Id); 
    }
    
    public void OnGet()
    {
    }
}