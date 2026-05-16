using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Data;

public static class SeedData
{
    // Toggle any section on/off without commenting code. Set to false to skip.
    private const bool SeedCategoriesEnabled       = true;
    private const bool SeedRawMaterialsEnabled     = true;
    private const bool SeedUnwashedMaterialsEnabled= true;
    private const bool SeedAdditivesEnabled        = true;
    private const bool SeedFinishedGoodsEnabled    = true;
    private const bool SeedPackagingEnabled        = true;
    private const bool SeedUtilityExpensesEnabled  = true;
    private const bool SeedTransportExpensesEnabled= true;
    private const bool SeedLabourExpensesEnabled   = true;
    private const bool SeedMaintenanceExpensesEnabled = true;
    private const bool SeedOfficeExpensesEnabled   = true;
    private const bool SeedBanksAndCashEnabled     = true;
    private const bool SeedCustomersEnabled        = true;
    private const bool SeedVendorsEnabled          = true;
    private const bool SeedTransportersEnabled     = true;

    // Category names — central constants so every section references the same string.
    private static class Cat
    {
        public const string RawMaterials      = "Raw Materials";
        public const string UnwashedMaterials = "Unwashed Materials";
        public const string Additives         = "Additives";
        public const string FinishedGoods     = "Finished Goods";
        public const string Packaging         = "Packaging";
        public const string Utilities         = "Utilities";
        public const string Transport         = "Transport";
        public const string Labour            = "Labour & Wages";
        public const string Maintenance       = "Maintenance";
        public const string Office            = "Office & Admin";
        public const string BanksAndCash      = "Banks & Cash";
        public const string Customers         = "Customers";
        public const string Vendors           = "Vendors";
        public const string Transporters      = "Transporters";
    }

    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        await EnsureUsersAsync(db);

        var categories = SeedCategoriesEnabled
            ? await EnsureCategoriesAsync(db)
            : await LoadExistingCategoriesAsync(db);

        if (SeedRawMaterialsEnabled)      await EnsureRawMaterialsAsync(db, categories);
        if (SeedUnwashedMaterialsEnabled) await EnsureUnwashedMaterialsAsync(db, categories);
        if (SeedAdditivesEnabled)         await EnsureAdditivesAsync(db, categories);
        if (SeedFinishedGoodsEnabled)     await EnsureFinishedGoodsAsync(db, categories);
        if (SeedPackagingEnabled)         await EnsurePackagingAsync(db, categories);

        if (SeedUtilityExpensesEnabled)     await EnsureUtilityExpensesAsync(db, categories);
        if (SeedTransportExpensesEnabled)   await EnsureTransportExpensesAsync(db, categories);
        if (SeedLabourExpensesEnabled)      await EnsureLabourExpensesAsync(db, categories);
        if (SeedMaintenanceExpensesEnabled) await EnsureMaintenanceExpensesAsync(db, categories);
        if (SeedOfficeExpensesEnabled)      await EnsureOfficeExpensesAsync(db, categories);

        if (SeedBanksAndCashEnabled)  await EnsureBanksAndCashAsync(db, categories);
        if (SeedCustomersEnabled)     await EnsureCustomersAsync(db, categories);
        if (SeedVendorsEnabled)       await EnsureVendorsAsync(db, categories);
        if (SeedTransportersEnabled)  await EnsureTransportersAsync(db, categories);
    }

    // ---------- Users / Roles ----------

    private static async Task EnsureUsersAsync(AppDbContext db)
    {
        var adminRole = await EnsureAdminRoleAsync(db);
        await EnsureAdminRolePagesAsync(db, adminRole);

        var adminUsername = Environment.GetEnvironmentVariable("SeedAdmin__Username")?.Trim();
        if (string.IsNullOrWhiteSpace(adminUsername)) adminUsername = "admin";

        var adminFullName = Environment.GetEnvironmentVariable("SeedAdmin__FullName")?.Trim();
        if (string.IsNullOrWhiteSpace(adminFullName)) adminFullName = "Administrator";

        var adminPassword = Environment.GetEnvironmentVariable("SeedAdmin__Password");
        if (string.IsNullOrWhiteSpace(adminPassword)) adminPassword = "Admin123!";

        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Username == adminUsername);
        if (adminUser == null)
        {
            db.Users.Add(new User
            {
                Username = adminUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                FullName = adminFullName,
                RoleId = adminRole.Id,
                IsActive = true
            });
        }
        else
        {
            adminUser.RoleId = adminRole.Id;
            adminUser.IsActive = true;
            if (string.IsNullOrWhiteSpace(adminUser.FullName))
            {
                adminUser.FullName = adminFullName;
            }
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

    private static async Task EnsureAdminRolePagesAsync(AppDbContext db, Role adminRole)
    {
        var existingPageKeys = await db.RolePages
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PageKey)
            .ToListAsync();

        var missingPageKeys = PageCatalog.All
            .Select(page => page.Key)
            .Except(existingPageKeys, StringComparer.Ordinal)
            .ToList();

        if (missingPageKeys.Count == 0) return;

        foreach (var pageKey in missingPageKeys)
        {
            db.RolePages.Add(new RolePage { RoleId = adminRole.Id, PageKey = pageKey });
        }
        await db.SaveChangesAsync();
    }

    // ---------- Categories ----------

    private static async Task<Dictionary<string, Category>> EnsureCategoriesAsync(AppDbContext db)
    {
        var categoryNames = new[]
        {
            Cat.RawMaterials, Cat.UnwashedMaterials, Cat.Additives, Cat.FinishedGoods, Cat.Packaging,
            Cat.Utilities, Cat.Transport, Cat.Labour, Cat.Maintenance, Cat.Office,
            Cat.BanksAndCash, Cat.Customers, Cat.Vendors, Cat.Transporters
        };

        foreach (var name in categoryNames)
        {
            if (!await db.Categories.AnyAsync(c => c.Name == name))
            {
                db.Categories.Add(new Category { Name = name });
            }
        }
        await db.SaveChangesAsync();

        return await db.Categories.ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, Category>> LoadExistingCategoriesAsync(AppDbContext db)
        => await db.Categories.ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase);

    // ---------- Raw Materials (washed scrap going into extruder) ----------

    private static async Task EnsureRawMaterialsAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var items = new[]
        {
            new ProductSeed("HDPE Scrap Natural Washed", AccountType.RawMaterial, "50kg / Bag", 50m),
            new ProductSeed("HDPE Scrap Black Washed",   AccountType.RawMaterial, "50kg / Bag", 50m),
            new ProductSeed("HDPE Scrap Blue Washed",    AccountType.RawMaterial, "50kg / Bag", 50m),
            new ProductSeed("LDPE Scrap Washed",         AccountType.RawMaterial, "40kg / Bag", 40m),
            new ProductSeed("PP Scrap Washed",           AccountType.RawMaterial, "30kg / Bag", 30m),
            new ProductSeed("PP Raffia Washed",          AccountType.RawMaterial, "25kg / Bag", 25m),
            new ProductSeed("PET Flakes Clear",          AccountType.RawMaterial, "35kg / Bag", 35m),
            new ProductSeed("PET Flakes Green",          AccountType.RawMaterial, "35kg / Bag", 35m),
            new ProductSeed("ABS Regrind",               AccountType.RawMaterial, "30kg / Bag", 30m)
        };
        await UpsertProductsAsync(db, categories[Cat.RawMaterials].Id, items);
    }

    // ---------- Unwashed Materials (pre-washing scrap) ----------

    private static async Task EnsureUnwashedMaterialsAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var items = new[]
        {
            new ProductSeed("HDPE Scrap Natural Unwashed", AccountType.UnwashedMaterial, "50kg / Bag", 50m),
            new ProductSeed("HDPE Scrap Black Unwashed",   AccountType.UnwashedMaterial, "50kg / Bag", 50m),
            new ProductSeed("HDPE Scrap Blue Unwashed",    AccountType.UnwashedMaterial, "50kg / Bag", 50m),
            new ProductSeed("LDPE Scrap Unwashed",         AccountType.UnwashedMaterial, "40kg / Bag", 40m),
            new ProductSeed("PP Scrap Unwashed",           AccountType.UnwashedMaterial, "30kg / Bag", 30m),
            new ProductSeed("PP Raffia Unwashed",          AccountType.UnwashedMaterial, "25kg / Bag", 25m)
        };
        await UpsertProductsAsync(db, categories[Cat.UnwashedMaterials].Id, items);
    }

    // ---------- Additives (colors, calcium, lumps, stabilizers) ----------

    private static async Task EnsureAdditivesAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var items = new[]
        {
            new ProductSeed("Color Masterbatch Black",  AccountType.RawMaterial, "25kg / Bag", 25m),
            new ProductSeed("Color Masterbatch White",  AccountType.RawMaterial, "25kg / Bag", 25m),
            new ProductSeed("Color Masterbatch Blue",   AccountType.RawMaterial, "25kg / Bag", 25m),
            new ProductSeed("Color Masterbatch Red",    AccountType.RawMaterial, "25kg / Bag", 25m),
            new ProductSeed("Color Masterbatch Yellow", AccountType.RawMaterial, "25kg / Bag", 25m),
            new ProductSeed("Color Masterbatch Green",  AccountType.RawMaterial, "25kg / Bag", 25m),
            new ProductSeed("Calcium Carbonate Filler", AccountType.RawMaterial, "25kg / Bag", 25m),
            new ProductSeed("Plastic Lumps Mixed",      AccountType.RawMaterial, "50kg / Bag", 50m),
            new ProductSeed("Plastic Lumps HDPE",       AccountType.RawMaterial, "50kg / Bag", 50m),
            new ProductSeed("Plastic Lumps PP",         AccountType.RawMaterial, "50kg / Bag", 50m),
            new ProductSeed("Anti-Oxidant",             AccountType.RawMaterial, "20kg / Bag", 20m),
            new ProductSeed("UV Stabilizer",            AccountType.RawMaterial, "20kg / Bag", 20m),
            new ProductSeed("Lubricant Wax",            AccountType.RawMaterial, "25kg / Bag", 25m)
        };
        await UpsertProductsAsync(db, categories[Cat.Additives].Id, items);
    }

    // ---------- Finished Goods (Dana / Granules) ----------

    private static async Task EnsureFinishedGoodsAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var items = new[]
        {
            new ProductSeed("HDPE Dana Natural",          AccountType.Product, "25kg / Bag", 25m),
            new ProductSeed("HDPE Dana Black",            AccountType.Product, "25kg / Bag", 25m),
            new ProductSeed("HDPE Dana Blue",             AccountType.Product, "25kg / Bag", 25m),
            new ProductSeed("HDPE Blow Grade Dana",       AccountType.Product, "25kg / Bag", 25m),
            new ProductSeed("LDPE Dana Natural",          AccountType.Product, "25kg / Bag", 25m),
            new ProductSeed("LDPE Dana Black",            AccountType.Product, "25kg / Bag", 25m),
            new ProductSeed("LDPE Film Grade Dana",       AccountType.Product, "25kg / Bag", 25m),
            new ProductSeed("PP Dana Natural",            AccountType.Product, "25kg / Bag", 25m),
            new ProductSeed("PP Dana Black",              AccountType.Product, "25kg / Bag", 25m),
            new ProductSeed("PP Dana Mixed Color",        AccountType.Product, "25kg / Bag", 25m),
            new ProductSeed("PP Raffia Dana",             AccountType.Product, "25kg / Bag", 25m),
            new ProductSeed("Reprocessed Granules Mixed", AccountType.Product, "25kg / Bag", 25m)
        };
        await UpsertProductsAsync(db, categories[Cat.FinishedGoods].Id, items);
    }

    // ---------- Packaging ----------

    private static async Task EnsurePackagingAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var items = new[]
        {
            new ProductSeed("HDPE Woven Bag 25kg", AccountType.Product, "1 Piece", 0.20m),
            new ProductSeed("HDPE Woven Bag 50kg", AccountType.Product, "1 Piece", 0.35m),
            new ProductSeed("PP Liner Bag",        AccountType.Product, "1 Piece", 0.10m),
            new ProductSeed("Wooden Pallet",       AccountType.Product, "1 Piece", 22m),
            new ProductSeed("Plastic Pallet",      AccountType.Product, "1 Piece", 18m),
            new ProductSeed("Stretch Film Roll",   AccountType.Product, "1 Roll",  3m)
        };
        await UpsertProductsAsync(db, categories[Cat.Packaging].Id, items);
    }

    // ---------- Expense accounts ----------

    private static async Task EnsureUtilityExpensesAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
        => await UpsertExpensesAsync(db, categories[Cat.Utilities].Id, new[]
        {
            "Electricity (WAPDA)",
            "Sui Gas",
            "Water Supply",
            "Generator Diesel"
        });

    private static async Task EnsureTransportExpensesAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
        => await UpsertExpensesAsync(db, categories[Cat.Transport].Id, new[]
        {
            "Inward Freight",
            "Outward Freight",
            "Vehicle Fuel",
            "Vehicle Repair"
        });

    private static async Task EnsureLabourExpensesAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
        => await UpsertExpensesAsync(db, categories[Cat.Labour].Id, new[]
        {
            "Production Labour",
            "Washing Line Labour",
            "Loading/Unloading Labour",
            "Salaries & Wages",
            "Overtime"
        });

    private static async Task EnsureMaintenanceExpensesAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
        => await UpsertExpensesAsync(db, categories[Cat.Maintenance].Id, new[]
        {
            "Machinery Repair",
            "Spare Parts",
            "Die & Mold Maintenance",
            "Cutter Blade Replacement",
            "Building Repair"
        });

    private static async Task EnsureOfficeExpensesAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
        => await UpsertExpensesAsync(db, categories[Cat.Office].Id, new[]
        {
            "Office Stationery",
            "Internet & Phone",
            "Printing & Postage",
            "Misc Expenses"
        });

    // ---------- Banks & Cash ----------

    private static async Task EnsureBanksAndCashAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var categoryId = categories[Cat.BanksAndCash].Id;
        var banks = new (string Name, string? AccountNumber, string? BankName)[]
        {
            ("Cash In Hand",                  null, null),
            ("Petty Cash",                    null, null),
            ("HBL Current Account",           "1234-5678-0001", "Habib Bank Ltd"),
            ("Meezan Bank Current Account",   "9876-5432-0002", "Meezan Bank"),
            ("Allied Bank Current",           "5555-6666-0003", "Allied Bank Ltd")
        };

        foreach (var bank in banks)
        {
            await UpsertBankAccountAsync(db, categoryId, bank.Name, bank.AccountNumber, bank.BankName);
        }
    }

    // ---------- Parties: Customers / Vendors / Transporters ----------

    private static async Task EnsureCustomersAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var categoryId = categories[Cat.Customers].Id;
        var parties = new[]
        {
            new PartySeed("Akhtar Plastics",      PartyRole.Customer, "M. Akhtar",       "0300-1234567", "Lahore",     "Shah Alam Market, Lahore"),
            new PartySeed("Bilal Industries",     PartyRole.Customer, "Bilal Ahmed",     "0321-9876543", "Faisalabad", "Industrial Estate, Faisalabad"),
            new PartySeed("Khan Polymers",        PartyRole.Customer, "Zahid Khan",      "0345-2223344", "Karachi",    "SITE Area, Karachi"),
            new PartySeed("Lahore Plastic House", PartyRole.Customer, "Adnan Sheikh",    "0302-5556677", "Lahore",     "Misri Shah, Lahore"),
            new PartySeed("Sialkot Plastic Works",PartyRole.Customer, "Usman Tariq",     "0301-7778899", "Sialkot",    "Daska Road, Sialkot")
        };
        await UpsertPartiesAsync(db, categoryId, parties);
    }

    private static async Task EnsureVendorsAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var categoryId = categories[Cat.Vendors].Id;
        var parties = new[]
        {
            new PartySeed("Crescent Polymers",       PartyRole.Vendor, "Imran Shah",    "0333-5556677", "Karachi", "SITE Area, Karachi"),
            new PartySeed("Punjab Scrap Traders",    PartyRole.Vendor, "Asif Mehmood",  "0300-1119988", "Lahore",  "Badami Bagh, Lahore"),
            new PartySeed("Multan Recycling Co",     PartyRole.Vendor, "Zulfiqar Ali",  "0345-2227766", "Multan",  "Vehari Road, Multan"),
            new PartySeed("Color Master Industries", PartyRole.Vendor, "Hamza Iqbal",   "0321-3334455", "Karachi", "Korangi, Karachi"),
            new PartySeed("Calcium Mineral Co",      PartyRole.Vendor, "Sajid Hussain", "0312-6667788", "Quetta",  "Hub Road, Quetta"),
            new PartySeed("Karachi Imports",         PartyRole.Vendor, "Bilal Saleem",  "0322-8881122", "Karachi", "Port Area, Karachi")
        };
        await UpsertPartiesAsync(db, categoryId, parties);
    }

    private static async Task EnsureTransportersAsync(AppDbContext db, IReadOnlyDictionary<string, Category> categories)
    {
        var categoryId = categories[Cat.Transporters].Id;
        var parties = new[]
        {
            new PartySeed("Al-Madina Logistics",     PartyRole.Transporter, "Tariq Mehmood", "0301-7778899", "Multan",  "GT Road, Multan"),
            new PartySeed("Faisal Goods Transport",  PartyRole.Transporter, "Faisal Khan",   "0312-4445566", "Lahore",  "Badami Bagh, Lahore"),
            new PartySeed("Speed Cargo",             PartyRole.Transporter, "Naveed Ali",    "0322-8889900", "Karachi", "Port Area, Karachi")
        };
        await UpsertPartiesAsync(db, categoryId, parties);
    }

    // ---------- Generic upsert helpers ----------

    private static async Task UpsertProductsAsync(AppDbContext db, int categoryId, IEnumerable<ProductSeed> items)
    {
        foreach (var item in items)
        {
            await UpsertProductAccountAsync(db, categoryId, item.Name, item.AccountType, item.Packing, item.WeightKg);
        }
    }

    private static async Task<Account> UpsertProductAccountAsync(
        AppDbContext db, int categoryId, string name, AccountType accountType, string packing, decimal weightKg)
    {
        var account = await db.Accounts
            .Include(a => a.ProductDetail)
            .FirstOrDefaultAsync(a => a.CategoryId == categoryId && a.Name == name);

        if (account == null)
        {
            account = new Account { Name = name, CategoryId = categoryId, AccountType = accountType };
            db.Accounts.Add(account);
            await db.SaveChangesAsync();
        }
        else
        {
            account.AccountType = accountType;
        }

        if (account.ProductDetail == null)
        {
            db.ProductDetails.Add(new ProductDetail
            {
                AccountId = account.Id,
                Packing = packing,
                PackingWeightKg = weightKg
            });
        }
        else
        {
            account.ProductDetail.Packing = packing;
            account.ProductDetail.PackingWeightKg = weightKg;
        }
        await db.SaveChangesAsync();
        return account;
    }

    private static async Task UpsertExpensesAsync(AppDbContext db, int categoryId, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var exists = await db.Accounts.AnyAsync(a => a.CategoryId == categoryId && a.Name == name);
            if (exists) continue;

            db.Accounts.Add(new Account
            {
                Name = name,
                CategoryId = categoryId,
                AccountType = AccountType.Expense
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task UpsertBankAccountAsync(
        AppDbContext db, int categoryId, string name, string? accountNumber, string? bankName)
    {
        var account = await db.Accounts
            .Include(a => a.BankDetail)
            .FirstOrDefaultAsync(a => a.CategoryId == categoryId && a.Name == name);

        if (account == null)
        {
            account = new Account { Name = name, CategoryId = categoryId, AccountType = AccountType.Account };
            db.Accounts.Add(account);
            await db.SaveChangesAsync();
        }
        else
        {
            account.AccountType = AccountType.Account;
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

    private static async Task UpsertPartiesAsync(AppDbContext db, int categoryId, IEnumerable<PartySeed> parties)
    {
        foreach (var party in parties)
        {
            var account = await db.Accounts
                .Include(a => a.PartyDetail)
                .FirstOrDefaultAsync(a => a.CategoryId == categoryId && a.Name == party.Name);

            if (account == null)
            {
                account = new Account { Name = party.Name, CategoryId = categoryId, AccountType = AccountType.Party };
                db.Accounts.Add(account);
                await db.SaveChangesAsync();
            }
            else
            {
                account.AccountType = AccountType.Party;
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

    private sealed record ProductSeed(string Name, AccountType AccountType, string Packing, decimal WeightKg);
    private sealed record PartySeed(string Name, PartyRole Role, string Contact, string Phone, string City, string Address);
}
