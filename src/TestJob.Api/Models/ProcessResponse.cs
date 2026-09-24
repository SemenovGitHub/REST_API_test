namespace TestJob.Api.Models;

/// <summary>
///     Result of the page processing request.
/// </summary>
public sealed class ProcessResponse
{
    public int IsError { get; init; }

    public string ErrorCode { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public int ElementsCount { get; init; }

    public int EmailsCount { get; init; }

    public string Url { get; init; } = string.Empty;

    public string DecryptedPlainText { get; init; } = string.Empty;

    public List<string> ElementsAttrList { get; init; } = [];

    public List<string> EmailsList { get; init; } = [];
}
