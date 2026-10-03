using ScamDetector.Core.ThreatIntel;
using ScamDetector.Infrastructure.Tests.Support;
using ScamDetector.Infrastructure.ThreatIntel;

namespace ScamDetector.Infrastructure.Tests.ThreatIntel;

public sealed class ThreatIntelLoaderTests
{
    private const string TemplateText = "Tài khoản của bạn bị khoá, truy cập <URL> và nhập mã OTP để mở khoá ngay hôm nay";
    private static readonly DateTime WriteTime = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Load_MissingFiles_ReturnsEmptyIndex()
    {
        using var directory = new TemporaryDirectory();

        var index = ThreatIntelLoader.Load(directory.Path, ThreatIntelligenceIndex.DefaultMinimumCoverage);

        Assert.Equal(0, index.BlockedDomainCount);
        Assert.Equal(0, index.TemplateCount);
    }

    [Fact]
    public void Load_DomainsAndTemplates_BuildsMatchingIndex()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile(ThreatIntelFiles.BlockedDomains,
            "domain,source,source_url,first_seen,evidence\nvcb-xacminh.com,test,https://example.gov.vn,2026-10-01,\n", WriteTime);
        directory.WriteFile(ThreatIntelFiles.ScamTemplates,
            $"id,text,source,source_url,first_seen\ntpl_000001,\"{TemplateText}\",test,https://example.gov.vn,2026-10-01\n", WriteTime);

        var index = ThreatIntelLoader.Load(directory.Path, ThreatIntelligenceIndex.DefaultMinimumCoverage);

        Assert.Equal(1, index.BlockedDomainCount);
        Assert.Equal(1, index.TemplateCount);
        var match = index.Match("Vào https://vcb-xacminh.com/login ngay", "Vào <URL> ngay");
        Assert.Equal(["vcb-xacminh.com"], match.BlocklistedDomains);
        Assert.Equal("tpl_000001", index.Match(TemplateText, TemplateText).Template?.TemplateId);
    }

    [Fact]
    public void Load_RepositoryDataDirectory_FilesExistAndParse()
    {
        var repositoryDataDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "threat-intel"));

        ThreatIntelLoader.Load(repositoryDataDirectory, ThreatIntelligenceIndex.DefaultMinimumCoverage);

        Assert.True(File.Exists(Path.Combine(repositoryDataDirectory, ThreatIntelFiles.BlockedDomains)));
        Assert.True(File.Exists(Path.Combine(repositoryDataDirectory, ThreatIntelFiles.ScamTemplates)));
    }

    [Fact]
    public void LatestWriteTimeUtc_NoFiles_ReturnsMinValue()
    {
        using var directory = new TemporaryDirectory();

        Assert.Equal(DateTime.MinValue, ThreatIntelLoader.LatestWriteTimeUtc(directory.Path));
    }

    [Fact]
    public void LatestWriteTimeUtc_TwoFiles_ReturnsNewest()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile(ThreatIntelFiles.BlockedDomains, "domain\n", WriteTime);
        directory.WriteFile(ThreatIntelFiles.ScamTemplates, "id,text\n", WriteTime.AddHours(1));

        Assert.Equal(WriteTime.AddHours(1), ThreatIntelLoader.LatestWriteTimeUtc(directory.Path));
    }
}
