using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZnErp.Data;

namespace ZnErp.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        ViewBag.TodaySales = await _db.Sales.Where(s => s.Date >= today && s.Date < tomorrow)
            .SumAsync(s => (decimal?)s.Total) ?? 0m;
        ViewBag.TodayBills = await _db.Sales.CountAsync(s => s.Date >= today && s.Date < tomorrow);
        ViewBag.ItemCount = await _db.Items.CountAsync();
        ViewBag.StoreCount = await _db.Stores.CountAsync();
        ViewBag.EmployeeCount = await _db.Employees.CountAsync(e => e.IsActive);
        return View();
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
