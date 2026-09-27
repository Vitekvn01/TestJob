using System.Security.Cryptography;
using System.Text;
using AngleSharp.Html.Parser;
using TestJob.Exceptions;
using TestJob.Models;
using AngleSharp.Dom;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using TestJob.Options;

namespace TestJob.Services;

public class HtmlAnalysisService : IHtmlAnalysisService
{
    private readonly ILogger<HtmlAnalysisService> _logger;
    private readonly DatabaseOptions _dbOptions;

    public HtmlAnalysisService(
        ILogger<HtmlAnalysisService> logger,
        IOptions<DatabaseOptions> dbOptions)
    {
        _logger = logger;
        _dbOptions = dbOptions.Value;
    }

    public async  Task<HtmlResponse> ProcessAsync(HtmlRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Получен запрос: {Selector}", request.Selector);

        // Декодируем базовый URL
        string baseUrl;
        try
        {
            var baseUrlBytes = Convert.FromBase64String(request.UrlB64);
            baseUrl = Encoding.UTF8.GetString(baseUrlBytes);
        }
        catch (Exception ex)
        {
            throw new BusinessException("URL_DECODE_FAILED", ex.Message);
        }

        _logger.LogInformation("Base URL: {BaseUrl}", baseUrl);

        // Декодируем ключ
        byte[] key;
        try
        {
            key = Convert.FromBase64String(request.KeyBytesB64);
        }
        catch (Exception ex)
        {
            throw new BusinessException("KEY_DECODE_FAILED", ex.Message);
        }

        _logger.LogInformation("Key length: {Length} bytes", key.Length);

        // Декодируем шифротекст
        byte[] encrypted;
        try
        {
            encrypted = Convert.FromBase64String(request.EncryptedTextBytesB64);
        }
        catch (Exception ex)
        {
            throw new BusinessException("ENCRYPTED_DECODE_FAILED", ex.Message);
        }

        _logger.LogInformation("Encrypted length: {Length} bytes", encrypted.Length);

        // Расшифровываем
        string target;
        try
        {
            target = DecryptAesEcb(encrypted, key);
        }
        catch (Exception ex)
        {
            throw new BusinessException("DECRYPTION_FAILED", ex.Message);
        }

        _logger.LogInformation("Decrypted target: {Target}", target);

        // Декодируем HTML
        string html;
        try
        {
            var pageBytes = Convert.FromBase64String(request.PageB64);
            html = Encoding.UTF8.GetString(pageBytes);
        }
        catch (Exception ex)
        {
            throw new BusinessException("PAGE_DECODE_FAILED", ex.Message);
        }

        _logger.LogInformation("HTML length: {Length} characters", html.Length);

        // Парсим HTML через AngleSharp
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        // Выбираем элементы по селектору
        List<IElement> elements;
        try
        {
            elements = document.QuerySelectorAll(request.Selector).ToList();
        }
        catch (Exception ex)
        {
            throw new BusinessException("INVALID_SELECTOR", "Invalid CSS selector: " + ex.Message);
        }

        _logger.LogInformation("Elements count: {Count}", elements.Count);

        //Собираем значения атрибутов
        var attrValues = new List<string>();
        foreach (var element in elements)
        {
            var value = element.GetAttribute(request.Attribute);
            if (value != null)
                attrValues.Add(value);
        }

        _logger.LogInformation("Attribute values collected: {Count}", attrValues.Count);

        // Ищем email-адреса
        var emailMatches = EmailRegex.Pattern.Matches(html);
        var emails = emailMatches.Select(m => m.Value).ToList();
        _logger.LogInformation("Emails found: {Count}", emails.Count);
        
        // после сбора attrValues
        await SaveElementsAsync(elements, request.Attribute, cancellationToken);
        
        var response = new HtmlResponse
        {
            Url = baseUrl,
            DecryptedPlainText = target,
            ElementsCount = elements.Count,
            ElementsAttrList = attrValues,
            EmailsCount = emails.Count,
            EmailsList = emails,
        };
        
        

        return response;
    }

    private async Task SaveElementsAsync(
        List<IElement> elements,
        string attribute,
        CancellationToken cancellationToken)
    {
        if (elements.Count == 0)
            return;

        try
        {
            await using var connection = new NpgsqlConnection(_dbOptions.Postgres);

            const string sql = """
                               INSERT INTO elements (value, html)
                               VALUES (@Value, @Html)
                               """;

            var rows = elements
                .Select(e => new
                {
                    Value = e.GetAttribute(attribute) ?? string.Empty,
                    Html = e.OuterHtml
                })
                .ToList();

            var affected = await connection.ExecuteAsync(
                new CommandDefinition(sql, rows, cancellationToken: cancellationToken));

            _logger.LogInformation("Inserted {Count} elements into DB", affected);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save elements to DB (continuing without DB)");
        }
    }

    private static string DecryptAesEcb(byte[] cipherText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None; 

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}