using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ZnErp.Data;
using ZnErp.Models;
using ZnErp.Services;

namespace ZnErp.Controllers;

// ================= General Setups / Material Management : Definitions =================

[Authorize(Roles = Roles.AdminStore)]
public class StoresController : CrudController<Store>
{
    public StoresController(AppDbContext db) : base(db) { }
    protected override string Title => "Stores";
    protected override IQueryable<Store> Query() => Db.Stores.AsNoTracking().OrderBy(s => s.Name);
}

[Authorize(Roles = Roles.AdminStore)]
public class ItemsController : CrudController<Item>
{
    public ItemsController(AppDbContext db) : base(db) { }
    protected override string Title => "Items";
    protected override IQueryable<Item> Query() => Db.Items.AsNoTracking().OrderBy(i => i.Name);
}

[Authorize(Roles = Roles.AdminStore)]
public class OpeningBalancesController : CrudController<ItemOpeningBalance>
{
    public OpeningBalancesController(AppDbContext db) : base(db) { }
    protected override string Title => "Items Opening Balances";

    protected override async Task LoadLookups()
    {
        Lookups["StoreId"] = await Db.Stores.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Code + " - " + s.Name, s.Id.ToString())).ToListAsync();
        Lookups["ItemId"] = await Db.Items.AsNoTracking().OrderBy(i => i.Name)
            .Select(i => new SelectListItem(i.Code + " - " + i.Name, i.Id.ToString())).ToListAsync();
    }
}

// ================= Financial Accounts =================

[Authorize(Roles = Roles.AdminAccountant)]
public class AccountsController : CrudController<Account>
{
    public AccountsController(AppDbContext db) : base(db) { }
    protected override string Title => "Chart of Accounts";
    protected override IQueryable<Account> Query() => Db.Accounts.AsNoTracking().OrderBy(a => a.Code);
}

[Authorize(Roles = Roles.AdminAccountant)]
public class JournalController : CrudController<GLEntry>
{
    public JournalController(AppDbContext db) : base(db) { }
    protected override string Title => "Journal Entry";

    protected override async Task LoadLookups()
    {
        Lookups["AccountId"] = await Db.Accounts.AsNoTracking().OrderBy(a => a.Code)
            .Select(a => new SelectListItem(a.Code + " - " + a.Name, a.Id.ToString())).ToListAsync();
    }

    protected override Task BeforeSave(GLEntry e, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(e.Reference)) e.Reference = "JV";
        return Task.CompletedTask;
    }
}

/// <summary>
/// Petty Cash Entry form: expense account debit hota hai aur Petty Cash account (1001) credit.
/// </summary>
[Authorize(Roles = Roles.AdminAccountant)]
public class PettyCashController : CrudController<PettyCash>
{
    public PettyCashController(AppDbContext db) : base(db) { }
    protected override string Title => "Petty Cash Entry";

    protected override async Task LoadLookups()
    {
        Lookups["ExpenseAccountId"] = await Db.Accounts.AsNoTracking()
            .Where(a => a.AccountType == AccountType.Expense).OrderBy(a => a.Code)
            .Select(a => new SelectListItem(a.Code + " - " + a.Name, a.Id.ToString())).ToListAsync();
    }

    protected override async Task AfterSave(PettyCash e, bool isNew)
    {
        var reference = "PC-" + e.Id;

        var old = await Db.GLEntries.Where(g => g.Reference == reference).ToListAsync();
        Db.GLEntries.RemoveRange(old);

        var cash = await Db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Code == "1001");
        if (cash != null)
        {
            Db.GLEntries.Add(new GLEntry { Date = e.Date, AccountId = e.ExpenseAccountId, Debit = e.Amount, Narration = e.Description, Reference = reference });
            Db.GLEntries.Add(new GLEntry { Date = e.Date, AccountId = cash.Id, Credit = e.Amount, Narration = e.Description, Reference = reference });
        }

        await Db.SaveChangesAsync();
    }

    protected override async Task BeforeDelete(PettyCash e)
    {
        var reference = "PC-" + e.Id;
        Db.GLEntries.RemoveRange(await Db.GLEntries.Where(g => g.Reference == reference).ToListAsync());
    }
}

// ================= Human Resource / Payroll =================

[Authorize(Roles = Roles.AdminAccountant)]
public class EmployeesController : CrudController<Employee>
{
    public EmployeesController(AppDbContext db) : base(db) { }
    protected override string Title => "Employees";
    protected override IQueryable<Employee> Query() => Db.Employees.AsNoTracking().OrderBy(e => e.Code);
}

[Authorize(Roles = Roles.AdminAccountant)]
public class PayrollsController : CrudController<Payroll>
{
    public PayrollsController(AppDbContext db) : base(db) { }
    protected override string Title => "Payroll";

    protected override async Task LoadLookups()
    {
        Lookups["EmployeeId"] = await Db.Employees.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.Code)
            .Select(e => new SelectListItem(e.Code + " - " + e.Name, e.Id.ToString())).ToListAsync();
    }

    protected override async Task BeforeSave(Payroll p, bool isNew)
    {
        var emp = await Db.Employees.AsNoTracking().FirstAsync(e => e.Id == p.EmployeeId);
        p.BasicSalary = emp.BasicSalary;
        p.NetPay = p.BasicSalary + p.Allowances - p.Deductions;
    }
}
