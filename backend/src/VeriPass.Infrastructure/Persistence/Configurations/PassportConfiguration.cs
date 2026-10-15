using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class BusinessPassportConfiguration : IEntityTypeConfiguration<BusinessPassport>
{
    public void Configure(EntityTypeBuilder<BusinessPassport> builder)
    {
        builder.ToTable("business_passports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PassportNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.PassportNumber).IsUnique();

        builder.Property(x => x.PublicIdentifier).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.PublicIdentifier).IsUnique();

        builder.Property(x => x.Version).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.QrTokenHash).HasMaxLength(128);
        builder.Property(x => x.PrivacyPreferences).HasColumnType("text").IsRequired(false);

        builder.HasMany(x => x.Shares)
            .WithOne(x => x.Passport)
            .HasForeignKey(x => x.PassportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AccessLogs)
            .WithOne(x => x.Passport)
            .HasForeignKey(x => x.PassportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PassportShareConfiguration : IEntityTypeConfiguration<PassportShare>
{
    public void Configure(EntityTypeBuilder<PassportShare> builder)
    {
        builder.ToTable("passport_shares");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ShareTokenHash).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.ShareTokenHash);
        builder.Property(x => x.RecipientEmail).HasMaxLength(256);
        builder.Property(x => x.RecipientName).HasMaxLength(200);
        builder.Property(x => x.AccessScopeJson).HasColumnType("jsonb");
    }
}

public class PassportAccessLogConfiguration : IEntityTypeConfiguration<PassportAccessLog>
{
    public void Configure(EntityTypeBuilder<PassportAccessLog> builder)
    {
        builder.ToTable("passport_access_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IpHash).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.HasIndex(x => new { x.PassportId, x.AccessedAt });
    }
}
