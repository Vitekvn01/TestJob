namespace TestJob.Options;

public class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    public string Postgres { get; set; } = string.Empty;
}