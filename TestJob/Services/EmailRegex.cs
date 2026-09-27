using System.Text.RegularExpressions;

namespace TestJob.Services;

public static class EmailRegex
{
    public static readonly Regex Pattern = new(
        @"\b[\w.]+@[\w.]+\.\w+\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
}