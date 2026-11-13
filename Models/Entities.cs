using System.ComponentModel.DataAnnotations;

namespace ZnErp.Models;

// ---------- General Setups / Material Management definitions ----------

public class Store
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string Code { get; set; } = "";
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(200)] public string? Location { get; set; }
}

public class Item
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string Code { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, StringLength(20)] public string Unit { get; set; } = "Pcs";
    [Range(0, 999999999)] public decimal CostPrice { get; set; }
    [Range(0, 999999999)] public decimal SalePrice { get; set; }
}

public class ItemOpeningBalance
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public Store? Store { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    [Range(0, 999999999)] public decimal Quantity { get; set; }
    [Range(0, 999999999)] public decimal Rate { get; set; }
}

// ---------- Material Management entries ----------

public enum StockDocType
{
    ProductionOrder = 1,
    GoodsReceive = 2,
    GoodsReturn = 3,
    TransferIn = 4,
    TransferOut = 5,
    StockAudit = 6
}

public class StockDocument
{
    public int Id { get; set; }
    [StringLength(30)] public string DocNo { get; set; } = "";
    public StockDocType DocType { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public int StoreId { get; set; }
    public Store? Store { get; set; }
    public int? OtherStoreId { get; set; }
    public Store? OtherStore { get; set; }
    [StringLength(300)] public string? Remarks { get; set; }
    public List<StockDocumentLine> Lines { get; set; } = new();
}

public class StockDocumentLine
{
    public int Id { get; set; }
    public int StockDocumentId { get; set; }
    public StockDocument? StockDocument { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
}

// ---------- Financial Accounts ----------

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Income = 4,
    Expense = 5
}

public class Account
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string Code { get; set; } = "";
    [Required, StringLength(120)] public string Name { get; set; } = "";
    public AccountType AccountType { get; set; } = AccountType.Expense;
}

public class GLEntry
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public int AccountId { get; set; }
    public Account? Account { get; set; }
    [Range(0, 999999999)] public decimal Debit { get; set; }
    [Range(0, 999999999)] public decimal Credit { get; set; }
    [StringLength(300)] public string? Narration { get; set; }
    [StringLength(30)] public string? Reference { get; set; }
}

public class PettyCash
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public int ExpenseAccountId { get; set; }
    public Account? ExpenseAccount { get; set; }
    [Required, StringLength(300)] public string Description { get; set; } = "";
    [Range(0.01, 999999999)] public decimal Amount { get; set; }
}

// ---------- Point of Sale ----------

public class Sale
{
    public int Id { get; set; }
    [StringLength(30)] public string BillNo { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    [StringLength(120)] public string? CustomerName { get; set; }
    public int StoreId { get; set; }
    public Store? Store { get; set; }
    public bool IsDelivery { get; set; }
    [StringLength(300)] public string? DeliveryAddress { get; set; }
    public decimal SubTotal { get; set; }
    [Range(0, 999999999)] public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public List<SaleLine> Lines { get; set; } = new();
}

public class SaleLine
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}

// ---------- Human Resource / Payroll ----------

public class Employee
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string Code { get; set; } = "";
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [StringLength(80)] public string? Designation { get; set; }
    [StringLength(80)] public string? Department { get; set; }
    [Range(0, 999999999)] public decimal BasicSalary { get; set; }
    public DateTime JoinDate { get; set; } = DateTime.Today;
    public bool IsActive { get; set; } = true;
}

public class Payroll
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    [Required, RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Month format YYYY-MM hona chahiye (e.g. 2026-11)")]
    public string Month { get; set; } = DateTime.Today.ToString("yyyy-MM");
    [ScaffoldColumn(false)] public decimal BasicSalary { get; set; }
    [Range(0, 999999999)] public decimal Allowances { get; set; }
    [Range(0, 999999999)] public decimal Deductions { get; set; }
    [ScaffoldColumn(false)] public decimal NetPay { get; set; }
}
