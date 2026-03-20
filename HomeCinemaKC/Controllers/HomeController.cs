using HomeCinemaKC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace HomeCinemaKC.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => View();

        [Authorize]
        public IActionResult Catalog() => View();

        [Authorize]
        public IActionResult Profile() => View();

        [Authorize(Roles = "admin")]
        public IActionResult Admin() => View();

        public IActionResult AccessDenied() => View();

        public IActionResult Logout() => SignOut("Cookies", "OpenIdConnect");

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
