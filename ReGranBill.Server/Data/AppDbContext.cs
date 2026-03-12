using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ProductDetail> ProductDetails => Set<ProductDetail>();
    public DbSet<BankDetail> BankDetails => Set<BankDetail>();
    public DbSet<PartyDetail> PartyDetails => Set<PartyDetail>();
    public DbSet<JournalVoucher> JournalVouchers => Set<JournalVoucher>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalVoucherReference> JournalVoucherReferences => Set<JournalVoucherReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).HasMaxLength(100);
            e.Property(u => u.Role).HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => Enum.Parse<UserRole>(v));
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
            e.HasIndex(a => a.Name).IsUnique();
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
            e.Property(p => p.Unit).HasMaxLength(20).HasDefaultValue("kg");
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
            e.Property(j => j.VoucherType).HasMaxLength(20)
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
            e.Property(je => je.Rbp).HasMaxLength(5);
            e.Property(je => je.Rate).HasColumnType("decimal(12,2)");
            e.Property(je => je.IsEdited).HasDefaultValue(false);
            e.HasOne(je => je.JournalVoucher).WithMany(j => j.Entries).HasForeignKey(je => je.VoucherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(je => je.Account).WithMany().HasForeignKey(je => je.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        // JournalVoucherReference
        modelBuilder.Entity<JournalVoucherReference>(e =>
        {
            e.ToTable("journal_voucher_references");
            e.HasOne(r => r.MainVoucher).WithMany().HasForeignKey(r => r.MainVoucherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.ReferenceVoucher).WithMany().HasForeignKey(r => r.ReferenceVoucherId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.MainVoucherId, r.ReferenceVoucherId }).IsUnique();
        });
    }
}
