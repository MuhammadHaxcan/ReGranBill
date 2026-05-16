using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePage> RolePages => Set<RolePage>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ProductDetail> ProductDetails => Set<ProductDetail>();
    public DbSet<BankDetail> BankDetails => Set<BankDetail>();
    public DbSet<PartyDetail> PartyDetails => Set<PartyDetail>();
    public DbSet<JournalVoucher> JournalVouchers => Set<JournalVoucher>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalVoucherReference> JournalVoucherReferences => Set<JournalVoucherReference>();
    public DbSet<VoucherCounter> VoucherCounters => Set<VoucherCounter>();
    public DbSet<VehicleOption> VehicleOptions => Set<VehicleOption>();
    public DbSet<Formulation> Formulations => Set<Formulation>();
    public DbSet<FormulationLine> FormulationLines => Set<FormulationLine>();
    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<InventoryVoucherLink> InventoryVoucherLinks => Set<InventoryVoucherLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Role
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasIndex(r => r.Name).IsUnique();
            e.Property(r => r.Name).HasMaxLength(50);
            e.Property(r => r.IsSystem).HasDefaultValue(false);
            e.Property(r => r.IsAdmin).HasDefaultValue(false);
        });

        // RolePage
        modelBuilder.Entity<RolePage>(e =>
        {
            e.ToTable("role_pages");
            e.HasKey(rp => new { rp.RoleId, rp.PageKey });
            e.Property(rp => rp.PageKey).HasMaxLength(64);
            e.HasOne(rp => rp.Role).WithMany(r => r.Pages).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).HasMaxLength(100);
            e.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CompanySettings>(e =>
        {
            e.ToTable("company_settings");
            e.Property(cs => cs.CompanyName).HasMaxLength(200);
            e.Property(cs => cs.Address).HasMaxLength(500);
            e.Property(cs => cs.LogoContentType).HasMaxLength(100);
        });

        // Category
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasIndex(c => c.Name).IsUnique();
            e.Property(c => c.Name).HasMaxLength(100);
        });

        // Account
        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasIndex(a => new { a.CategoryId, a.Name }).IsUnique();
            e.Property(a => a.Name).HasMaxLength(200);
            e.Property(a => a.AccountType).HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => Enum.Parse<AccountType>(v));
            e.HasOne(a => a.Category).WithMany(c => c.Accounts).HasForeignKey(a => a.CategoryId);
        });

        // ProductDetail (1:1)
        modelBuilder.Entity<ProductDetail>(e =>
        {
            e.ToTable("product_details");
            e.HasIndex(p => p.AccountId).IsUnique();
            e.Property(p => p.Packing).HasMaxLength(100);
            e.Property(p => p.PackingWeightKg).HasColumnType("decimal(10,2)");
            e.HasOne(p => p.Account).WithOne(a => a.ProductDetail).HasForeignKey<ProductDetail>(p => p.AccountId);
        });

        // BankDetail (1:1)
        modelBuilder.Entity<BankDetail>(e =>
        {
            e.ToTable("bank_details");
            e.HasIndex(b => b.AccountId).IsUnique();
            e.Property(b => b.AccountNumber).HasMaxLength(100);
            e.Property(b => b.BankName).HasMaxLength(200);
            e.HasOne(b => b.Account).WithOne(a => a.BankDetail).HasForeignKey<BankDetail>(b => b.AccountId);
        });

        // PartyDetail (1:1)
        modelBuilder.Entity<PartyDetail>(e =>
        {
            e.ToTable("party_details");
            e.HasIndex(p => p.AccountId).IsUnique();
            e.Property(p => p.PartyRole).HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => Enum.Parse<PartyRole>(v));
            e.Property(p => p.ContactPerson).HasMaxLength(200);
            e.Property(p => p.Phone).HasMaxLength(50);
            e.Property(p => p.City).HasMaxLength(100);
            e.Property(p => p.Address).HasMaxLength(500);
            e.HasOne(p => p.Account).WithOne(a => a.PartyDetail).HasForeignKey<PartyDetail>(p => p.AccountId);
        });

        // JournalVoucher
        modelBuilder.Entity<JournalVoucher>(e =>
        {
            e.ToTable("journal_vouchers");
            e.HasIndex(j => j.VoucherNumber).IsUnique();
            e.Property(j => j.VoucherNumber).HasMaxLength(20);
            e.Property(j => j.VehicleNumber).HasMaxLength(20);
            e.Property(j => j.Description).HasMaxLength(500);
            e.Property(j => j.VoucherType).HasMaxLength(50)
                .HasConversion(v => v.ToString(), v => Enum.Parse<VoucherType>(v));
            e.Property(j => j.IsDeleted).HasDefaultValue(false);
            e.HasOne(j => j.Creator).WithMany().HasForeignKey(j => j.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(j => !j.IsDeleted);
        });

        // JournalEntry
        modelBuilder.Entity<JournalEntry>(e =>
        {
            e.ToTable("journal_entries");
            e.Property(je => je.Description).HasMaxLength(500);
            e.Property(je => je.Debit).HasColumnType("decimal(14,2)");
            e.Property(je => je.Credit).HasColumnType("decimal(14,2)");
            e.Property(je => je.ActualWeightKg).HasColumnType("decimal(14,2)");
            e.Property(je => je.Rbp).HasMaxLength(5);
            e.Property(je => je.Rate).HasColumnType("decimal(12,2)");
            e.Property(je => je.IsEdited).HasDefaultValue(false);
            e.Property(je => je.LineKind).HasMaxLength(20)
                .HasConversion(v => v == null ? null : v.Value.ToString(), v => v == null ? null : Enum.Parse<ProductionLineKind>(v));
            e.HasOne(je => je.JournalVoucher).WithMany(j => j.Entries).HasForeignKey(je => je.VoucherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(je => je.Account).WithMany().HasForeignKey(je => je.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(je => !je.JournalVoucher.IsDeleted);
        });

        modelBuilder.Entity<InventoryLot>(e =>
        {
            e.ToTable("inventory_lots");
            e.HasIndex(x => x.LotNumber);
            e.Property(x => x.LotNumber).HasMaxLength(50);
            e.Property(x => x.SourceVoucherType).HasMaxLength(50)
                .HasConversion(v => v.ToString(), v => Enum.Parse<VoucherType>(v));
            e.Property(x => x.OriginalWeightKg).HasColumnType("decimal(14,2)");
            e.Property(x => x.BaseRate).HasColumnType("decimal(12,2)");
            e.Property(x => x.Status).HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => Enum.Parse<InventoryLotStatus>(v));
            e.HasOne(x => x.ProductAccount).WithMany().HasForeignKey(x => x.ProductAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.VendorAccount).WithMany().HasForeignKey(x => x.VendorAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SourceVoucher).WithMany().HasForeignKey(x => x.SourceVoucherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SourceEntry).WithMany().HasForeignKey(x => x.SourceEntryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ParentLot).WithMany(x => x.ChildLots).HasForeignKey(x => x.ParentLotId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryTransaction>(e =>
        {
            e.ToTable("inventory_transactions");
            e.Property(x => x.VoucherType).HasMaxLength(50)
                .HasConversion(v => v.ToString(), v => Enum.Parse<VoucherType>(v));
            e.Property(x => x.VoucherLineKey).HasMaxLength(80);
            e.Property(x => x.TransactionType).HasMaxLength(30)
                .HasConversion(v => v.ToString(), v => Enum.Parse<InventoryTransactionType>(v));
            e.Property(x => x.WeightKgDelta).HasColumnType("decimal(14,2)");
            e.Property(x => x.Rate).HasColumnType("decimal(12,2)");
            e.Property(x => x.ValueDelta).HasColumnType("decimal(14,2)");
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.LotId, x.TransactionDate, x.Id });
            e.HasOne(x => x.Voucher).WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ProductAccount).WithMany().HasForeignKey(x => x.ProductAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lot).WithMany(x => x.Transactions).HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryVoucherLink>(e =>
        {
            e.ToTable("inventory_voucher_links");
            e.Property(x => x.VoucherType).HasMaxLength(50)
                .HasConversion(v => v.ToString(), v => Enum.Parse<VoucherType>(v));
            e.Property(x => x.VoucherLineKey).HasMaxLength(80);
            e.HasIndex(x => new { x.VoucherId, x.VoucherType, x.VoucherLineKey });
            e.HasOne(x => x.Voucher).WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Lot).WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
        });

        // Formulation
        modelBuilder.Entity<Formulation>(e =>
        {
            e.ToTable("formulations");
            e.HasIndex(f => f.Name).IsUnique();
            e.Property(f => f.Name).HasMaxLength(120);
            e.Property(f => f.Description).HasMaxLength(500);
            e.Property(f => f.BaseInputKg).HasColumnType("decimal(10,2)").HasDefaultValue(100m);
            e.Property(f => f.IsActive).HasDefaultValue(true);
            e.Property(f => f.IsDeleted).HasDefaultValue(false);
            e.HasOne(f => f.Creator).WithMany().HasForeignKey(f => f.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(f => !f.IsDeleted);
        });

        // FormulationLine
        modelBuilder.Entity<FormulationLine>(e =>
        {
            e.ToTable("formulation_lines");
            e.Property(fl => fl.LineKind).HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => Enum.Parse<ProductionLineKind>(v));
            e.Property(fl => fl.AmountPerBase).HasColumnType("decimal(10,2)");
            e.Property(fl => fl.BagsPerBase).HasColumnType("decimal(8,2)");
            e.HasOne(fl => fl.Formulation).WithMany(f => f.Lines).HasForeignKey(fl => fl.FormulationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(fl => fl.Account).WithMany().HasForeignKey(fl => fl.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(fl => !fl.Formulation.IsDeleted);
        });

        // JournalVoucherReference
        modelBuilder.Entity<JournalVoucherReference>(e =>
        {
            e.ToTable("journal_voucher_references");
            e.HasOne(r => r.MainVoucher).WithMany().HasForeignKey(r => r.MainVoucherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.ReferenceVoucher).WithMany().HasForeignKey(r => r.ReferenceVoucherId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.MainVoucherId, r.ReferenceVoucherId }).IsUnique();
            e.HasIndex(r => r.MainVoucherId).IsUnique();
            e.HasIndex(r => r.ReferenceVoucherId).IsUnique();
            e.HasQueryFilter(r => !r.MainVoucher.IsDeleted && !r.ReferenceVoucher.IsDeleted);
        });

        modelBuilder.Entity<VoucherCounter>(e =>
        {
            e.ToTable("voucher_counters");
            e.HasKey(vc => vc.SequenceKey);
            e.Property(vc => vc.SequenceKey).HasColumnName("sequence_key").HasMaxLength(10);
            e.Property(vc => vc.LastNumber).HasColumnName("last_number");
        });

        modelBuilder.Entity<VehicleOption>(e =>
        {
            e.ToTable("vehicle_options");
            e.Property(v => v.Name).HasMaxLength(120);
            e.Property(v => v.VehicleNumber).HasMaxLength(50);
            e.Property(v => v.NormalizedVehicleNumber).HasMaxLength(50);
            e.HasIndex(v => v.NormalizedVehicleNumber).IsUnique();
            e.HasIndex(v => v.SortOrder);
        });
    }
}
