using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        await EnsureUsersAsync(db);

        //var categories = await EnsureCategoriesAsync(db);

        //await EnsureInventoryAccountsAsync(db, categories);
        //await EnsureExpenseAccountsAsync(db, categories);
        //await EnsureBankAccountsAsync(db, categories);
        //await EnsurePartyAccountsAsync(db, categories);
    }

    private static async Task EnsureUsersAsync(AppDbContext db)
    {
        var adminRole = await EnsureAdminRoleAsync(db);

        if (!await db.Users.AnyAsync(u => u.Username == "admin"))
        {
            db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                FullName = "Administrator",
                RoleId = adminRole.Id
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<Role> EnsureAdminRoleAsync(AppDbContext db)
    {
        var admin = await db.Roles.FirstOrDefaultAsync(r => r.IsAdmin);
        if (admin != null) return admin;

        admin = new Role
        {
            Name = "Admin",
            IsSystem = true,
            IsAdmin = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
            UpdatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        db.Roles.Add(admin);
        await db.SaveChangesAsync();
        return admin;
    }

    private static async Task<Dictionary<string, Category>> EnsureCategoriesAsync(AppDbContext db)
    {
        var categoryNames = new[]
        {
            "Raw Materials",
            "Finished Goods",
            "Packaging",
            "Transport",
            "Office Supplies",
            "Utilities"
        };

        foreach (var categoryName in categoryNames)
        {
            if (!await db.Categories.AnyAsync(category => category.Name == categoryName))
            {
                db.Categories.Add(new Category { Name = categoryName });
            }
        }

        await db.SaveChangesAsync();

        return await db.Categories
            .ToDictionaryAsync(category => category.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task EnsureInventoryAccountsAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var inventoryItems = new[]
        {
            new InventorySeed("HDPE Blue Drum", "Finished Goods", AccountType.Product, "50kg / Bag", 50m),
            new InventorySeed("PP-125 Natural", "Finished Goods", AccountType.Product, "25kg / Bag", 25m),
            new InventorySeed("LDPE Film Grade", "Finished Goods", AccountType.Product, "25kg / Bag", 25m),
            new InventorySeed("HDPE Dana Black", "Finished Goods", AccountType.Product, "25kg / Bag", 25m),
            new InventorySeed("PP Raffia Granules", "Finished Goods", AccountType.Product, "25kg / Bag", 25m),
            new InventorySeed("Plastic Pallets", "Packaging", AccountType.Product, "1 Piece", 18m),
            new InventorySeed("Wooden Pallets", "Packaging", AccountType.Product, "1 Piece", 22m),
            new InventorySeed("HDPE Scrap Natural", "Raw Materials", AccountType.RawMaterial, "50kg / Bag", 50m),
            new InventorySeed("LDPE Scrap Mix", "Raw Materials", AccountType.RawMaterial, "40kg / Bag", 40m),
            new InventorySeed("PP Regrind", "Raw Materials", AccountType.RawMaterial, "30kg / Bag", 30m),
            new InventorySeed("PET Flakes Clear", "Raw Materials", AccountType.RawMaterial, "35kg / Bag", 35m),
            new InventorySeed("Color Masterbatch", "Raw Materials", AccountType.RawMaterial, "25kg / Bag", 25m),
            new InventorySeed("Calcium Filler", "Raw Materials", AccountType.RawMaterial, "25kg / Bag", 25m)
        };

        foreach (var item in inventoryItems)
        {
            var category = categories[item.CategoryName];
            var account = await db.Accounts
                .Include(existing => existing.ProductDetail)
                .FirstOrDefaultAsync(existing => existing.Name == item.Name);

            if (account == null)
            {
                account = new Account
                {
                    Name = item.Name,
                    CategoryId = category.Id,
                    AccountType = item.AccountType
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync();
            }
            else
            {
                account.CategoryId = category.Id;
                account.AccountType = item.AccountType;
                await db.SaveChangesAsync();
            }

            if (account.ProductDetail == null)
            {
                    db.ProductDetails.Add(new ProductDetail
                    {
                        AccountId = account.Id,
                        Packing = item.Packing,
                        PackingWeightKg = item.WeightKg
                    });
                }
                else
                {
                    account.ProductDetail.Packing = item.Packing;
                    account.ProductDetail.PackingWeightKg = item.WeightKg;
                }

            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureExpenseAccountsAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var expenses = new[]
        {
            new AccountSeed("Electricity", "Utilities", AccountType.Expense),
            new AccountSeed("Diesel Fuel", "Transport", AccountType.Expense),
            new AccountSeed("Labour Charges", "Office Supplies", AccountType.Expense)
        };

        foreach (var expense in expenses)
        {
            if (await db.Accounts.AnyAsync(account => account.Name == expense.Name))
            {
                continue;
            }

            db.Accounts.Add(new Account
            {
                Name = expense.Name,
                CategoryId = categories[expense.CategoryName].Id,
                AccountType = expense.AccountType
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureBankAccountsAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        await EnsureBankAccountAsync(db, categories["Raw Materials"].Id, "HBL Current Account", "1234-5678-0001", "Habib Bank Ltd");
        await EnsureBankAccountAsync(db, categories["Raw Materials"].Id, "Cash In Hand", null, null);
    }

    private static async Task EnsureBankAccountAsync(
        AppDbContext db,
        int categoryId,
        string accountName,
        string? accountNumber,
        string? bankName)
    {
        var account = await db.Accounts
            .Include(existing => existing.BankDetail)
            .FirstOrDefaultAsync(existing => existing.Name == accountName);

        if (account == null)
        {
            account = new Account
            {
                Name = accountName,
                CategoryId = categoryId,
                AccountType = AccountType.Account
            };

            db.Accounts.Add(account);
            await db.SaveChangesAsync();
        }
        else
        {
            account.CategoryId = categoryId;
            account.AccountType = AccountType.Account;
            await db.SaveChangesAsync();
        }

        if (account.BankDetail == null)
        {
            db.BankDetails.Add(new BankDetail
            {
                AccountId = account.Id,
                AccountNumber = accountNumber,
                BankName = bankName
            });
        }
        else
        {
            account.BankDetail.AccountNumber = accountNumber;
            account.BankDetail.BankName = bankName;
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsurePartyAccountsAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var parties = new[]
        {
            new PartySeed("Akhtar Plastics", "Finished Goods", PartyRole.Customer, "M. Akhtar", "0300-1234567", "Lahore", "Shah Alam Market, Lahore"),
            new PartySeed("Bilal Industries", "Finished Goods", PartyRole.Customer, "Bilal Ahmed", "0321-9876543", "Faisalabad", "Industrial Estate, Faisalabad"),
            new PartySeed("Crescent Polymers", "Raw Materials", PartyRole.Vendor, "Imran Shah", "0333-5556677", "Karachi", "SITE Area, Karachi"),
            new PartySeed("Delta Packaging", "Packaging", PartyRole.Both, "Salman Raza", "0345-1112233", "Multan", "Bosan Road, Multan"),
            new PartySeed("Al-Madina Logistics", "Transport", PartyRole.Transporter, "Tariq Mehmood", "0301-7778899", "Multan", "GT Road, Multan"),
            new PartySeed("Faisal Goods Transport", "Transport", PartyRole.Transporter, "Faisal Khan", "0312-4445566", "Lahore", "Badami Bagh, Lahore"),
            new PartySeed("Speed Cargo", "Transport", PartyRole.Transporter, "0322-8889900", "Naveed Ali", "Karachi", "Port Area, Karachi")
        };

        foreach (var party in parties)
        {
            var account = await db.Accounts
                .Include(existing => existing.PartyDetail)
                .FirstOrDefaultAsync(existing => existing.Name == party.Name);

            if (account == null)
            {
                account = new Account
                {
                    Name = party.Name,
                    CategoryId = categories[party.CategoryName].Id,
                    AccountType = AccountType.Party
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync();
            }
            else
            {
                account.CategoryId = categories[party.CategoryName].Id;
                account.AccountType = AccountType.Party;
                await db.SaveChangesAsync();
            }

            if (account.PartyDetail == null)
            {
                db.PartyDetails.Add(new PartyDetail
                {
                    AccountId = account.Id,
                    PartyRole = party.Role,
                    ContactPerson = party.Contact,
                    Phone = party.Phone,
                    City = party.City,
                    Address = party.Address
                });
            }
            else
            {
                account.PartyDetail.PartyRole = party.Role;
                account.PartyDetail.ContactPerson = party.Contact;
                account.PartyDetail.Phone = party.Phone;
                account.PartyDetail.City = party.City;
                account.PartyDetail.Address = party.Address;
            }

            await db.SaveChangesAsync();
        }
    }

    private sealed record InventorySeed(string Name, string CategoryName, AccountType AccountType, string Packing, decimal WeightKg);
    private sealed record AccountSeed(string Name, string CategoryName, AccountType AccountType);
    private sealed record PartySeed(string Name, string CategoryName, PartyRole Role, string Contact, string Phone, string City, string Address);
}
