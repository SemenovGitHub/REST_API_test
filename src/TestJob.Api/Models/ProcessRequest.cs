namespace TestJob.Api.Models;

/// <summary>
///     Payload for the page processing request.
/// </summary>
public sealed class ProcessRequest
{
    public required string Selector { get; init; }

    public required string Attribute { get; init; }

    public required string UrlB64 { get; init; }

    public required string EncryptedTextBytesB64 { get; init; }

    public required string KeyBytesB64 { get; init; }

    public required string PageB64 { get; init; }
}
