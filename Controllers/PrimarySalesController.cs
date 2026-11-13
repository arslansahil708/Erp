using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ZnErp.Data;
using ZnErp.Models;
using ZnErp.Services;

namespace ZnErp.Controllers;

/// <summary>
/// Primary Sales: distributor / dealer (Customer) ko credit par bulk sale.
/// Save par stock kam hota hai (Stock Balance report), customer ka receivable barhta hai aur
/// GL mein auto post hota hai: Dr Accounts Receivable, Cr Sales (+ Dr COGS, Cr Inventory).
/// </summary>
[Authorize(Roles = Roles.AdminAccountantSales)]
public class PrimarySalesController : Controller
{
    private readonly AppDbContext _db;
    private readonly GlPostingService _gl;
    private readonly ReceivableService _ar;

    public PrimarySalesController(AppDbContext db, GlPostingService gl, ReceivableService ar)
    {
        _db = db;
        _gl = gl;
        _ar = ar;
    }

    public async Task<IActionResult> Index()
    {
        var sales = await _db.PrimarySales.AsNoTracking()
            .Include(s => s.Customer).Include(s => s.Store)
            .OrderByDescending(s => s.Id).Take(200).ToListAsync();
        return View(sales);
    }

    [Authorize(Roles = Roles.AdminSales)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new PrimarySale { Date = DateTime.Today };
        await Fill(model);
        return View("Form", model);
    }

    [Authorize(Roles = Roles.AdminSales)]
    [HttpPost]
    public async Task<IActionResult> Create([Bind("Date,CustomerId,StoreId,Remarks,Discount,Lines")] PrimarySale model)
    {
        await Validate(model, null);
        if (!ModelState.IsValid)
        {
            await Fill(model);
            return View("Form", model);
        }

        var next = (await _db.PrimarySales.MaxAsync(s => (int?)s.Id) ?? 0) + 1;
        model.InvoiceNo = "PS-" + next.ToString("D6");

        _db.PrimarySales.Add(model);
        await PostGl(model.InvoiceNo, model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize(Roles = Roles.AdminSales)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var sale = await _db.PrimarySales.AsNoTracking().Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return NotFound();

        sale.Lines = sale.Lines.OrderBy(l => l.Id).ToList();
        await Fill(sale);
        return View("Form", sale);
    }

    [Authorize(Roles = Roles.AdminSales)]
    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Date,CustomerId,StoreId,Remarks,Discount,Lines")] PrimarySale model)
    {
        var sale = await _db.PrimarySales.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return NotFound();

        await Validate(model, id);
        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.InvoiceNo = sale.InvoiceNo;
            await Fill(model);
            return View("Form", model);
        }

        sale.Date = model.Date;
        sale.CustomerId = model.CustomerId;
        sale.StoreId = model.StoreId;
        sale.Remarks = model.Remarks;
        sale.Discount = model.Discount;
        sale.SubTotal = model.SubTotal;
        sale.Total = model.Total;

        _db.PrimarySaleLines.RemoveRange(sale.Lines.ToList());
        foreach (var l in model.Lines)
            sale.Lines.Add(new PrimarySaleLine { ItemId = l.ItemId, Quantity = l.Quantity, Price = l.Price });

        // GL: purani entries hata kar nayi lagti hain, sab ek hi SaveChanges (transaction) mein
        // (naye lines model.Lines mein hain; sale.Lines mein purani deleted lines bhi hoti hain)
        await _gl.RemoveAsync(sale.InvoiceNo);
        await PostGl(sale.InvoiceNo, model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var sale = await _db.PrimarySales.AsNoTracking()
            .Include(s => s.Customer).Include(s => s.Store)
            .Include(s => s.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(s => s.Id == id);
        return sale == null ? NotFound() : View(sale);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var sale = await _db.PrimarySales.FindAsync(id);
        if (sale != null)
        {
            await _gl.RemoveAsync(sale.InvoiceNo);
            _db.PrimarySales.Remove(sale);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------

    /// <summary>Lines saaf karta hai, totals nikalta hai aur validation errors ModelState mein daalta hai.</summary>
    private async Task Validate(PrimarySale model, int? editingId)
    {
        model.Lines = (model.Lines ?? new()).Where(l => l.ItemId > 0 && l.Quantity != 0).ToList();
        if (model.Lines.Count == 0)
            ModelState.AddModelError("", "Kam az kam ek item line add karein.");
        if (model.Lines.Any(l => l.Quantity < 0 || l.Price < 0))
            ModelState.AddModelError("", "Quantity aur price negative nahi ho sakte.");

        model.SubTotal = model.Lines.Sum(l => l.Quantity * l.Price);
        model.Total = model.SubTotal - model.Discount;
        if (model.Discount > model.SubTotal)
            ModelState.AddModelError("Discount", "Discount invoice total se zyada nahi ho sakta.");

        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == model.CustomerId);
        if (customer == null)
        {
            ModelState.AddModelError("", "Customer chunein.");
        }
        else if (!customer.IsActive)
        {
            ModelState.AddModelError("", "Yeh customer inactive hai.");
        }
        else if (customer.CreditLimit > 0)
        {
            var outstanding = await _ar.OutstandingAsync(customer.Id, editingId);
            if (outstanding + model.Total > customer.CreditLimit)
                ModelState.AddModelError("", "Credit limit cross ho rahi hai. Limit: " + customer.CreditLimit.ToString("N2")
                    + ", maujooda outstanding: " + outstanding.ToString("N2") + ", is invoice ke baad: " + (outstanding + model.Total).ToString("N2") + ".");
        }

        if (!await _db.Stores.AnyAsync(s => s.Id == model.StoreId))
            ModelState.AddModelError("", "Store chunein.");
    }

    private Task PostGl(string invoiceNo, PrimarySale src) =>
        _gl.PostSaleAsync(invoiceNo, src.Date, "Primary Sale " + invoiceNo, GlCodes.Receivable,
            src.SubTotal, src.Discount, src.Total, src.Lines.Select(l => (l.ItemId, l.Quantity)).ToList());

    private async Task Fill(PrimarySale model)
    {
        ViewBag.Customers = await _db.Customers.AsNoTracking().Where(c => c.IsActive || c.Id == model.CustomerId)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Code + " - " + c.Name, c.Id.ToString())).ToListAsync();
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
