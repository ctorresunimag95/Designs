namespace AspireGuide.ResourceCli.Validation;

public enum IssueSeverity { Error, Warning }

public sealed record ValidationIssue(string Code, string Message, IssueSeverity Severity)
{
    public bool IsError => Severity == IssueSeverity.Error;
}