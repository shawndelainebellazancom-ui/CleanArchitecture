using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProjectName.Application.Interfaces;
using ProjectName.Infrastructure.Data;

namespace ProjectName.Infrastructure.Services;

public class PersistentCognitiveTrail(CognitiveDbContext db, ILogger<PersistentCognitiveTrail> logger) : ICognitiveTrail
{
    public void Record(string phase, object data)
    {
        var json = JsonSerializer.Serialize(data);
        db.Logs.Add(new CognitiveLog { Phase = phase, DataJson = json });
        db.SaveChanges();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("DB TRAIL [{Phase}]: {Json}", phase, json);
        }
    }

    public string GetHistory()
    {
        var logs = db.Logs.OrderByDescending(x => x.Timestamp).Take(50).ToList();
        return JsonSerializer.Serialize(logs);
    }

    public async Task ClearAsync()
    {
        db.Logs.RemoveRange(db.Logs);
        await db.SaveChangesAsync();
    }
}