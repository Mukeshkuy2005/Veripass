using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("businesses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BusinessIdNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.BusinessIdNumber).IsUnique();

        builder.Property(x => x.LegalName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.TradeName).HasMaxLength(250);
        builder.Property(x => x.BusinessType).HasMaxLength(100);

        builder.Property(x => x.Pan).HasMaxLength(10);
        builder.HasIndex(x => x.Pan).IsUnique().HasFilter("\"Pan\" IS NOT NULL");

        builder.Property(x => x.Gstin).HasMaxLength(15);
        builder.HasIndex(x => x.Gstin).IsUnique().HasFilter("\"Gstin\" IS NOT NULL");

        builder.Property(x => x.Cin).HasMaxLength(21);
        builder.HasIndex(x => x.Cin).IsUnique().HasFilter("\"Cin\" IS NOT NULL");

        builder.Property(x => x.UdyamNumber).HasMaxLength(30);
        builder.Property(x => x.Website).HasMaxLength(500);
        builder.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasMany(x => x.Addresses)
            .WithOne(x => x.Business)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Directors)
            .WithOne(x => x.Business)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.BankAccounts)
            .WithOne(x => x.Business)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TrustScore)
            .WithOne(x => x.Business)
            .HasForeignKey<TrustScore>(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Passport)
            .WithOne(x => x.Business)
            .HasForeignKey<BusinessPassport>(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BusinessAddressConfiguration : IEntityTypeConfiguration<BusinessAddress>
{
    public void Configure(EntityTypeBuilder<BusinessAddress> builder)
    {
        builder.ToTable("business_addresses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AddressType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Line1).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Line2).HasMaxLength(250);
        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.District).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Pincode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Country).HasMaxLength(100).IsRequired();
        builder.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
    }
}

public class DirectorConfiguration : IEntityTypeConfiguration<Director>
{
    public void Configure(EntityTypeBuilder<Director> builder)
    {
        builder.ToTable("directors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Designation).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Din).HasMaxLength(20);
        builder.Property(x => x.PanMasked).HasMaxLength(20);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
    }
}

public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("bank_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccountHolderName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BankName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AccountNumberLast4).HasMaxLength(4).IsRequired();
        builder.Property(x => x.Ifsc).HasMaxLength(11).IsRequired();
        builder.Property(x => x.AccountType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
    }
}
