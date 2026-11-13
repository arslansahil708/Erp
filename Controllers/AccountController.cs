using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZnErp.Data;
using ZnErp.Models;
using ZnErp.Services;

namespace ZnErp.Controllers;

public class AccountController : Controller
{
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(10);

    // Username na milne par bhi hash check hota hai, taake timing se pata na chale ke user maujood hai ya nahi
    private static readonly string DummyHash = PasswordService.Hash("dummy-password-1");

    private readonly AppDbContext _db;
    public AccountController(AppDbContext db) => _db = db;

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(model);

        const string badLogin = "Username ya password ghalat hai.";
        var userName = model.UserName.Trim();
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.UserName == userName);

        if (user == null)
        {
            PasswordService.Verify(model.Password, DummyHash);
            ModelState.AddModelError("", badLogin);
            return View(model);
        }

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            ModelState.AddModelError("", "Zyada ghalat koshishon ki wajah se account kuch der ke liye lock hai. Baad mein koshish karein.");
            return View(model);
        }

        if (!PasswordService.Verify(model.Password, user.PasswordHash))
        {
            user.FailedLogins++;
            if (user.FailedLogins >= MaxFailedLogins)
            {
                user.LockedUntil = DateTime.UtcNow.Add(LockDuration);
                user.FailedLogins = 0;
            }
            await _db.SaveChangesAsync();
            ModelState.AddModelError("", badLogin);
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError("", "Yeh account inactive hai. Admin se rabta karein.");
            return View(model);
        }

        user.FailedLogins = 0;
        user.LockedUntil = null;
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("FullName", user.FullName),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = int.TryParse(idText, out var id) ? await _db.AppUsers.FindAsync(id) : null;
        if (user == null) return Forbid();

        if (!PasswordService.Verify(model.CurrentPassword, user.PasswordHash))
            ModelState.AddModelError(nameof(model.CurrentPassword), "Maujooda password ghalat hai.");

        var problem = PasswordService.Validate(model.NewPassword);
        if (problem != null)
            ModelState.AddModelError(nameof(model.NewPassword), problem);
        else if (model.NewPassword != model.ConfirmPassword)
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Dono naye password aik jaise nahi hain.");

        if (!ModelState.IsValid) return View(model);

        user.PasswordHash = PasswordService.Hash(model.NewPassword);
        await _db.SaveChangesAsync();

        ViewBag.Done = true;
        return View(new ChangePasswordViewModel());
    }
}
