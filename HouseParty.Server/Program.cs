using HouseParty.Common;
using StackExchange.Redis;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.AddRedisClient("cache");
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("frontend");

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

var api = app.MapGroup("/api");

api.MapPost("person", async (string firstName, string lastName, IConnectionMultiplexer redis) =>
{
    var person = new Person(Guid.NewGuid().ToString("n"), firstName, lastName);
    await redis.GetDatabase().StringSetAsync(person.id, JsonSerializer.Serialize(person));

    return Results.Created($"/api/person?id={person.id}", person);
})
.WithName("SavePerson");

api.MapGet("person", async (string id, IConnectionMultiplexer redis) =>
{
    var payload = await redis.GetDatabase().StringGetAsync(id);
    if (payload.IsNullOrEmpty)
        return Results.NotFound();

    var person = JsonSerializer.Deserialize<Person>(payload!.ToString());
    return person is null ? Results.NotFound() : Results.Ok(person);
})
.WithName("GetPerson");

app.MapDefaultEndpoints();
app.UseFileServer();
app.Run();
