
using Microsoft.OpenApi;
using ProjectName.PlanerService;
using ProjectName.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PMCR-O Orchestration API",
        Version = "v1",
        Description = "REST API Gateway for the PMCR-O Agent Framework"
    });
});

// Add gRPC client for Planner service
builder.Services.AddGrpcClient<Planner.PlannerClient>(options =>
{
    // Service discovery through Aspire
    var plannerUrl = builder.Configuration.GetConnectionString("planner-service")
        ?? "https://localhost:7035";

    options.Address = new Uri(plannerUrl);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    // For development - accept self-signed certificates
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

// Add CORS for development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });
}

// Add Aspire service defaults
builder.AddServiceDefaults();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PMCR-O API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
    });

    app.UseCors();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Map default endpoints (health checks from Aspire)
app.MapDefaultEndpoints();

app.Run();