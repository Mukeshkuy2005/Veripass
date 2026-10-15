using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.ToTable("disputes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Resolution).HasMaxLength(2000);

        builder.HasOne(x => x.Business)
            .WithMany(x => x.Disputes)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.VerificationCase)
            .WithMany()
            .HasForeignKey(x => x.VerificationCaseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Messages)
            .WithOne(x => x.Dispute)
            .HasForeignKey(x => x.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DisputeMessageConfiguration : IEntityTypeConfiguration<DisputeMessage>
{
    public void Configure(EntityTypeBuilder<DisputeMessage> builder)
    {
        builder.ToTable("dispute_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SenderRole).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.AttachmentsJson).HasColumnType("jsonb");
    }
}
