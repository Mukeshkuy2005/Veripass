using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> builder)
    {
        builder.ToTable("credentials");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CredentialNumberHash).HasMaxLength(128);
        builder.HasIndex(x => x.CredentialNumberHash);
        builder.Property(x => x.CredentialDisplayValue).HasMaxLength(100);
        builder.Property(x => x.Issuer).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");

        builder.HasOne(x => x.CredentialType)
            .WithMany(x => x.Credentials)
            .HasForeignKey(x => x.CredentialTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Business)
            .WithMany(x => x.Credentials)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CredentialTypeConfiguration : IEntityTypeConfiguration<CredentialType>
{
    public void Configure(EntityTypeBuilder<CredentialType> builder)
    {
        builder.ToTable("credential_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.Property(x => x.VerificationProvider).HasMaxLength(50);
        builder.Property(x => x.SchemaJson).HasColumnType("jsonb");
    }
}
