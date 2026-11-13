using ZnErp.Models;
using ZnErp.Services;

namespace ZnErp.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (!db.Stores.Any())
        {
            db.Stores.AddRange(
                new Store { Code = "MAIN", Name = "Main Store", Location = "Head Office" },
                new Store { Code = "OUT1", Name = "Outlet 1", Location = "Branch" });
            db.SaveChanges();
        }

        if (!db.Accounts.Any())
        {
            db.Accounts.AddRange(
                new Account { Code = "1001", Name = "Petty Cash", AccountType = AccountType.Asset },
                new Account { Code = "1002", Name = "Bank", AccountType = AccountType.Asset },
                new Account { Code = "2001", Name = "Accounts Payable", AccountType = AccountType.Liability },
                new Account { Code = "3001", Name = "Owner Equity", AccountType = AccountType.Equity },
                new Account { Code = "4001", Name = "Sales Revenue", AccountType = AccountType.Income },
                new Account { Code = "5001", Name = "Utilities Expense", AccountType = AccountType.Expense },
                new Account { Code = "5002", Name = "Salaries Expense", AccountType = AccountType.Expense },
                new Account { Code = "5003", Name = "Repair & Maintenance", AccountType = AccountType.Expense },
                new Account { Code = "5004", Name = "Transport & Fuel", AccountType = AccountType.Expense },
                new Account { Code = "5005", Name = "Miscellaneous Expense", AccountType = AccountType.Expense });
            db.SaveChanges();
        }

        if (!db.Items.Any())
        {
            db.Items.AddRange(
                new Item { Code = "ITM001", Name = "Zinger Burger", Unit = "Pcs", CostPrice = 220, SalePrice = 450 },
                new Item { Code = "ITM002", Name = "French Fries", Unit = "Pcs", CostPrice = 80, SalePrice = 200 },
                new Item { Code = "ITM003", Name = "Cold Drink 345ml", Unit = "Pcs", CostPrice = 50, SalePrice = 80 },
                new Item { Code = "ITM004", Name = "Chicken Patty (Raw)", Unit = "Pcs", CostPrice = 120, SalePrice = 0 },
                new Item { Code = "ITM005", Name = "Burger Bun", Unit = "Pcs", CostPrice = 25, SalePrice = 0 });
            db.SaveChanges();

            var main = db.Stores.First(s => s.Code == "MAIN");
            foreach (var item in db.Items.ToList())
            {
                db.ItemOpeningBalances.Add(new ItemOpeningBalance
                {
                    StoreId = main.Id,
                    ItemId = item.Id,
                    Quantity = 100,
                    Rate = item.CostPrice
                });
            }
            db.SaveChanges();
        }

        // Auto-posting ke system accounts (purane database mein bhi bante hain)
        foreach (var (code, d) in GlCodes.Defaults)
        {
            if (!db.Accounts.Any(a => a.Code == code))
                db.Accounts.Add(new Account { Code = code, Name = d.Name, AccountType = d.Type });
        }
        db.SaveChanges();

        // Pehla admin user. LOGIN KE BAAD PASSWORD FORAN BADAL DEIN.
        if (!db.AppUsers.Any())
        {
            db.AppUsers.Add(new AppUser
            {
                UserName = "admin",
                FullName = "Administrator",
                Role = UserRole.Admin,
                PasswordHash = PasswordService.Hash("Admin@123")
            });
            db.SaveChanges();
        }

        if (!db.Employees.Any())
        {
            db.Employees.AddRange(
                new Employee { Code = "E001", Name = "Faisal Amir Uddin", Designation = "Shift Manager", Department = "Operation", BasicSalary = 60000, JoinDate = new DateTime(2024, 1, 1) },
                new Employee { Code = "E002", Name = "Sample Cashier", Designation = "Cashier", Department = "Operation", BasicSalary = 38000, JoinDate = new DateTime(2025, 3, 1) });
            db.SaveChanges();
        }
    }
}
