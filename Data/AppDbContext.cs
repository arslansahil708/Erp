using Microsoft.EntityFrameworkCore;
using ZnErp.Models;

namespace ZnErp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemOpeningBalance> ItemOpeningBalances => Set<ItemOpeningBalance>();
    public DbSet<StockDocument> StockDocuments => Set<StockDocument>();
    public DbSet<StockDocumentLine> StockDocumentLines => Set<StockDocumentLine>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<GLEntry> GLEntries => Set<GLEntry>();
    public DbSet<PettyCash> PettyCashes => Set<PettyCash>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PrimarySale> PrimarySales => Set<PrimarySale>();
    public DbSet<PrimarySaleLine> PrimarySaleLines => Set<PrimarySaleLine>();
    public DbSet<CustomerReceipt> CustomerReceipts => Set<CustomerReceipt>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 3);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Default: koi bhi cascade delete nahi (multiple cascade path errors se bachne ke liye)
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        // Document lines apne header ke saath delete hon
        b.Entity<StockDocumentLine>()
            .HasOne(l => l.StockDocument).WithMany(d => d.Lines)
            .HasForeignKey(l => l.StockDocumentId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<SaleLine>()
            .HasOne(l => l.Sale).WithMany(s => s.Lines)
            .HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<PrimarySaleLine>()
            .HasOne(l => l.PrimarySale).WithMany(s => s.Lines)
            .HasForeignKey(l => l.PrimarySaleId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Store>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Item>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Account>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.Code).IsUnique();
        b.Entity<StockDocument>().HasIndex(x => x.DocNo).IsUnique();
        b.Entity<Sale>().HasIndex(x => x.BillNo).IsUnique();
        b.Entity<AppUser>().HasIndex(x => x.UserName).IsUnique();
        b.Entity<Customer>().HasIndex(x => x.Code).IsUnique();
        b.Entity<PrimarySale>().HasIndex(x => x.InvoiceNo).IsUnique();
        b.Entity<CustomerReceipt>().HasIndex(x => x.ReceiptNo).IsUnique();
        b.Entity<ItemOpeningBalance>().HasIndex(x => new { x.StoreId, x.ItemId }).IsUnique();
        b.Entity<Payroll>().HasIndex(x => new { x.EmployeeId, x.Month }).IsUnique();
    }
}
