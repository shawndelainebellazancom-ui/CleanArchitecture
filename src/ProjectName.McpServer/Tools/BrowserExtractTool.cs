using ProjectName.McpServer.Services;

namespace ProjectName.McpServer.Tools;

/// <summary>
/// Extract structured data from the current browser page
/// </summary>
public class BrowserExtractTool : McpToolBase<BrowserExtractInput, BrowserExtractOutput>
{
    private readonly PlaywrightManager _playwright;

    public BrowserExtractTool(
        PlaywrightManager playwright,
        ILogger<BrowserExtractTool> logger) : base(logger)
    {
        _playwright = playwright;
    }

    public override string Name => "browser_extract";

    public override string Description => "Extract text, HTML, or structured data from elements on the page";

    public override async Task<BrowserExtractOutput> ExecuteAsync(
        BrowserExtractInput input,
        CancellationToken ct = default)
    {
        Logger.LogInformation("Extracting data with selector: {Selector}", input.Selector);

        var page = await _playwright.GetPageAsync();

        var results = new List<object>();

        if (input.ExtractAll)
        {
            var elements = await page.QuerySelectorAllAsync(input.Selector);

            foreach (var element in elements)
            {
                var data = await ExtractFromElementAsync(element, input.Format);
                results.Add(data);
            }
        }
        else
        {
            var element = await page.QuerySelectorAsync(input.Selector);

            if (element != null)
            {
                var data = await ExtractFromElementAsync(element, input.Format);
                results.Add(data);
            }
        }

        Logger.LogInformation("Extracted {Count} elements", results.Count);

        return new BrowserExtractOutput
        {
            Success = results.Count > 0,
            Data = results,
            Count = results.Count
        };
    }

    private async Task<object> ExtractFromElementAsync(
        Microsoft.Playwright.IElementHandle element,
        string format)
    {
        return format.ToLowerInvariant() switch
        {
            "text" => await element.TextContentAsync() ?? "",
            "html" => await element.InnerHTMLAsync(),
            "innertext" => await element.InnerTextAsync(),
            "attributes" => await ExtractAttributesAsync(element),
            _ => await element.TextContentAsync() ?? ""
        };
    }

    private async Task<Dictionary<string, string>> ExtractAttributesAsync(
        Microsoft.Playwright.IElementHandle element)
    {
        var attributes = new Dictionary<string, string>();

        // Common attributes to extract
        var attrNames = new[] { "id", "class", "href", "src", "alt", "title", "data-*" };

        foreach (var attr in attrNames)
        {
            var value = await element.GetAttributeAsync(attr);
            if (!string.IsNullOrEmpty(value))
            {
                attributes[attr] = value;
            }
        }

        return attributes;
    }
}

public record BrowserExtractInput
{
    public string Selector { get; init; } = "";
    public string Format { get; init; } = "text"; // text, html, innertext, attributes
    public bool ExtractAll { get; init; } = false;
}

public record BrowserExtractOutput
{
    public bool Success { get; init; }
    public List<object> Data { get; init; } = new();
    public int Count { get; init; }
}