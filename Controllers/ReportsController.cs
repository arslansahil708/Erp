using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ZnErp.Data;
using ZnErp.Models;
using ZnErp.Services;

namespace ZnErp.Controllers;

/// <summary>Financial Reports, Output MM aur POS Reports. Sab reports ek hi view (Report.cshtml) use karti hain.</summary>
public class ReportsController : Controller
{
    private readonly AppDbContext _db;
    public ReportsController(AppDbContext db) => _db = db;

    private static string N(decimal d) => d.ToString("N2");
    private static string Q(decimal d) => d.ToString("N0");
    private static string D(DateTime d) => d.ToString("dd-MMM-yyyy");

    private static (DateTime f, DateTime toEx, DateTime t) Range(DateTime? from, DateTime? to, DateTime? defFrom = null)
    {
        var today = DateTime.Today;
        var f = (from ?? defFrom ?? new DateTime(today.Year, today.Month, 1)).Date;
        var t = (to ?? today).Date;
        return (f, t.AddDays(1), t);
    }

    private static ReportViewModel NewReport(string title, DateTime f, DateTime t, bool showFrom = true, bool showTo = true) =>
        new() { Title = title, From = f, To = t, ShowFrom = showFrom, ShowTo = showTo };

    private IActionResult Show(ReportViewModel vm) => View("Report", vm);

    public IActionResult Index() => RedirectToAction("Index", "Home");

    // ============================ Financial Reports ============================

    [Authorize(Roles = Roles.AdminAccountant)]
    public async Task<IActionResult> GLReport(int? accountId, DateTime? from, DateTime? to)
    {
        var (f, toEx, t) = Range(from, to);
        var vm = NewReport("GL Report", f, t);
        vm.AccountId = accountId;
        vm.Accounts = await AccountList();
        vm.Columns = new() { "Date", "Account", "Reference", "Narration", "Debit", "Credit", "Balance" };

        var accounts = await _db.Accounts.AsNoTracking().ToDictionaryAsync(a => a.Id);
        var q = _db.GLEntries.AsNoTracking().AsQueryable();
        if (accountId.HasValue) q = q.Where(e => e.AccountId == accountId.Value);

        var openingList = await q.Where(e => e.Date < f)
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Balance = g.Sum(x => x.Debit - x.Credit) })
            .ToListAsync();
        var running = openingList.ToDictionary(o => o.AccountId, o => o.Balance);

        var entries = await q.Where(e => e.Date >= f && e.Date < toEx)
            .OrderBy(e => e.AccountId).ThenBy(e => e.Date).ThenBy(e => e.Id)
            .ToListAsync();

        var openingShown = new HashSet<int>();
        void ShowOpening(int accId)
        {
            if (!openingShown.Add(accId)) return;
            if (running.TryGetValue(accId, out var bal) && bal != 0)
                vm.Rows.Add(new[] { "", accounts[accId].Name, "", "Opening balance", "", "", N(bal) });
        }

        foreach (var e in entries)
        {
            ShowOpening(e.AccountId);
            running.TryGetValue(e.AccountId, out var bal);
            bal += e.Debit - e.Credit;
            running[e.AccountId] = bal;
            vm.Rows.Add(new[] { D(e.Date), accounts[e.AccountId].Name, e.Reference ?? "", e.Narration ?? "", N(e.Debit), N(e.Credit), N(bal) });
        }

        vm.Footer = new[] { "TOTAL", "", "", "", N(entries.Sum(e => e.Debit)), N(entries.Sum(e => e.Credit)), "" };
        return Show(vm);
    }

    [Authorize(Roles = Roles.AdminAccountant)]
    public async Task<IActionResult> ExpensesDetail(DateTime? from, DateTime? to)
    {
        var (f, toEx, t) = Range(from, to);
        var vm = NewReport("Expenses Detail Report", f, t);
        vm.Columns = new() { "Date", "Account", "Reference", "Narration", "Amount" };

        var rows = await _db.GLEntries.AsNoTracking().Include(e => e.Account)
            .Where(e => e.Account!.AccountType == AccountType.Expense && e.Date >= f && e.Date < toEx)
            .OrderBy(e => e.Account!.Code).ThenBy(e => e.Date).ThenBy(e => e.Id)
            .ToListAsync();

        foreach (var e in rows)
            vm.Rows.Add(new[] { D(e.Date), e.Account!.Name, e.Reference ?? "", e.Narration ?? "", N(e.Debit - e.Credit) });

        vm.Footer = new[] { "TOTAL", "", "", "", N(rows.Sum(e => e.Debit - e.Credit)) };
        return Show(vm);
    }

    // ============================ Output MM ============================

    [Authorize(Roles = Roles.AdminAccountantStore)]
    public async Task<IActionResult> StockBalance(int? storeId)
    {
        var vm = new ReportViewModel { Title = "Stock Balance", StoreId = storeId };
        vm.Stores = await StoreList();
        vm.Columns = new() { "Store", "Code", "Item", "Unit", "Opening", "In", "Out", "Balance" };

        var stores = await _db.Stores.AsNoTracking().ToDictionaryAsync(s => s.Id);
        var items = await _db.Items.AsNoTracking().ToDictionaryAsync(i => i.Id);

        var map = new Dictionary<(int Store, int Item), (decimal Open, decimal In, decimal Out)>();
        void Add((int, int) key, decimal open, decimal inQty, decimal outQty)
        {
            map.TryGetValue(key, out var v);
            map[key] = (v.Open + open, v.In + inQty, v.Out + outQty);
        }

        foreach (var o in await _db.ItemOpeningBalances.AsNoTracking().ToListAsync())
            Add((o.StoreId, o.ItemId), o.Quantity, 0, 0);

        var docLines = await _db.StockDocumentLines.AsNoTracking()
            .Select(l => new { StoreId = l.StockDocument!.StoreId, ItemId = l.ItemId, Type = l.StockDocument.DocType, Qty = l.Quantity })
            .ToListAsync();
        foreach (var l in docLines)
        {
            var key = (l.StoreId, l.ItemId);
            switch (l.Type)
            {
                case StockDocType.ProductionOrder:
                case StockDocType.GoodsReceive:
                case StockDocType.TransferIn:
                    Add(key, 0, l.Qty, 0); break;
                case StockDocType.GoodsReturn:
                case StockDocType.TransferOut:
                    Add(key, 0, 0, l.Qty); break;
                case StockDocType.StockAudit:
                    if (l.Qty >= 0) Add(key, 0, l.Qty, 0); else Add(key, 0, 0, -l.Qty);
                    break;
            }
        }

        var saleLines = await _db.SaleLines.AsNoTracking()
            .Select(l => new { StoreId = l.Sale!.StoreId, ItemId = l.ItemId, Qty = l.Quantity })
            .ToListAsync();
        foreach (var l in saleLines)
            Add((l.StoreId, l.ItemId), 0, 0, l.Qty);

        var primaryLines = await _db.PrimarySaleLines.AsNoTracking()
            .Select(l => new { StoreId = l.PrimarySale!.StoreId, ItemId = l.ItemId, Qty = l.Quantity })
            .ToListAsync();
        foreach (var l in primaryLines)
            Add((l.StoreId, l.ItemId), 0, 0, l.Qty);

        var ordered = map
            .Where(kv => !storeId.HasValue || kv.Key.Store == storeId.Value)
            .OrderBy(kv => stores[kv.Key.Store].Name).ThenBy(kv => items[kv.Key.Item].Name);

        foreach (var kv in ordered)
        {
            var item = items[kv.Key.Item];
            var v = kv.Value;
            vm.Rows.Add(new[] { stores[kv.Key.Store].Name, item.Code, item.Name, item.Unit, Q(v.Open), Q(v.In), Q(v.Out), Q(v.Open + v.In - v.Out) });
        }

        vm.Notes.Add("Balance = Opening + (GRN + Transfer IN + Production + Audit plus) - (Return + Transfer Out + POS Sales + Primary Sales + Audit minus)");
        return Show(vm);
    }

    // ============================ POS Reports ============================

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> PosConsolidated(DateTime? from, DateTime? to)
    {
        var (f, toEx, t) = Range(from, to);
        var vm = NewReport("Consolidated Report", f, t);
        vm.Columns = new() { "Particular", "Value" };

        var sales = await _db.Sales.AsNoTracking().Where(s => s.Date >= f && s.Date < toEx).ToListAsync();
        var petty = await _db.PettyCashes.AsNoTracking().Where(p => p.Date >= f && p.Date < toEx).SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var delivery = sales.Where(s => s.IsDelivery).ToList();

        vm.Rows.Add(new[] { "Total bills", sales.Count.ToString() });
        vm.Rows.Add(new[] { "Gross sales", N(sales.Sum(s => s.SubTotal)) });
        vm.Rows.Add(new[] { "Discounts", N(sales.Sum(s => s.Discount)) });
        vm.Rows.Add(new[] { "Net sales", N(sales.Sum(s => s.Total)) });
        vm.Rows.Add(new[] { "Delivery orders", delivery.Count.ToString() });
        vm.Rows.Add(new[] { "Delivery sales", N(delivery.Sum(s => s.Total)) });
        vm.Rows.Add(new[] { "Petty cash expenses", N(petty) });
        vm.Rows.Add(new[] { "Net after petty cash", N(sales.Sum(s => s.Total) - petty) });
        return Show(vm);
    }

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> CustomerOrderHistory(string? q, DateTime? from, DateTime? to)
    {
        var (f, toEx, t) = Range(from, to, DateTime.Today.AddMonths(-3));
        var vm = NewReport("Customer Order History Report", f, t);
        vm.ShowText = true;
        vm.TextLabel = "Customer name";
        vm.Q = q;
        vm.Columns = new() { "Customer", "Bill No", "Date", "Items", "Total" };

        var query = _db.Sales.AsNoTracking().Include(s => s.Lines).Where(s => s.Date >= f && s.Date < toEx);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(s => s.CustomerName != null && s.CustomerName.Contains(q));

        var sales = await query.OrderBy(s => s.CustomerName).ThenBy(s => s.Date).ToListAsync();
        foreach (var s in sales)
            vm.Rows.Add(new[] { s.CustomerName ?? "(Walk-in)", s.BillNo, D(s.Date), s.Lines.Count.ToString(), N(s.Total) });

        vm.Footer = new[] { "TOTAL", "", "", "", N(sales.Sum(s => s.Total)) };
        return Show(vm);
    }

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> DailySales(DateTime? from, DateTime? to)
    {
        var (f, toEx, t) = Range(from, to);
        var vm = NewReport("Daily Sales Report", f, t);
        vm.Columns = new() { "Date", "Bills", "Gross Sales", "Discount", "Net Sales" };

        var sales = await _db.Sales.AsNoTracking().Where(s => s.Date >= f && s.Date < toEx).ToListAsync();
        foreach (var g in sales.GroupBy(s => s.Date.Date).OrderBy(g => g.Key))
            vm.Rows.Add(new[] { D(g.Key), g.Count().ToString(), N(g.Sum(x => x.SubTotal)), N(g.Sum(x => x.Discount)), N(g.Sum(x => x.Total)) });

        vm.Footer = new[] { "TOTAL", sales.Count.ToString(), N(sales.Sum(s => s.SubTotal)), N(sales.Sum(s => s.Discount)), N(sales.Sum(s => s.Total)) };
        return Show(vm);
    }

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> DayEnd(DateTime? from)
    {
        var day = (from ?? DateTime.Today).Date;
        var vm = NewReport("Day End Report", day, day, showFrom: true, showTo: false);
        vm.FromLabel = "Date";
        vm.Columns = new() { "Store", "Bills", "Gross Sales", "Discount", "Net Sales", "Delivery Bills" };

        var sales = await _db.Sales.AsNoTracking().Include(s => s.Store)
            .Where(s => s.Date >= day && s.Date < day.AddDays(1)).ToListAsync();

        foreach (var g in sales.GroupBy(s => s.Store!.Name).OrderBy(g => g.Key))
            vm.Rows.Add(new[] { g.Key, g.Count().ToString(), N(g.Sum(x => x.SubTotal)), N(g.Sum(x => x.Discount)), N(g.Sum(x => x.Total)), g.Count(x => x.IsDelivery).ToString() });

        vm.Footer = new[] { "TOTAL", sales.Count.ToString(), N(sales.Sum(s => s.SubTotal)), N(sales.Sum(s => s.Discount)), N(sales.Sum(s => s.Total)), sales.Count(s => s.IsDelivery).ToString() };

        var petty = await _db.PettyCashes.AsNoTracking().Where(p => p.Date >= day && p.Date < day.AddDays(1)).SumAsync(p => (decimal?)p.Amount) ?? 0m;
        vm.Notes.Add("Petty cash expenses: " + N(petty));
        vm.Notes.Add("Net sales after petty cash: " + N(sales.Sum(s => s.Total) - petty));
        return Show(vm);
    }

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> DeliveryDispatch(DateTime? from, DateTime? to)
    {
        var (f, toEx, t) = Range(from, to);
        var vm = NewReport("Delivery Dispatch Report", f, t);
        vm.Columns = new() { "Bill No", "Date", "Customer", "Address", "Total" };

        var sales = await _db.Sales.AsNoTracking()
            .Where(s => s.IsDelivery && s.Date >= f && s.Date < toEx).OrderBy(s => s.Date).ToListAsync();
        foreach (var s in sales)
            vm.Rows.Add(new[] { s.BillNo, D(s.Date) + " " + s.Date.ToString("HH:mm"), s.CustomerName ?? "", s.DeliveryAddress ?? "", N(s.Total) });

        vm.Footer = new[] { "TOTAL", "", "", "", N(sales.Sum(s => s.Total)) };
        return Show(vm);
    }

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> DiscountReport(DateTime? from, DateTime? to)
    {
        var (f, toEx, t) = Range(from, to);
        var vm = NewReport("Discount Report", f, t);
        vm.Columns = new() { "Bill No", "Date", "Customer", "Gross", "Discount", "Discount %", "Net" };

        var sales = await _db.Sales.AsNoTracking()
            .Where(s => s.Discount > 0 && s.Date >= f && s.Date < toEx).OrderBy(s => s.Date).ToListAsync();
        foreach (var s in sales)
        {
            var pct = s.SubTotal == 0 ? 0 : s.Discount / s.SubTotal * 100;
            vm.Rows.Add(new[] { s.BillNo, D(s.Date), s.CustomerName ?? "", N(s.SubTotal), N(s.Discount), pct.ToString("N1") + "%", N(s.Total) });
        }

        vm.Footer = new[] { "TOTAL", "", "", N(sales.Sum(s => s.SubTotal)), N(sales.Sum(s => s.Discount)), "", N(sales.Sum(s => s.Total)) };
        return Show(vm);
    }

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> ItemWiseSale(DateTime? from, DateTime? to)
    {
        var (f, toEx, t) = Range(from, to);
        var vm = NewReport("Item Wise Sale", f, t);
        vm.Columns = new() { "Code", "Item", "Quantity", "Amount" };

        var lines = await _db.SaleLines.AsNoTracking()
            .Where(l => l.Sale!.Date >= f && l.Sale.Date < toEx)
            .Select(l => new { ItemId = l.ItemId, Qty = l.Quantity, Amount = l.Quantity * l.Price })
            .ToListAsync();
        var items = await _db.Items.AsNoTracking().ToDictionaryAsync(i => i.Id);

        foreach (var g in lines.GroupBy(l => l.ItemId).OrderByDescending(g => g.Sum(x => x.Amount)))
            vm.Rows.Add(new[] { items[g.Key].Code, items[g.Key].Name, Q(g.Sum(x => x.Qty)), N(g.Sum(x => x.Amount)) });

        vm.Footer = new[] { "TOTAL", "", Q(lines.Sum(l => l.Qty)), N(lines.Sum(l => l.Amount)) };
        vm.Notes.Add("Amounts bill-level discount se pehle ke hain.");
        return Show(vm);
    }

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> PettyCashReport(DateTime? from, DateTime? to)
    {
        var (f, toEx, t) = Range(from, to);
        var vm = NewReport("Petty Cash Report", f, t);
        vm.Columns = new() { "Date", "Account", "Description", "Amount" };

        var rows = await _db.PettyCashes.AsNoTracking().Include(p => p.ExpenseAccount)
            .Where(p => p.Date >= f && p.Date < toEx).OrderBy(p => p.Date).ThenBy(p => p.Id).ToListAsync();
        foreach (var p in rows)
            vm.Rows.Add(new[] { D(p.Date), p.ExpenseAccount!.Name, p.Description, N(p.Amount) });

        vm.Footer = new[] { "TOTAL", "", "", N(rows.Sum(p => p.Amount)) };
        return Show(vm);
    }

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> SaleTransaction(DateTime? from, DateTime? to)
    {
        var (f, toEx, t) = Range(from, to);
        var vm = NewReport("Sale Transaction Report", f, t);
        vm.Columns = new() { "Bill No", "Date", "Item", "Quantity", "Price", "Amount" };

        var lines = await _db.SaleLines.AsNoTracking().Include(l => l.Sale).Include(l => l.Item)
            .Where(l => l.Sale!.Date >= f && l.Sale.Date < toEx)
            .OrderBy(l => l.Sale!.Date).ThenBy(l => l.Id).ToListAsync();
        foreach (var l in lines)
            vm.Rows.Add(new[] { l.Sale!.BillNo, D(l.Sale.Date), l.Item!.Name, Q(l.Quantity), N(l.Price), N(l.Quantity * l.Price) });

        vm.Footer = new[] { "TOTAL", "", "", Q(lines.Sum(l => l.Quantity)), "", N(lines.Sum(l => l.Quantity * l.Price)) };
        return Show(vm);
    }

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> TrendAnalysis(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var defFrom = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
        var (f, toEx, t) = Range(from, to, defFrom);
        var vm = NewReport("Trend Analysis Report (Monthly)", f, t);
        vm.Columns = new() { "Month", "Bills", "Net Sales", "Average Bill", "Growth vs Previous Month" };

        var sales = await _db.Sales.AsNoTracking().Where(s => s.Date >= f && s.Date < toEx).ToListAsync();
        decimal? previous = null;
        foreach (var g in sales.GroupBy(s => new DateTime(s.Date.Year, s.Date.Month, 1)).OrderBy(g => g.Key))
        {
            var net = g.Sum(x => x.Total);
            var avg = g.Count() == 0 ? 0 : net / g.Count();
            var growth = previous.HasValue && previous.Value != 0
                ? ((net - previous.Value) / previous.Value * 100).ToString("N1") + "%"
                : "-";
            vm.Rows.Add(new[] { g.Key.ToString("MMM yyyy"), g.Count().ToString(), N(net), N(avg), growth });
            previous = net;
        }

        vm.Footer = new[] { "TOTAL", sales.Count.ToString(), N(sales.Sum(s => s.Total)), "", "" };
        return Show(vm);
    }

    // ============================ Account Receivable ============================

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> CustomerLedger(int? customerId, DateTime? from, DateTime? to, string? back)
    {
        var (f, toEx, t) = Range(from, to, DateTime.Today.AddMonths(-3));
        var vm = NewReport("Customer Ledger", f, t);
        vm.CustomerId = customerId;
        vm.Customers = await CustomerList();
        if (back == "PrimarySales")
        {
            vm.Back = back;
            vm.BackUrl = Url.Action("Index", "PrimarySales");
        }
        vm.Columns = new() { "Date", "Document", "Type", "Particular", "Debit", "Credit", "Balance" };

        if (!customerId.HasValue)
        {
            vm.Notes.Add("Customer chunein, phir Show dabayen.");
            return Show(vm);
        }

        var invoices = await _db.PrimarySales.AsNoTracking()
            .Where(s => s.CustomerId == customerId.Value).Select(s => new { s.Id, s.InvoiceNo, s.Date, s.Total, s.Remarks }).ToListAsync();
        var receipts = await _db.CustomerReceipts.AsNoTracking()
            .Where(r => r.CustomerId == customerId.Value).Select(r => new { r.Id, r.ReceiptNo, r.Date, r.Amount, r.PaymentMethod, r.Remarks }).ToListAsync();

        var opening = invoices.Where(i => i.Date < f).Sum(i => i.Total) - receipts.Where(r => r.Date < f).Sum(r => r.Amount);
        var running = opening;
        vm.Rows.Add(new[] { "", "", "", "Opening balance", "", "", N(opening) });

        var entries = invoices.Where(i => i.Date >= f && i.Date < toEx)
                .Select(i => (Date: i.Date, Order: 0, Id: i.Id, Doc: i.InvoiceNo, Type: "Primary Sale", Part: i.Remarks ?? "", Debit: i.Total, Credit: 0m))
            .Concat(receipts.Where(r => r.Date >= f && r.Date < toEx)
                .Select(r => (Date: r.Date, Order: 1, Id: r.Id, Doc: r.ReceiptNo, Type: "Receipt", Part: r.PaymentMethod + (string.IsNullOrWhiteSpace(r.Remarks) ? "" : " - " + r.Remarks), Debit: 0m, Credit: r.Amount)))
            .OrderBy(e => e.Date).ThenBy(e => e.Order).ThenBy(e => e.Id)
            .ToList();

        foreach (var e in entries)
        {
            running += e.Debit - e.Credit;
            vm.Rows.Add(new[] { D(e.Date), e.Doc, e.Type, e.Part, e.Debit == 0 ? "" : N(e.Debit), e.Credit == 0 ? "" : N(e.Credit), N(running) });
        }

        vm.Footer = new[] { "CLOSING", "", "", "", N(entries.Sum(e => e.Debit)), N(entries.Sum(e => e.Credit)), N(running) };
        vm.Notes.Add("Debit = primary sale invoice, Credit = customer se wasooli. Balance positive = customer ne paisa dena hai.");
        return Show(vm);
    }

    [Authorize(Roles = Roles.AdminAccountantSales)]
    public async Task<IActionResult> ReceivableAging(DateTime? from)
    {
        var asOf = (from ?? DateTime.Today).Date;
        var vm = NewReport("Receivable Aging", asOf, asOf, showFrom: true, showTo: false);
        vm.FromLabel = "As on date";
        vm.Columns = new() { "Customer", "0-30 days", "31-60 days", "61-90 days", "Over 90 days", "Advance (unapplied)", "Outstanding" };

        var invoices = await _db.PrimarySales.AsNoTracking()
            .Where(s => s.Date <= asOf).Select(s => new { s.CustomerId, s.Date, s.Total }).ToListAsync();
        var receiptTotals = (await _db.CustomerReceipts.AsNoTracking()
                .Where(r => r.Date <= asOf).GroupBy(r => r.CustomerId)
                .Select(g => new { CustomerId = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync())
            .ToDictionary(x => x.CustomerId, x => x.Amount);
        var customers = await _db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id);

        var ids = invoices.Select(i => i.CustomerId).Concat(receiptTotals.Keys).Distinct();
        var rows = new List<(string Name, decimal B1, decimal B2, decimal B3, decimal B4, decimal Advance)>();

        foreach (var id in ids)
        {
            // Wasooli sab se purane invoices par pehle lagti hai (FIFO)
            receiptTotals.TryGetValue(id, out var remainingReceipt);
            decimal b1 = 0, b2 = 0, b3 = 0, b4 = 0;

            foreach (var inv in invoices.Where(i => i.CustomerId == id).OrderBy(i => i.Date))
            {
                var applied = Math.Min(remainingReceipt, inv.Total);
                remainingReceipt -= applied;
                var open = inv.Total - applied;
                if (open <= 0) continue;

                var age = (asOf - inv.Date.Date).Days;
                if (age <= 30) b1 += open;
                else if (age <= 60) b2 += open;
                else if (age <= 90) b3 += open;
                else b4 += open;
            }

            var name = customers.TryGetValue(id, out var c) ? c.Code + " - " + c.Name : "(unknown)";
            if (b1 + b2 + b3 + b4 + remainingReceipt != 0)
                rows.Add((name, b1, b2, b3, b4, remainingReceipt));
        }

        foreach (var r in rows.OrderBy(r => r.Name))
            vm.Rows.Add(new[] { r.Name, N(r.B1), N(r.B2), N(r.B3), N(r.B4), N(r.Advance), N(r.B1 + r.B2 + r.B3 + r.B4 - r.Advance) });

        vm.Footer = new[]
        {
            "TOTAL", N(rows.Sum(r => r.B1)), N(rows.Sum(r => r.B2)), N(rows.Sum(r => r.B3)), N(rows.Sum(r => r.B4)),
            N(rows.Sum(r => r.Advance)), N(rows.Sum(r => r.B1 + r.B2 + r.B3 + r.B4 - r.Advance))
        };
        vm.Notes.Add("Age invoice date se gini jati hai. Customer ki wasooli sab se purane invoices par pehle lagti hai.");
        return Show(vm);
    }

    // ============================ helpers ============================

    private async Task<List<SelectListItem>> StoreList() =>
        await _db.Stores.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Code + " - " + s.Name, s.Id.ToString())).ToListAsync();

    private async Task<List<SelectListItem>> CustomerList() =>
        await _db.Customers.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Code + " - " + c.Name, c.Id.ToString())).ToListAsync();

    private async Task<List<SelectListItem>> AccountList() =>
        await _db.Accounts.AsNoTracking().OrderBy(a => a.Code)
            .Select(a => new SelectListItem(a.Code + " - " + a.Name, a.Id.ToString())).ToListAsync();
}
