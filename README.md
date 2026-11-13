# NEXA ERP – ASP.NET Core MVC + EF Core + SQL Server

An ERP project developed for **Visual Studio 2026 (.NET 10)**. The menu structure follows the ERP screenshots you provided:

- General Setups
- Financial Accounts
- Material Management
- Point of Sale
- Human Resource

## How to Run the Project

1. Open Visual Studio 2026 and select **File > Open > Project/Solution**.
2. Open `ZnErp.slnx` (or `ZnErp.csproj`).
3. NuGet packages will be restored automatically. If they are not restored, use **Tools > NuGet Package Manager > Restore** or run `dotnet restore`.
4. Press **F5** to run the project.

On the first run, the `ZnErpDb` database will be created automatically using SQL Server LocalDB, and sample data (stores, items, accounts, and employees) will be loaded.

To use another SQL Server instance, update `DefaultConnection` in `appsettings.json`. For example:

```text
Server=.\SQLEXPRESS;Database=ZnErpDb;Trusted_Connection=True;TrustServerCertificate=True
```

## Packages

The following packages are already included in `ZnErp.csproj`:

- Microsoft.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.SqlServer
- Microsoft.EntityFrameworkCore.Design
- Microsoft.EntityFrameworkCore.Tools

## Database Migrations (Recommended)

In `Program.cs`, the application uses `Migrate()` if migrations are available; otherwise, it uses `EnsureCreated()`.

If you want to use migrations, run the following commands **before the first run** (or drop the existing `ZnErpDb` database first).

**Package Manager Console:**

```powershell
Add-Migration InitialCreate
Update-Database
```

**Or using the terminal:**

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Whenever you modify the models, create and apply a new migration:

```powershell
Add-Migration <MigrationName>
Update-Database
```

## Project Modules

| Menu | Features |
|---|---|
| General Setups | Stores, Items, Chart of Accounts, Employees |
| Financial Accounts | Petty Cash Entry (automatic GL posting), Journal Entry, GL Report, Expenses Detail Report |
| Material Management | Items Opening Balances, Production Order, GRN, Goods Return, Transfer IN/OUT, Stock Audit, Stock Balance |
| Point of Sale | New Sale (Billing), Sales List, 10 POS Reports |
| Human Resource | Employees, Payroll |

**The 10 POS Reports:**

1. Consolidated Report
2. Customer Order History
3. Daily Sales Report
4. Day End Report
5. Delivery Dispatch Report
6. Discount Report
7. Item Wise Sales Report
8. Petty Cash Report
9. Sale Transaction Report
10. Trend Analysis Report

**Stock Balance Calculation:**

Stock Balance = Opening + (GRN + Transfer IN + Production + Audit Plus) − (Return + Transfer OUT + Sales + Audit Minus).

## Login and User Roles

On the first run, a default administrator account is created:

- **Username:** `admin`
- **Password:** `Admin@123`

Immediately after logging in, change your password using the **Change Password** option in the top-right corner.

| Role | Permissions |
|---|---|
| Admin | Full access, Users & Roles, delete bills/invoices, and post old bills to GL |
| Accountant | Chart of Accounts, Journal, Petty Cash, Payroll/Employees, Customer Receipts, AR Reports, and all reports (including sales and stock reports) |
| Sales | New POS Sales, Primary Sales (create/edit), Customers, Customer Ledger, Receivable Aging, and POS Reports |
| Store | Stores, Items, Opening Balances, Stock Documents (GRN, Transfer, Audit, etc.), and Stock Balance |

**Security Features:**

- After 5 incorrect password attempts, the account is locked for 10 minutes.
- Admin can create new users, change roles, deactivate accounts, and reset passwords through Users & Roles.
- When a user is deactivated or their role is changed, their active session is terminated immediately.

## Primary Sales and Accounts Receivable

- **Customers:** Distributors and dealers with configurable credit limits. A credit limit of 0 means there is no credit limit.
- **Primary Sales:** Create bulk credit invoices for customers (e.g., `PS-000001`). Stock decreases and accounts receivable increases. The system prevents saving if the credit limit is exceeded.
- **Customer Receipts:** Record customer payments (`RC-000001`) through Cash or Bank.
- **Customer Ledger:** View customer transaction history.
- **Receivable Aging:** View outstanding balances in the following categories:
  - 0–30 days
  - 31–60 days
  - 61–90 days
  - 90+ days

Payments are automatically allocated to the oldest outstanding invoices first.

## General Ledger (GL) Automatic Posting

| Document | GL Entries |
|---|---|
| POS Sale (`SL-`) | Dr Petty Cash (1001), Dr Sales Discount (5007), Cr Sales Revenue (4001); Dr COGS (5006), Cr Inventory (1004) |
| Primary Sale (`PS-`) | Dr Accounts Receivable (1003), Dr Sales Discount (5007), Cr Sales Revenue (4001); Dr COGS (5006), Cr Inventory (1004) |
| Customer Receipt (`RC-`) | Dr Cash (1001) or Bank (