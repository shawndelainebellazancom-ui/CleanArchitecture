using Microsoft.Extensions.Logging;
using ProjectName.Core.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ProjectName.Infrastructure.Services;

public class InMemoryCognitiveTrail : ICognitiveTrail
{
    private readonly ConcurrentBag<object> _history = new();
    private readonly ILogger<InMemoryCognitiveTrail> _logger;

    public InMemoryCognitiveTrail(ILogger<InMemoryCognitiveTrail> logger)
    {
        _logger = logger;
    }

    public void Record(string phase, object data)
    {
        var entry = new { Timestamp = DateTime.UtcNow, Phase = phase, Data = data };
        _history.Add(entry);
        _logger.LogInformation("TRAIL [{Phase}]: {Json}", phase, JsonSerializer.Serialize(data));
    }

    public string GetHistory()
    {
        return JsonSerializer.Serialize(_history);
    }
}