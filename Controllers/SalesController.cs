using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ZnErp.Data;
using ZnErp.Models;
using ZnErp.Services;

namespace ZnErp.Controllers;

/// <summary>
/// Point of Sale: naya bill banana, bills ki list aur detail.
/// Har bill GL mein auto post hota hai: Dr Cash, Cr Sales (+ Dr Sales Discount, Dr COGS / Cr Inventory).
/// </summary>
[Authorize(Roles = Roles.AdminAccountantSales)]
public class SalesController : Controller
{
    private readonly AppDbContext _db;
    private readonly GlPostingService _gl;

    public SalesController(AppDbContext db, GlPostingService gl)
    {
        _db = db;
        _gl = gl;
    }

    public async Task<IActionResult> Index()
    {
        var sales = await _db.Sales.AsNoTracking().Include(s => s.Store)
            .OrderByDescending(s => s.Id).Take(200).ToListAsync();
        return View(sales);
    }

    [Authorize(Roles = Roles.AdminSales)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new Sale { Date = DateTime.Now };
        await Fill(model);
        return View(model);
    }

    [Authorize(Roles = Roles.AdminSales)]
    [HttpPost]
    public async Task<IActionResult> Create([Bind("Date,CustomerName,StoreId,IsDelivery,DeliveryAddress,Discount,Lines")] Sale model)
    {
        model.Lines = (model.Lines ?? new()).Where(l => l.ItemId > 0 && l.Quantity != 0).ToList();
        if (model.Lines.Count == 0)
            ModelState.AddModelError("", "Kam az kam ek item line add karein.");
        if (model.Lines.Any(l => l.Quantity < 0 || l.Price < 0))
            ModelState.AddModelError("", "Quantity aur price negative nahi ho sakte.");

        model.SubTotal = model.Lines.Sum(l => l.Quantity * l.Price);
        if (model.Discount > model.SubTotal)
            ModelState.AddModelError("Discount", "Discount bill total se zyada nahi ho sakta.");
        if (!model.IsDelivery) model.DeliveryAddress = null;

        if (!ModelState.IsValid)
        {
            await Fill(model);
            return View(model);
        }

        var next = (await _db.Sales.MaxAsync(s => (int?)s.Id) ?? 0) + 1;
        model.BillNo = "SL-" + next.ToString("D6");
        model.Total = model.SubTotal - model.Discount;

        _db.Sales.Add(model);
        await PostGl(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var sale = await _db.Sales.AsNoTracking()
            .Include(s => s.Store)
            .Include(s => s.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(s => s.Id == id);
        return sale == null ? NotFound() : View(sale);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var sale = await _db.Sales.FindAsync(id);
        if (sale != null)
        {
            await _gl.RemoveAsync(sale.BillNo);
            _db.Sales.Remove(sale);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Purane bills (jo GL auto-posting se pehle bane) ko GL mein post karta hai.
    /// Cost un items ki maujooda CostPrice se nikalti hai. Sirf wo bills jin ki GL entry nahi hai.
    /// </summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> PostPending()
    {
        var posted = (await _db.GLEntries.AsNoTracking()
                .Where(g => g.Reference != null && g.Reference.StartsWith("SL-"))
                .Select(g => g.Reference!).Distinct().ToListAsync())
            .ToHashSet();

        var pending = (await _db.Sales.Include(s => s.Lines).OrderBy(s => s.Id).ToListAsync())
            .Where(s => !posted.Contains(s.BillNo)).ToList();

        foreach (var sale in pending)
            await PostGl(sale);
        await _db.SaveChangesAsync();

        TempData["Msg"] = pending.Count + " purane bill(s) GL mein post ho gaye.";
        return RedirectToAction(nameof(Index));
    }

    private Task PostGl(Sale sale) =>
        _gl.PostSaleAsync(sale.BillNo, sale.Date, "POS Sale " + sale.BillNo, GlCodes.Cash,
            sale.SubTotal, sale.Discount, sale.Total, sale.Lines.Select(l => (l.ItemId, l.Quantity)).ToList());

    private async Task Fill(Sale model)
    {
        ViewBag.Stores = await _db.Stores.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Code + " - " + s.Name, s.Id.ToString())).ToListAsync();

        ViewBag.Editor = new LineEditorModel
        {
            AmountLabel = "Price",
            AmountField = "Price",
            Items = await _db.Items.AsNoTracking().Where(i => i.SalePrice > 0).OrderBy(i => i.Name)
                .Select(i => new LineItemOption { Value = i.Id.ToString(), Text = i.Code + " - " + i.Name, DefaultAmount = i.SalePrice })
                .ToListAsync(),
            Rows = model.Lines.Select(l => new LineRow { ItemId = l.ItemId, Quantity = l.Quantity, Amount = l.Price }).ToList()
        };
    }
}
