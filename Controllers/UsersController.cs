using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZnErp.Data;
using ZnErp.Models;
using ZnErp.Services;

namespace ZnErp.Controllers;

/// <summary>User management (sirf Admin): naya user, role, active/inactive, password reset.</summary>
[Authorize(Roles = Roles.Admin)]
public class UsersController : Controller
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> Index()
    {
        var users = await _db.AppUsers.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        ViewBag.CurrentUserId = CurrentUserId;
        return View(users);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new UserFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        model.UserName = (model.UserName ?? "").Trim();

        var problem = PasswordService.Validate(model.Password);
        if (problem != null) ModelState.AddModelError(nameof(model.Password), problem);

        if (await _db.AppUsers.AnyAsync(u => u.UserName == model.UserName))
            ModelState.AddModelError(nameof(model.UserName), "Yeh username pehle se maujood hai.");

        if (!ModelState.IsValid) return View("Form", model);

        _db.AppUsers.Add(new AppUser
        {
            UserName = model.UserName,
            FullName = model.FullName.Trim(),
            Role = model.Role,
            IsActive = model.IsActive,
            PasswordHash = PasswordService.Hash(model.Password!)
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        return View("Form", new UserFormViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UserFormViewModel model)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        model.Id = id;
        model.UserName = user.UserName; // username badla nahi ja sakta

        if (id == CurrentUserId && (!model.IsActive || model.Role != UserRole.Admin))
            ModelState.AddModelError("", "Aap apna khud ka account deactivate ya Admin role se nahi hata sakte.");

        if (!string.IsNullOrEmpty(model.Password))
        {
            var problem = PasswordService.Validate(model.Password);
            if (problem != null) ModelState.AddModelError(nameof(model.Password), problem);
        }

        // Aakhri active Admin bacha rehna chahiye
        var losesAdmin = user.Role == UserRole.Admin && user.IsActive && (!model.IsActive || model.Role != UserRole.Admin);
        if (losesAdmin && !await _db.AppUsers.AnyAsync(u => u.Id != id && u.Role == UserRole.Admin && u.IsActive))
            ModelState.AddModelError("", "Kam az kam ek active Admin hona zaroori hai.");

        if (!ModelState.IsValid) return View("Form", model);

        user.FullName = model.FullName.Trim();
        user.Role = model.Role;
        user.IsActive = model.IsActive;
        user.FailedLogins = 0;
        user.LockedUntil = null;
        if (!string.IsNullOrEmpty(model.Password))
            user.PasswordHash = PasswordService.Hash(model.Password);

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
