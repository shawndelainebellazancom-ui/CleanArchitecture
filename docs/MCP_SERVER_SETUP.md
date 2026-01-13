# .NET MCP Server Setup Guide for PMCR-O

## What You're Building

A **fully type-safe, .NET-native MCP server** with Playwright browser automation, integrated directly into your Aspire PMCR-O framework.

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                  Aspire AppHost                         │
│  ┌──────────┐  ┌──────────┐  ┌────────────────────┐   │
│  │ Planner  │  │   API    │  │   MCP Server       │   │
│  │ Service  │──┤ Gateway  │  │   (.NET 10)        │   │
│  │ (gRPC)   │  │  (REST)  │  │                    │   │
│  └──────────┘  └──────────┘  │  ┌──────────────┐  │   │
│       │                       │  │ Playwright   │  │   │
│       └───────────────────────┼─►│ Tools (6)    │  │   │
│                               │  └──────────────┘  │   │
│                               └────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

## Step-by-Step Setup

### Step 1: Create Project Structure

```powershell
cd T:\agents\ProjectName\src

# Create project directory
mkdir ProjectName.McpServer
cd ProjectName.McpServer

# Create subdirectories
mkdir Tools
mkdir Services
mkdir Properties
```

### Step 2: Copy Files from Artifacts

Copy each artifact to the correct location:

| Artifact File | Destination |
|---------------|-------------|
| `ProjectName.McpServer.csproj` | `src/ProjectName.McpServer/` |
| `Program.cs` | `src/ProjectName.McpServer/` |
| `appsettings.json` | `src/ProjectName.McpServer/` |
| `McpToolBase.cs` | `src/ProjectName.McpServer/Tools/` |
| `BrowserNavigateTool.cs` | `src/ProjectName.McpServer/Tools/` |
| `BrowserExtractTool.cs` | `src/ProjectName.McpServer/Tools/` |
| `BrowserScreenshotTool.cs` | `src/ProjectName.McpServer/Tools/` |
| `BrowserInteractionTools.cs` | `src/ProjectName.McpServer/Tools/` |
| `PlaywrightManager.cs` | `src/ProjectName.McpServer/Services/` |
| `McpServerService.cs` | `src/ProjectName.McpServer/Services/` |

### Step 3: Add Project to Solution

```powershell
cd T:\agents\ProjectName

# Add to solution
dotnet sln add src\ProjectName.McpServer\ProjectName.McpServer.csproj

# Add project reference to AppHost
cd src\ProjectName.AppHost
dotnet add reference ..\ProjectName.McpServer\ProjectName.McpServer.csproj
```

### Step 4: Install Playwright

```powershell
cd T:\agents\ProjectName\src\ProjectName.McpServer

# Restore packages
dotnet restore

# Build project
dotnet build

# Install Playwright browsers
pwsh bin\Debug\net10.0\playwright.ps1 install chromium
```

### Step 5: Update AppHost.cs

Replace your `src/ProjectName.AppHost/AppHost.cs` with the updated version from artifacts.

Key change:
```csharp
// Add .NET MCP Server
var mcpServer = builder.AddProject<Projects.ProjectName_McpServer>("mcp-server");

// Reference it in Planner
var plannerService = builder.AddProject<Projects.ProjectName_PlanerService>("planner-service")
    .WithReference(redis)
    .WithReference(mcpServer);  // ✅ Type-safe MCP reference
```

### Step 6: Build and Run

```powershell
cd T:\agents\ProjectName

# Clean build
dotnet clean
dotnet build

# Run via Aspire
cd src\ProjectName.AppHost
dotnet run
```

## Verify It's Working

### 1. Check Aspire Dashboard

Open `https://localhost:17001`

You should see:
- ✅ `mcp-server` - Running
- ✅ `planner-service` - Running
- ✅ `orchestration-api` - Running
- ✅ `redis` - Running
- ✅ `postgres` - Running

### 2. Test MCP Tools Endpoint

```powershell
# List available tools
curl http://localhost:5000/tools
```

Expected response:
```json
{
  "tools": [
    {
      "name": "browser_navigate",
      "description": "Navigate to a URL in the headless browser",
      "inputSchema": { ... }
    },
    {
      "name": "browser_click",
      "description": "Click an element on the page using a CSS selector",
      "inputSchema": { ... }
    },
    // ... 4 more tools
  ]
}
```

### 3. Test Browser Navigation

```powershell
curl -X POST http://localhost:5000/mcp `
  -H "Content-Type: application/json" `
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "id": "1",
    "params": {
      "name": "browser_navigate",
      "arguments": {
        "url": "https://anthropic.com"
      }
    }
  }'
```

Expected response:
```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "result": {
    "content": [{
      "type": "text",
      "text": "{\"success\":true,\"url\":\"https://anthropic.com\",\"title\":\"Anthropic\",\"statusCode\":200}"
    }]
  }
}
```

### 4. Test via Planner

```powershell
# Create a plan that uses browser automation
curl -X POST https://localhost:7269/api/orchestration/plan `
  -H "Content-Type: application/json" `
  -d '{
    "intent": "Navigate to anthropic.com and take a screenshot"
  }'
```

## Available MCP Tools

Your MCP server now has 6 fully functional tools:

### 1. browser_navigate
Navigate to any URL
```json
{
  "url": "https://example.com",
  "waitUntil": "load",
  "timeout": 30000
}
```

### 2. browser_click
Click elements
```json
{
  "selector": "button.submit",
  "button": "left",
  "clickCount": 1
}
```

### 3. browser_type
Type into inputs
```json
{
  "selector": "input[name='search']",
  "text": "search query",
  "clear": true
}
```

### 4. browser_screenshot
Capture screenshots
```json
{
  "path": "screenshot.png",
  "fullPage": true,
  "format": "png"
}
```

### 5. browser_extract
Extract data from page
```json
{
  "selector": "h1",
  "format": "text",
  "extractAll": false
}
```

### 6. browser_evaluate
Run JavaScript
```json
{
  "script": "document.title"
}
```

## Integration with PlannerAgent

Now your PlannerAgent can use these tools! Update your `McpClientConfiguration.cs`:

```csharp
public static IServiceCollection AddMcpClient(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddHttpClient<IMcpToolExecutor, McpToolExecutor>((sp, client) =>
    {
        // Aspire resolves "mcp-server" automatically!
        var endpoint = configuration.GetConnectionString("mcp-server") 
            ?? "http://localhost:5000";
        
        client.BaseAddress = new Uri(endpoint);
    });

    services.AddSingleton<IMcpToolExecutor, McpToolExecutor>();

    return services;
}
```

## Project Structure

```
ProjectName.McpServer/
├── Program.cs                      (Entry point, MCP endpoints)
├── appsettings.json               (Configuration)
├── ProjectName.McpServer.csproj   (Project file)
├── Services/
│   ├── PlaywrightManager.cs       (Browser lifecycle)
│   └── McpServerService.cs        (MCP protocol handler)
└── Tools/
    ├── McpToolBase.cs             (Base class for tools)
    ├── BrowserNavigateTool.cs     (Navigate)
    ├── BrowserExtractTool.cs      (Extract data)
    ├── BrowserScreenshotTool.cs   (Screenshots)
    └── BrowserInteractionTools.cs (Click, Type, Evaluate)
```

## Benefits You Get

### 1. Type Safety
```csharp
// Compile-time checking!
var result = await tool.ExecuteAsync(new BrowserNavigateInput
{
    Url = "https://example.com"  // ✅ IntelliSense works
});
```

### 2. Unified Debugging
```
F5 → Debug entire flow:
PlannerAgent → McpServerService → BrowserNavigateTool → Playwright
```

### 3. Shared Infrastructure
```csharp
// Same ILogger, IConfiguration across all services
public BrowserNavigateTool(
    PlaywrightManager playwright,
    ILogger<BrowserNavigateTool> logger)  // ✅ Same patterns
```

### 4. Aspire Integration
- All logs in one dashboard
- Service discovery works automatically
- Health checks included
- Telemetry built-in

## Next Steps

### Phase M: Implement Tool Execution

Now that you have MCP tools, implement **Phase M (Make)** in your Orchestrator:

```csharp
// In Orchestrator.cs
public async Task<string> ProcessIntent(string seed, CancellationToken ct = default)
{
    // Phase P: Plan
    var plan = await _planner.CreatePlanAsync(seed, ct);
    
    // Phase M: Make (NEW!)
    foreach (var step in plan.Steps)
    {
        var args = step.ArgumentsJson.ToMcpArguments();
        var result = await _mcpExecutor.ExecuteToolAsync(step.Tool, args, ct);
        
        _trail.Record("Make", new { 
            Step = step.Order, 
            Tool = step.Tool, 
            Success = result.Success 
        });
    }
    
    // TODO: Phase C: Check
    // TODO: Phase R: Reflect
    // TODO: Phase O: Optimize
    
    return "Execution complete";
}
```

### Add More Tools

Create custom tools specific to PMCR-O:

1. **FileSystemTool** - Read/write artifacts
2. **GitTool** - Version control operations
3. **DatabaseTool** - Query cognitive trail
4. **ApiTool** - Call external APIs

Just extend `McpToolBase<TInput, TOutput>`!

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Playwright install fails | Run: `pwsh bin/Debug/net10.0/playwright.ps1 install chromium` |
| Browser won't start | Check headless mode in appsettings.json |
| MCP endpoint 404 | Verify service is running in Aspire dashboard |
| Type errors | Rebuild solution: `dotnet build` |

## Summary

You now have:
- ✅ .NET MCP Server project created
- ✅ 6 Playwright browser tools implemented
- ✅ Full type safety and IntelliSense
- ✅ Integrated with Aspire
- ✅ Ready for Phase M (Make) implementation

Your PMCR-O framework can now **plan AND execute** browser automation tasks with full observability!

Ready to test it? Run:
```powershell
cd src\ProjectName.AppHost
dotnet run
```