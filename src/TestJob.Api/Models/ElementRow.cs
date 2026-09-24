namespace TestJob.Api.Models;

/// <summary>
///     Row stored in the elements table.
/// </summary>
public sealed class ElementRow
{
    public long Id { get; init; }

    public string AttributeValue { get; init; } = string.Empty;

    public string Html { get; init; } = string.Empty;
}
