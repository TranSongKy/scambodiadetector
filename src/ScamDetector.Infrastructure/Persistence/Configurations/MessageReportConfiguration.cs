using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Reports;

namespace ScamDetector.Infrastructure.Persistence.Configurations;

public sealed class MessageReportConfiguration : IEntityTypeConfiguration<MessageReport>
{
    private const string TableName = "MessageReports";
    private const int EnumColumnLength = 16;

    public void Configure(EntityTypeBuilder<MessageReport> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).ValueGeneratedNever();
        builder.Property(report => report.MaskedText).IsRequired().HasMaxLength(ClassificationLimits.MaxMessageLength);
        builder.Property(report => report.ReportedLabel).HasConversion<string>().HasMaxLength(EnumColumnLength);
        builder.Property(report => report.Channel).HasConversion<string>().HasMaxLength(EnumColumnLength);
        builder.Property(report => report.CreatedAt).IsRequired();
        builder.HasIndex(report => report.CreatedAt);
    }
}
