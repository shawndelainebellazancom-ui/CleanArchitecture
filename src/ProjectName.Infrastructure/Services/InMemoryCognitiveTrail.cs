using Microsoft.Extensions.Logging;
using ProjectName.Core.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ProjectName.Infrastructure.Services;

public class InMemoryCognitiveTrail(ILogger<InMemoryCognitiveTrail> logger) : ICognitiveTrail
{
    private readonly ConcurrentBag<object> _history = [];

    public void Record(string phase, object data)
    {
        var entry = new { Timestamp = DateTime.UtcNow, Phase = phase, Data = data };
        _history.Add(entry);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("TRAIL [{Phase}]: {Json}", phase, JsonSerializer.Serialize(data));
        }
    }

    public string GetHistory()
    {
        return JsonSerializer.Serialize(_history);
    }

    public Task ClearAsync()
    {
        _history.Clear();
        return Task.CompletedTask;
    }
}