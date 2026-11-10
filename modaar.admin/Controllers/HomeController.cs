using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace modaar.admin.Controllers;

public class HomeController : Controller
{
    // The landing page after sign-in. The dashboard and the other sections come next.
    public IActionResult Index() => View();

    [AllowAnonymous]
    public IActionResult Error() => View();
}
