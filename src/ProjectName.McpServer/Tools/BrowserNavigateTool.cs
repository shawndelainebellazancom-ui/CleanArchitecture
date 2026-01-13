using Microsoft.Extensions.Logging;
using ProjectName.McpServer.Services;

namespace ProjectName.McpServer.Tools;

/// <summary>
/// Navigate to a URL in the browser
/// </summary>
public class BrowserNavigateTool : McpToolBase<BrowserNavigateInput, BrowserNavigateOutput>
{
    private readonly PlaywrightManager _playwright;

    public BrowserNavigateTool(
        PlaywrightManager playwright,
        ILogger<BrowserNavigateTool> logger) : base(logger)
    {
        _playwright = playwright;
    }

    public override string Name => "browser_navigate";

    public override string Description => "Navigate to a URL in the headless browser";

    public override async Task<BrowserNavigateOutput> ExecuteAsync(
        BrowserNavigateInput input,
        CancellationToken ct = default)
    {
        Logger.LogInformation("Navigating to: {Url}", input.Url);

        var page = await _playwright.GetPageAsync();

        var response = await page.GotoAsync(input.Url, new()
        {
            WaitUntil = input.WaitUntil switch
            {
                "networkidle" => Microsoft.Playwright.WaitUntilState.NetworkIdle,
                "domcontentloaded" => Microsoft.Playwright.WaitUntilState.DOMContentLoaded,
                _ => Microsoft.Playwright.WaitUntilState.Load
            },
            Timeout = input.Timeout ?? 30000
        });

        var title = await page.TitleAsync();
        var url = page.Url;

        Logger.LogInformation("Navigated successfully - Title: {Title}", title);

        return new BrowserNavigateOutput
        {
            Success = response?.Ok ?? false,
            Url = url,
            Title = title,
            StatusCode = response?.Status ?? 0
        };
    }
}

public record BrowserNavigateInput
{
    public string Url { get; init; } = "";
    public string? WaitUntil { get; init; } = "load";
    public int? Timeout { get; init; } = 30000;
}

public record BrowserNavigateOutput
{
    public bool Success { get; init; }
    public string Url { get; init; } = "";
    public string Title { get; init; } = "";
    public int StatusCode { get; init; }
}