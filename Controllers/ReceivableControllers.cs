using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ZnErp.Data;
using ZnErp.Models;
using ZnErp.Services;

namespace ZnErp.Controllers;

// ================= Account Receivable =================

[Authorize(Roles = Roles.AdminAccountantSales)]
public class CustomersController : CrudController<Customer>
{
    public CustomersController(AppDbContext db) : base(db) { }
    protected override string Title => "Customers (Distributors)";
    protected override IQueryable<Customer> Query() => Db.Customers.AsNoTracking().OrderBy(c => c.Code);
}

/// <summary>
/// Customer Receipt: customer se paisa wasool hota hai. Save par GL mein auto post hota hai
/// (Dr Cash/Bank, Cr Accounts Receivable). GL entries receipt ke saath ek hi SaveChanges mein save hoti hain.
/// </summary>
[Authorize(Roles = Roles.AdminAccountant)]
public class CustomerReceiptsController : CrudController<CustomerReceipt>
{
    private readonly GlPostingService _gl;

    public CustomerReceiptsController(AppDbContext db, GlPostingService gl) : base(db) => _gl = gl;
    protected override string Title => "Customer Receipts";

    protected override async Task LoadLookups()
    {
        Lookups["CustomerId"] = await Db.Customers.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Code + " - " + c.Name, c.Id.ToString())).ToListAsync();
    }

    protected override async Task BeforeSave(CustomerReceipt e, bool isNew)
    {
        if (isNew)
        {
            var next = (await Db.CustomerReceipts.MaxAsync(r => (int?)r.Id) ?? 0) + 1;
            e.ReceiptNo = "RC-" + next.ToString("D6");
        }
        else
        {
            // ReceiptNo form mein nahi hota, isliye purana number wapas lagana zaroori hai
            e.ReceiptNo = await Db.CustomerReceipts.AsNoTracking()
                .Where(r => r.Id == e.Id).Select(r => r.ReceiptNo).FirstAsync();
        }

        var customerName = await Db.Customers.AsNoTracking()
            .Where(c => c.Id == e.CustomerId).Select(c => c.Name).FirstOrDefaultAsync() ?? "";

        await _gl.RemoveAsync(e.ReceiptNo);
        await _gl.PostReceiptAsync(e, customerName);
    }

    protected override async Task BeforeDelete(CustomerReceipt e)
    {
        await _gl.RemoveAsync(e.ReceiptNo);
    }
}
