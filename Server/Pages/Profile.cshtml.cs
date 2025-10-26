using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Crayon.Box.Pages;

public class Profile : PageModel
{
    [BindProperty(SupportsGet = true)] public required string Id { get; set; }
    
    

    public bool ClientConnected()
    {
        return Crayon.Box.Server.Stations.ContainsKey(Id);
    }

    public string Organization => Config.Organization;
    public Version Ver => Assembly.GetEntryAssembly()!.GetName().Version!;
    
    public void OnGet()
    {
    }
}