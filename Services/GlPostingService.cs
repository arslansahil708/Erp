using Microsoft.EntityFrameworkCore;
using ZnErp.Data;
using ZnErp.Models;

namespace ZnErp.Services;

/// <summary>Standard account codes jo auto-posting mein use hote hain.</summary>
public static class GlCodes
{
    public const string Cash = "1001";          // Petty Cash / cash in hand (pehle se maujood)
    public const string Bank = "1002";
    public const string Receivable = "1003";
    public const string Inventory = "1004";
    public const string SalesRevenue = "4001";
    public const string Cogs = "5006";
    public const string SalesDiscount = "5007";

    public static readonly Dictionary<string, (string Name, AccountType Type)> Defaults = new()
    {
        [Cash] = ("Petty Cash", AccountType.Asset),
        [Bank] = ("Bank", AccountType.Asset),
        [Receivable] = ("Accounts Receivable", AccountType.Asset),
        [Inventory] = ("Inventory", AccountType.Asset),
        [SalesRevenue] = ("Sales Revenue", AccountType.Income),
        [Cogs] = ("Cost of Goods Sold", AccountType.Expense),
        [SalesDiscount] = ("Sales Discount", AccountType.Expense),
    };
}

/// <summary>
/// GL entries banata hai. Yeh khud SaveChanges nahi karta: caller document aur GL entries
/// ek hi SaveChanges (ek transaction) mein save karta hai, taake dono hamesha saath rahen.
/// </summary>
public class GlPostingService
{
    private readonly AppDbContext _db;
    private readonly Dictionary<string, Account> _accounts = new();

    public GlPostingService(AppDbContext db) => _db = db;

    /// <summary>Account code se account dhoondta hai; maujood na ho to bana deta hai.</summary>
    private async Task<Account> AccountAsync(string code)
    {
        if (_accounts.TryGetValue(code, out var cached)) return cached;

        var acc = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == code);
        if (acc == null)
        {
            var d = GlCodes.Defaults[code];
            acc = new Account { Code = code, Name = d.Name, AccountType = d.Type };
            _db.Accounts.Add(acc);
        }

        _accounts[code] = acc;
        return acc;
    }

    private void Add(Account acc, DateTime date, decimal debit, decimal credit, string narration, string reference)
    {
        if (debit == 0 && credit == 0) return;
        _db.GLEntries.Add(new GLEntry
        {
            Date = date,
            Account = acc,
            Debit = debit,
            Credit = credit,
            Narration = narration.Length > 300 ? narration[..300] : narration,
            Reference = reference
        });
    }

    /// <summary>Is reference ki purani saari GL entries hata deta hai (edit / delete ke liye).</summary>
    public async Task RemoveAsync(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return;
        var old = await _db.GLEntries.Where(g => g.Reference == reference).ToListAsync();
        _db.GLEntries.RemoveRange(old);
    }

    /// <summary>
    /// Sale posting:
    ///   Dr  debitCode (Cash ya Receivable)   total
    ///   Dr  Sales Discount                   discount
    ///       Cr  Sales Revenue                subTotal
    ///   Dr  Cost of Goods Sold               cost
    ///       Cr  Inventory                    cost
    /// </summary>
    public async Task PostSaleAsync(string reference, DateTime date, string narration, string debitCode,
        decimal subTotal, decimal discount, decimal total, IEnumerable<(int ItemId, decimal Quantity)> lines)
    {
        var lineList = lines.ToList();
        var itemIds = lineList.Select(l => l.ItemId).Distinct().ToList();
        var costs = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.CostPrice);
        var cost = lineList.Sum(l => l.Quantity * (costs.TryGetValue(l.ItemId, out var c) ? c : 0m));

        var debitAcc = await AccountAsync(debitCode);
        var salesAcc = await AccountAsync(GlCodes.SalesRevenue);

        Add(debitAcc, date, total, 0, narration, reference);
        if (discount > 0)
            Add(await AccountAsync(GlCodes.SalesDiscount), date, discount, 0, narration, reference);
        Add(salesAcc, date, 0, subTotal, narration, reference);

        if (cost > 0)
        {
            Add(await AccountAsync(GlCodes.Cogs), date, cost, 0, narration, reference);
            Add(await AccountAsync(GlCodes.Inventory), date, 0, cost, narration, reference);
        }
    }

    /// <summary>Customer receipt: Dr Cash/Bank, Cr Accounts Receivable.</summary>
    public async Task PostReceiptAsync(CustomerReceipt r, string customerName)
    {
        var narration = "Receipt from " + customerName + " (" + r.ReceiptNo + ")";
        var debitAcc = await AccountAsync(r.PaymentMethod == PaymentMethod.Bank ? GlCodes.Bank : GlCodes.Cash);
        var receivable = await AccountAsync(GlCodes.Receivable);

        Add(debitAcc, r.Date, r.Amount, 0, narration, r.ReceiptNo);
        Add(receivable, r.Date, 0, r.Amount, narration, r.ReceiptNo);
    }
}
