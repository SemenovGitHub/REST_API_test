using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Dapper;
using FluentValidation;
using FluentValidation.Results;
using Npgsql;
using TestJob.Api.Models;

namespace TestJob.Api.Services;

public interface IPageProcessService
{
    Task<ProcessResponse> ProcessAsync(ProcessRequest request, CancellationToken cancellationToken);
}

/// <summary>
///     Decodes the request, extracts elements and emails, stores elements, and decrypts the text.
/// </summary>
public sealed class PageProcessService : IPageProcessService
{
    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private const string InsertSql = """
                                     INSERT INTO elements (attribute_value, html)
                                     VALUES (@AttributeValue, @Html)
                                     """;

    private readonly IValidator<ProcessRequest> _validator;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PageProcessService> _logger;

    public PageProcessService(
        IValidator<ProcessRequest> validator,
        NpgsqlDataSource dataSource,
        ILogger<PageProcessService> logger)
    {
        _validator = validator;
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<ProcessResponse> ProcessAsync(
        ProcessRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return MapValidationFailure(validation.Errors[0]);
        }

        if (!TryDecodeText(request.UrlB64, out var url, out var urlError))
        {
            return Error("invalid_url_base64", urlError.Message);
        }

        string page;
        List<ElementRow> elements;
        try
        {
            (page, elements) = await DecodeAndParsePageAsync(
                request.PageB64,
                request.Selector,
                request.Attribute,
                cancellationToken);
        }
        catch (Exception exception) when (exception is FormatException or DecoderFallbackException)
        {
            return Error("invalid_page_base64", exception.Message, url);
        }

        try
        {
            await SaveElementsAsync(elements, cancellationToken);

            var emails = FindEmails(page);
            var plainText = Decrypt(request.KeyBytesB64, request.EncryptedTextBytesB64);

            return new ProcessResponse
            {
                IsError = 0,
                Url = url,
                ElementsCount = elements.Count,
                ElementsAttrList = elements.Select(element => element.AttributeValue).ToList(),
                EmailsCount = emails.Count,
                EmailsList = emails,
                DecryptedPlainText = plainText
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Page processing failed.");
            return Error("processing_error", exception.Message, url);
        }
    }

    private static ProcessResponse MapValidationFailure(ValidationFailure failure)
    {
        var errorCode = failure.PropertyName switch
        {
            nameof(ProcessRequest.Selector) when failure.AttemptedValue is string => "empty_selector",
            nameof(ProcessRequest.Attribute) when failure.AttemptedValue is string => "empty_attribute",
            _ => "missing_parameter"
        };

        return Error(errorCode, failure.ErrorMessage);
    }

    private static ProcessResponse Error(string errorCode, string errorMessage, string? url = null) => new()
    {
        IsError = 1,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        Url = url ?? string.Empty
    };

    private static bool TryDecodeText(string encoded, out string text, out Exception error)
    {
        try
        {
            text = Utf8.GetString(Convert.FromBase64String(encoded));
            error = null!;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or DecoderFallbackException)
        {
            text = string.Empty;
            error = exception;
            return false;
        }
    }

    private static async Task<(string Page, List<ElementRow> Elements)> DecodeAndParsePageAsync(
        string pageBase64,
        string selector,
        string attribute,
        CancellationToken cancellationToken)
    {
        var page = Utf8.GetString(Convert.FromBase64String(pageBase64));
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(page, cancellationToken);
        var elements = document.QuerySelectorAll(selector)
            .Select(node => new ElementRow
            {
                AttributeValue = node.GetAttribute(attribute) ?? string.Empty,
                Html = node.OuterHtml
            })
            .ToList();

        return (page, elements);
    }

    private async Task SaveElementsAsync(
        IReadOnlyList<ElementRow> elements,
        CancellationToken cancellationToken)
    {
        if (elements.Count == 0)
        {
            return;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            elements,
            transaction: transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    private static List<string> FindEmails(string page)
    {
        return EmailRegex.Matches(page)
            .Select(match => match.Value)
            .Distinct()
            .ToList();
    }

    private static string Decrypt(string keyBase64, string cipherBase64)
    {
        var key = Convert.FromBase64String(keyBase64);
        var cipher = Convert.FromBase64String(cipherBase64);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Utf8.GetString(plainBytes).TrimEnd('\0');
    }
}