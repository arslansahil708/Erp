using System.ComponentModel.DataAnnotations;

namespace ZnErp.Models;

// ---------- Login / Roles ----------

public enum UserRole
{
    Admin = 1,
    Accountant = 2,
    Sales = 3,
    Store = 4
}

public class AppUser
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string UserName { get; set; } = "";
    [Required, StringLength(120)] public string FullName { get; set; } = "";
    [StringLength(250)] public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Sales;
    public bool IsActive { get; set; } = true;
    public int FailedLogins { get; set; }
    public DateTime? LockedUntil { get; set; }
}

// ---------- Account Receivable / Primary Sales ----------

/// <summary>Customer = distributor / dealer jisko Primary Sale (credit par) hoti hai.</summary>
public class Customer
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string Code { get; set; } = "";
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    [Range(0, 999999999)] public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PrimarySale
{
    public int Id { get; set; }
    [StringLength(30)] public string InvoiceNo { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int StoreId { get; set; }
    public Store? Store { get; set; }
    [StringLength(300)] public string? Remarks { get; set; }
    public decimal SubTotal { get; set; }
    [Range(0, 999999999)] public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public List<PrimarySaleLine> Lines { get; set; } = new();
}

public class PrimarySaleLine
{
    public int Id { get; set; }
    public int PrimarySaleId { get; set; }
    public PrimarySale? PrimarySale { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}

public enum PaymentMethod
{
    Cash = 1,
    Bank = 2
}

public class CustomerReceipt
{
    public int Id { get; set; }
    [ScaffoldColumn(false), StringLength(30)] public string ReceiptNo { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [Range(0.01, 999999999)] public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    [StringLength(300)] public string? Remarks { get; set; }
}
