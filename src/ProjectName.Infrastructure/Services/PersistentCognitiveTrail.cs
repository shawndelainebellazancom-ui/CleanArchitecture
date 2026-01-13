using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProjectName.Core.Interfaces;
using ProjectName.Infrastructure.Data;

namespace ProjectName.Infrastructure.Services;

public class PersistentCognitiveTrail : ICognitiveTrail
{
    private readonly CognitiveDbContext _db;
    private readonly ILogger<PersistentCognitiveTrail> _logger;

    public PersistentCognitiveTrail(CognitiveDbContext db, ILogger<PersistentCognitiveTrail> logger)
    {
        _db = db;
        _logger = logger;
    }

    public void Record(string phase, object data)
    {
        var json = JsonSerializer.Serialize(data);
        _db.Logs.Add(new CognitiveLog { Phase = phase, DataJson = json });
        _db.SaveChanges(); // Synchronous for simplicity in this cycle
        _logger.LogInformation("DB TRAIL [{Phase}]: {Json}", phase, json);
    }

    public string GetHistory()
    {
        // Simple dump of last 50 logs
        var logs = _db.Logs.OrderByDescending(x => x.Timestamp).Take(50).ToList();
        return JsonSerializer.Serialize(logs);
    }
}