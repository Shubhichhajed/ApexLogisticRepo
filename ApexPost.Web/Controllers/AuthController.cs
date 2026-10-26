using System.Security.Claims;
using ApexPost.Application;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AuthenticationService = ApexPost.Application.AuthenticationService;

namespace ApexPost.Web.Controllers;

public sealed class AuthController : Controller
{
    private readonly AuthenticationService authenticationService;

    public AuthController(AuthenticationService authenticationService)
    {
        this.authenticationService = authenticationService;
    }

    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login()
    {
        return View(new LoginViewModel());
    }

    [HttpPost("/login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var staff = await authenticationService.AuthenticateAsync(model.Email, cancellationToken);
        if (staff is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid or inactive staff account.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new("StaffId", staff.StaffId),
            new("Role", staff.Role.ToString()),
            new(ClaimTypes.Role, staff.Role.ToString()),
            new("BranchCode", staff.HomeBranch.Value),
            new(ClaimTypes.Name, staff.Name),
            new(ClaimTypes.Email, staff.Email)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        });

        return RedirectToAction("Dashboard", "Home");
    }

    [HttpPost("/logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
