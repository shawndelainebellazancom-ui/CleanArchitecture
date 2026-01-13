using Microsoft.EntityFrameworkCore;
using ProjectName.Core.Entities;

namespace ProjectName.Infrastructure.Data;

public class CognitiveDbContext : DbContext
{
    public CognitiveDbContext(DbContextOptions<CognitiveDbContext> options) : base(options) { }

    public DbSet<CognitiveLog> Logs { get; set; }
}

// Simple Entity for DB
public class CognitiveLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Phase { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
}