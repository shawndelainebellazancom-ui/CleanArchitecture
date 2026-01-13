using ProjectName.McpServer.Services;

namespace ProjectName.McpServer.Tools;

/// <summary>
/// Click an element on the page
/// </summary>
public class BrowserClickTool(
    PlaywrightManager playwright,
    ILogger<BrowserClickTool> logger) : McpToolBase<BrowserClickInput, BrowserClickOutput>(logger)
{
    private readonly PlaywrightManager _playwright = playwright;

    public override string Name => "browser_click";

    public override string Description => "Click an element on the page using a CSS selector";

    public override async Task<BrowserClickOutput> ExecuteAsync(
        BrowserClickInput input,
        CancellationToken ct = default)
    {
        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation("Clicking element: {Selector}", input.Selector);
        }

        var page = await _playwright.GetPageAsync();

        await page.ClickAsync(input.Selector, new()
        {
            Button = input.Button switch
            {
                "right" => Microsoft.Playwright.MouseButton.Right,
                "middle" => Microsoft.Playwright.MouseButton.Middle,
                _ => Microsoft.Playwright.MouseButton.Left
            },
            ClickCount = input.ClickCount ?? 1,
            Delay = input.Delay,
            Timeout = input.Timeout ?? 30000
        });

        Logger.LogInformation("Clicked element successfully");

        return new BrowserClickOutput
        {
            Success = true,
            Selector = input.Selector
        };
    }
}

public record BrowserClickInput
{
    public string Selector { get; init; } = "";
    public string? Button { get; init; } = "left"; // left, right, middle
    public int? ClickCount { get; init; } = 1;
    public float? Delay { get; init; }
    public int? Timeout { get; init; } = 30000;
}

public record BrowserClickOutput
{
    public bool Success { get; init; }
    public string Selector { get; init; } = "";
}

/// <summary>
/// Type text into an input element
/// </summary>
public class BrowserTypeTool(
    PlaywrightManager playwright,
    ILogger<BrowserTypeTool> logger) : McpToolBase<BrowserTypeInput, BrowserTypeOutput>(logger)
{
    private readonly PlaywrightManager _playwright = playwright;

    public override string Name => "browser_type";

    public override string Description => "Type text into an input field";

    public override async Task<BrowserTypeOutput> ExecuteAsync(
        BrowserTypeInput input,
        CancellationToken ct = default)
    {
        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation("Typing into element: {Selector}", input.Selector);
        }

        var page = await _playwright.GetPageAsync();

        if (input.Clear)
        {
            await page.FillAsync(input.Selector, "");
        }

        // Use FillAsync instead of deprecated TypeAsync
        await page.FillAsync(input.Selector, input.Text);

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation("Typed {Length} characters successfully", input.Text.Length);
        }

        return new BrowserTypeOutput
        {
            Success = true,
            Selector = input.Selector,
            CharactersTyped = input.Text.Length
        };
    }
}

public record BrowserTypeInput
{
    public string Selector { get; init; } = "";
    public string Text { get; init; } = "";
    public bool Clear { get; init; } = false;
    public float? Delay { get; init; } = 50; // Delay between keystrokes in ms
    public int? Timeout { get; init; } = 30000;
}

public record BrowserTypeOutput
{
    public bool Success { get; init; }
    public string Selector { get; init; } = "";
    public int CharactersTyped { get; init; }
}

/// <summary>
/// Evaluate JavaScript in the browser context
/// </summary>
public class BrowserEvaluateTool(
    PlaywrightManager playwright,
    ILogger<BrowserEvaluateTool> logger) : McpToolBase<BrowserEvaluateInput, BrowserEvaluateOutput>(logger)
{
    private readonly PlaywrightManager _playwright = playwright;

    public override string Name => "browser_evaluate";

    public override string Description => "Execute JavaScript code in the browser context";

    public override async Task<BrowserEvaluateOutput> ExecuteAsync(
        BrowserEvaluateInput input,
        CancellationToken ct = default)
    {
        Logger.LogInformation("Evaluating JavaScript in browser");

        var page = await _playwright.GetPageAsync();

        var result = await page.EvaluateAsync<object>(input.Script);

        Logger.LogInformation("JavaScript evaluation completed");

        return new BrowserEvaluateOutput
        {
            Success = true,
            Result = result
        };
    }
}

public record BrowserEvaluateInput
{
    public string Script { get; init; } = "";
}

public record BrowserEvaluateOutput
{
    public bool Success { get; init; }
    public object? Result { get; init; }
}