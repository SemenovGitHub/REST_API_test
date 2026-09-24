using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Dapper;
using FluentValidation;
using Microsoft.OpenApi;
using Npgsql;
using TestJob.Api.Models;
using TestJob.Api.Services;
using TestJob.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'Postgres' is not configured.");
}

DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddScoped<IValidator<ProcessRequest>, ProcessRequestValidator>();
builder.Services.AddScoped<IPageProcessService, PageProcessService>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
    });
var swagger = builder.Configuration.GetSection("Swagger");
var swaggerRoutePrefix = swagger["RoutePrefix"]!;
var swaggerEndpoint = swagger["Endpoint"]!;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.SwaggerDoc(swagger["Version"], new OpenApiInfo
    {
        Title = swagger["Title"],
        Version = swagger["Version"]
    });
});

var app = builder.Build();

await EnsureElementsTableAsync(app.Services);

app.UseSwagger(options =>
{
    options.RouteTemplate = $"{swaggerRoutePrefix}/{{documentName}}/swagger.json";
});
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = swaggerRoutePrefix;
    options.SwaggerEndpoint(swaggerEndpoint, swagger["Title"]);
});
app.MapControllers();
app.Run();

static async Task EnsureElementsTableAsync(IServiceProvider services)
{
    var dataSource = services.GetRequiredService<NpgsqlDataSource>();
    await using var connection = await dataSource.OpenConnectionAsync();
    await connection.ExecuteAsync("""
        CREATE TABLE IF NOT EXISTS elements (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            attribute_value text NOT NULL,
            html text NOT NULL
        );
        """);
}
