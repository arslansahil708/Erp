namespace ZnErp.Services;

/// <summary>[Authorize(Roles = ...)] mein use hone wale role naam. UserRole enum ke naam se match karte hain.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Accountant = "Accountant";
    public const string Sales = "Sales";
    public const string Store = "Store";

    public const string AdminAccountant = Admin + "," + Accountant;
    public const string AdminSales = Admin + "," + Sales;
    public const string AdminStore = Admin + "," + Store;
    public const string AdminAccountantSales = Admin + "," + Accountant + "," + Sales;
    public const string AdminAccountantStore = Admin + "," + Accountant + "," + Store;
}
