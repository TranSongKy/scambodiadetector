namespace ScamDetector.Infrastructure.Persistence;

public sealed record DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "ScamDetector";

    public bool ApplyMigrationsOnStartup { get; init; }
}
