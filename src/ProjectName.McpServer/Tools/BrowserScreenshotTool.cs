using ProjectName.McpServer.Services;

namespace ProjectName.McpServer.Tools;

/// <summary>
/// Capture screenshots of the current page or specific elements
/// </summary>
public class BrowserScreenshotTool : McpToolBase<BrowserScreenshotInput, BrowserScreenshotOutput>
{
    private readonly PlaywrightManager _playwright;
    private readonly IConfiguration _config;

    public BrowserScreenshotTool(
        PlaywrightManager playwright,
        IConfiguration config,
        ILogger<BrowserScreenshotTool> logger) : base(logger)
    {
        _playwright = playwright;
        _config = config;
    }

    public override string Name => "browser_screenshot";

    public override string Description => "Capture a screenshot of the current page or a specific element";

    public override async Task<BrowserScreenshotOutput> ExecuteAsync(
        BrowserScreenshotInput input,
        CancellationToken ct = default)
    {
        Logger.LogInformation("Capturing screenshot: {Path}", input.Path ?? "base64");

        var page = await _playwright.GetPageAsync();

        byte[] screenshot;

        // FIX: Playwright throws an error if Quality is set for PNG.
        // We must ensure Quality is null if format is png.
        int? actualQuality = input.Format.Equals("png", StringComparison.OrdinalIgnoreCase)
            ? null
            : input.Quality;

        if (!string.IsNullOrEmpty(input.Selector))
        {
            // Screenshot of specific element
            var element = await page.QuerySelectorAsync(input.Selector);
            if (element == null)
            {
                throw new InvalidOperationException($"Element not found: {input.Selector}");
            }

            screenshot = await element.ScreenshotAsync(new()
            {
                Type = input.Format == "png" ? Microsoft.Playwright.ScreenshotType.Png : Microsoft.Playwright.ScreenshotType.Jpeg,
                Quality = actualQuality
            });
        }
        else
        {
            // Full page screenshot
            screenshot = await page.ScreenshotAsync(new()
            {
                FullPage = input.FullPage,
                Type = input.Format == "png" ? Microsoft.Playwright.ScreenshotType.Png : Microsoft.Playwright.ScreenshotType.Jpeg,
                Quality = actualQuality
            });
        }

        string? filePath = null;
        string? base64Data = null;

        if (!string.IsNullOrEmpty(input.Path))
        {
            // Save to file
            var screenshotsDir = _config["McpSettings:ScreenshotsDirectory"] ?? "./screenshots";
            Directory.CreateDirectory(screenshotsDir);

            filePath = Path.Combine(screenshotsDir, input.Path);
            await File.WriteAllBytesAsync(filePath, screenshot, ct);

            Logger.LogInformation("Screenshot saved to: {Path}", filePath);
        }
        else
        {
            // Return as base64
            base64Data = Convert.ToBase64String(screenshot);
        }

        return new BrowserScreenshotOutput
        {
            Success = true,
            Path = filePath,
            Base64 = base64Data,
            Size = screenshot.Length
        };
    }
}

public record BrowserScreenshotInput
{
    public string? Path { get; init; }
    public string? Selector { get; init; }
    public bool FullPage { get; init; } = false;
    public string Format { get; init; } = "png"; // png or jpeg
    public int? Quality { get; init; } = 90; // JPEG quality 0-100
}

public record BrowserScreenshotOutput
{
    public bool Success { get; init; }
    public string? Path { get; init; }
    public string? Base64 { get; init; }
    public int Size { get; init; }
}