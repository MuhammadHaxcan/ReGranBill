using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync()) return; // Already seeded

        // Users
        var admin = new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            FullName = "Administrator",
            Role = UserRole.Admin
        };
        var op = new User
        {
            Username = "operator",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Operator123!"),
            FullName = "Operator User",
            Role = UserRole.Operator
        };
        db.Users.AddRange(admin, op);
        await db.SaveChangesAsync();

        // Categories
        var cats = new[]
        {
            new Category { Name = "Raw Materials" },
            new Category { Name = "Finished Goods" },
            new Category { Name = "Packaging" },
            new Category { Name = "Transport" },
            new Category { Name = "Office Supplies" },
            new Category { Name = "Utilities" },
        };
        db.Categories.AddRange(cats);
        await db.SaveChangesAsync();

        // Accounts + detail rows
        // Products
        var products = new (string Name, int CatIdx, string Packing, decimal Weight)[]
        {
            ("HDPE Blue Drum", 1, "50kg / Bag", 50),
            ("PP-125 Natural", 1, "25kg / Bag", 25),
            ("LDPE Film Grade", 1, "25kg / Bag", 25),
        };
        foreach (var p in products)
        {
            var acct = new Account { Name = p.Name, CategoryId = cats[p.CatIdx].Id, AccountType = AccountType.Product };
            db.Accounts.Add(acct);
            await db.SaveChangesAsync();
            db.ProductDetails.Add(new ProductDetail { AccountId = acct.Id, Packing = p.Packing, PackingWeightKg = p.Weight, Unit = "kg" });
        }

        // Expenses
        db.Accounts.Add(new Account { Name = "Electricity", CategoryId = cats[5].Id, AccountType = AccountType.Expense });
        db.Accounts.Add(new Account { Name = "Diesel Fuel", CategoryId = cats[3].Id, AccountType = AccountType.Expense });
        await db.SaveChangesAsync();

        // Bank/Account type
        var hbl = new Account { Name = "HBL Current Account", CategoryId = cats[0].Id, AccountType = AccountType.Account };
        db.Accounts.Add(hbl);
        await db.SaveChangesAsync();
        db.BankDetails.Add(new BankDetail { AccountId = hbl.Id, AccountNumber = "1234-5678-0001", BankName = "Habib Bank Ltd" });

        var cash = new Account { Name = "Cash In Hand", CategoryId = cats[0].Id, AccountType = AccountType.Account };
        db.Accounts.Add(cash);
        await db.SaveChangesAsync();
        db.BankDetails.Add(new BankDetail { AccountId = cash.Id });

        // Parties
        var parties = new (string Name, int CatIdx, PartyRole Role, string Contact, string Phone, string City, string Address)[]
        {
            ("Akhtar Plastics", 1, PartyRole.Customer, "M. Akhtar", "0300-1234567", "Lahore", "Shah Alam Market, Lahore"),
            ("Bilal Industries", 1, PartyRole.Customer, "Bilal Ahmed", "0321-9876543", "Faisalabad", "Industrial Estate, Faisalabad"),
            ("Crescent Polymers", 0, PartyRole.Vendor, "Imran Shah", "0333-5556677", "Karachi", "SITE Area, Karachi"),
            ("Delta Packaging", 2, PartyRole.Both, "Salman Raza", "0345-1112233", "Multan", "Bosan Road, Multan"),
            ("Al-Madina Logistics", 3, PartyRole.Transporter, "Tariq Mehmood", "0301-7778899", "Multan", "GT Road, Multan"),
            ("Faisal Goods Transport", 3, PartyRole.Transporter, "Faisal Khan", "0312-4445566", "Lahore", "Badami Bagh, Lahore"),
            ("Speed Cargo", 3, PartyRole.Transporter, "Naveed Ali", "0322-8889900", "Karachi", "Port Area, Karachi"),
        };
        foreach (var p in parties)
        {
            var acct = new Account { Name = p.Name, CategoryId = cats[p.CatIdx].Id, AccountType = AccountType.Party };
            db.Accounts.Add(acct);
            await db.SaveChangesAsync();
            db.PartyDetails.Add(new PartyDetail
            {
                AccountId = acct.Id,
                PartyRole = p.Role,
                ContactPerson = p.Contact,
                Phone = p.Phone,
                City = p.City,
                Address = p.Address
            });
        }

        // DC Number Sequence
        db.DcNumberSequences.Add(new DcNumberSequence { LastNumber = 42 });
        await db.SaveChangesAsync();
    }
}
