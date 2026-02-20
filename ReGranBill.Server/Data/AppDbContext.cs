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
    public DbSet<DeliveryChallan> DeliveryChallans => Set<DeliveryChallan>();
    public DbSet<DcLine> DcLines => Set<DcLine>();
    public DbSet<DcCartage> DcCartages => Set<DcCartage>();
    public DbSet<DcNumberSequence> DcNumberSequences => Set<DcNumberSequence>();
    public DbSet<JournalVoucher> JournalVouchers => Set<JournalVoucher>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

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

        // DeliveryChallan
        modelBuilder.Entity<DeliveryChallan>(e =>
        {
            e.ToTable("delivery_challans");
            e.HasIndex(d => d.DcNumber).IsUnique();
            e.Property(d => d.DcNumber).HasMaxLength(20);
            e.Property(d => d.VehicleNumber).HasMaxLength(20);
            e.Property(d => d.Description).HasMaxLength(500);
            e.Property(d => d.VoucherType).HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => Enum.Parse<VoucherType>(v));
            e.Property(d => d.Status).HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => Enum.Parse<ChallanStatus>(v));
            e.HasOne(d => d.Customer).WithMany().HasForeignKey(d => d.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Creator).WithMany().HasForeignKey(d => d.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // DcLine
        modelBuilder.Entity<DcLine>(e =>
        {
            e.ToTable("dc_lines");
            e.Property(l => l.Rbp).HasMaxLength(5);
            e.Property(l => l.Rate).HasColumnType("decimal(12,2)");
            e.HasOne(l => l.DeliveryChallan).WithMany(d => d.Lines).HasForeignKey(l => l.DcId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // DcCartage (1:1)
        modelBuilder.Entity<DcCartage>(e =>
        {
            e.ToTable("dc_cartage");
            e.HasIndex(c => c.DcId).IsUnique();
            e.Property(c => c.Amount).HasColumnType("decimal(12,2)");
            e.HasOne(c => c.DeliveryChallan).WithOne(d => d.Cartage).HasForeignKey<DcCartage>(c => c.DcId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Transporter).WithMany().HasForeignKey(c => c.TransporterId).OnDelete(DeleteBehavior.Restrict);
        });

        // DcNumberSequence
        modelBuilder.Entity<DcNumberSequence>(e =>
        {
            e.ToTable("dc_number_sequence");
            e.Property(s => s.LastNumber).HasDefaultValue(42);
        });

        // JournalVoucher
        modelBuilder.Entity<JournalVoucher>(e =>
        {
            e.ToTable("journal_vouchers");
            e.HasIndex(j => j.VoucherNumber).IsUnique();
            e.Property(j => j.VoucherNumber).HasMaxLength(20);
            e.Property(j => j.Description).HasMaxLength(500);
            e.Property(j => j.VoucherType).HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => Enum.Parse<VoucherType>(v));
            e.HasOne(j => j.DeliveryChallan).WithMany(d => d.JournalVouchers)
                .HasForeignKey(j => j.DcId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(j => j.Creator).WithMany().HasForeignKey(j => j.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // JournalEntry
        modelBuilder.Entity<JournalEntry>(e =>
        {
            e.ToTable("journal_entries");
            e.Property(je => je.Description).HasMaxLength(500);
            e.Property(je => je.Debit).HasColumnType("decimal(14,2)");
            e.Property(je => je.Credit).HasColumnType("decimal(14,2)");
            e.Property(je => je.Rbp).HasMaxLength(5);
            e.HasOne(je => je.JournalVoucher).WithMany(j => j.Entries).HasForeignKey(je => je.VoucherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(je => je.Account).WithMany().HasForeignKey(je => je.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
