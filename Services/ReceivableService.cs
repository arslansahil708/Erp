using Microsoft.EntityFrameworkCore;
using ZnErp.Data;

namespace ZnErp.Services;

public class ReceivableService
{
    private readonly AppDbContext _db;
    public ReceivableService(AppDbContext db) => _db = db;

    /// <summary>Customer ka outstanding = primary sale invoices - receipts. excludeInvoiceId edit ke waqt use hota hai.</summary>
    public async Task<decimal> OutstandingAsync(int customerId, int? excludeInvoiceId = null)
    {
        var invoiced = await _db.PrimarySales.AsNoTracking()
            .Where(s => s.CustomerId == customerId && (excludeInvoiceId == null || s.Id != excludeInvoiceId))
            .SumAsync(s => (decimal?)s.Total) ?? 0m;
        var received = await _db.CustomerReceipts.AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;
        return invoiced - received;
    }
}
