using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ZnErp.Data;
using ZnErp.Models;
using ZnErp.Services;

namespace ZnErp.Controllers;

/// <summary>Entries (MM): Production Order, GRN, Goods Return, Transfer IN/Out, Stock Audit.</summary>
[Authorize(Roles = Roles.AdminStore)]
public class StockDocumentsController : Controller
{
    private readonly AppDbContext _db;
    public StockDocumentsController(AppDbContext db) => _db = db;

    private static readonly Dictionary<StockDocType, DocMeta> Meta = new()
    {
        [StockDocType.ProductionOrder] = new() { Title = "Production Order", Prefix = "PO", Hint = "Bani hui item ki quantity stock mein add hoti hai." },
        [StockDocType.GoodsReceive] = new() { Title = "Goods Receive Note", Prefix = "GRN", Hint = "Supplier se aaya maal stock mein add hota hai." },
        [StockDocType.GoodsReturn] = new() { Title = "Goods Return Note", Prefix = "GRT", Hint = "Wapas bheja gaya maal stock se kam hota hai." },
        [StockDocType.TransferIn] = new() { Title = "Transfer IN", Prefix = "TIN", NeedsOther = true, OtherLabel = "Received From Store", Hint = "Dusre store se aaya maal is store mein add hota hai." },
        [StockDocType.TransferOut] = new() { Title = "Transfer Out", Prefix = "TOUT", NeedsOther = true, OtherLabel = "Sent To Store", Hint = "Is store se bheja gaya maal stock se kam hota hai." },
        [StockDocType.StockAudit] = new() { Title = "Stock Audit", Prefix = "SA", Hint = "Adjustment quantity likhein: + zyada mila, - kam mila." },
    };

    public async Task<IActionResult> Index(StockDocType type = StockDocType.GoodsReceive)
    {
        ViewBag.Type = type;
        ViewBag.Meta = Meta[type];

        var docs = await _db.StockDocuments.AsNoTracking()
            .Include(d => d.Store).Include(d => d.OtherStore).Include(d => d.Lines)
            .Where(d => d.DocType == type)
            .OrderByDescending(d => d.Id)
            .ToListAsync();
        return View(docs);
    }

    [HttpGet]
    public async Task<IActionResult> Create(StockDocType type)
    {
        var model = new StockDocument { DocType = type, Date = DateTime.Today };
        await Fill(type, model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(StockDocType type, [Bind("Date,StoreId,OtherStoreId,Remarks,Lines")] StockDocument model)
    {
        Validate(type, model);

        if (!ModelState.IsValid)
        {
            model.DocType = type;
            await Fill(type, model);
            return View(model);
        }

        var next = (await _db.StockDocuments.MaxAsync(d => (int?)d.Id) ?? 0) + 1;
        model.DocType = type;
        model.DocNo = Meta[type].Prefix + "-" + next.ToString("D5");

        _db.StockDocuments.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    /// <summary>
    /// Maujooda document ko usi jagah edit karna (delete + dobara entry ki zaroorat nahi).
    /// DocNo aur type wahi rehte hain. Stock balance document lines se hi nikalta hai,
    /// isliye purani quantity khud-ba-khud hat kar nayi lag jati hai.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var doc = await _db.StockDocuments.AsNoTracking().Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        doc.Lines = doc.Lines.OrderBy(l => l.Id).ToList();
        await Fill(doc.DocType, doc);
        return View("Create", doc);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Date,StoreId,OtherStoreId,Remarks,Lines")] StockDocument model)
    {
        var doc = await _db.StockDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        var type = doc.DocType;
        Validate(type, model);

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.DocNo = doc.DocNo;
            model.DocType = type;
            await Fill(type, model);
            return View("Create", model);
        }

        doc.Date = model.Date;
        doc.StoreId = model.StoreId;
        doc.OtherStoreId = model.OtherStoreId;
        doc.Remarks = model.Remarks;

        // Purani lines hata kar nayi lines lagti hain; header + lines ek hi SaveChanges (transaction) mein
        _db.StockDocumentLines.RemoveRange(doc.Lines.ToList());
        foreach (var l in model.Lines)
            doc.Lines.Add(new StockDocumentLine { ItemId = l.ItemId, Quantity = l.Quantity, Rate = l.Rate });

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var doc = await _db.StockDocuments.AsNoTracking()
            .Include(d => d.Store).Include(d => d.OtherStore)
            .Include(d => d.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        ViewBag.Type = doc.DocType;
        ViewBag.Meta = Meta[doc.DocType];
        return View(doc);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, StockDocType type)
    {
        var doc = await _db.StockDocuments.FindAsync(id);
        if (doc != null)
        {
            _db.StockDocuments.Remove(doc);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { type });
    }

    /// <summary>Lines saaf karta hai aur validation errors ModelState mein daalta hai (Create aur Edit dono ke liye).</summary>
    private void Validate(StockDocType type, StockDocument model)
    {
        var meta = Meta[type];

        model.Lines = (model.Lines ?? new()).Where(l => l.ItemId > 0 && l.Quantity != 0).ToList();
        if (model.Lines.Count == 0)
            ModelState.AddModelError("", "Kam az kam ek item line add karein (item aur quantity ke saath).");
        if (type != StockDocType.StockAudit && model.Lines.Any(l => l.Quantity < 0))
            ModelState.AddModelError("", "Quantity negative nahi ho sakti (sirf Stock Audit mein +/- allowed hai).");

        if (!meta.NeedsOther)
            model.OtherStoreId = null;
        else if (model.OtherStoreId == null || model.OtherStoreId == model.StoreId)
            ModelState.AddModelError("", meta.OtherLabel + " alag store hona chahiye.");
    }

    private async Task Fill(StockDocType type, StockDocument model)
    {
        ViewBag.Type = type;
        ViewBag.Meta = Meta[type];
        ViewBag.Stores = await _db.Stores.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Code + " - " + s.Name, s.Id.ToString())).ToListAsync();

        ViewBag.Editor = new LineEditorModel
        {
            AmountLabel = "Rate",
            AmountField = "Rate",
            Items = await _db.Items.AsNoTracking().OrderBy(i => i.Name)
                .Select(i => new LineItemOption { Value = i.Id.ToString(), Text = i.Code + " - " + i.Name, DefaultAmount = i.CostPrice })
                .ToListAsync(),
            Rows = model.Lines.Select(l => new LineRow { ItemId = l.ItemId, Quantity = l.Quantity, Amount = l.Rate }).ToList()
        };
    }
}
