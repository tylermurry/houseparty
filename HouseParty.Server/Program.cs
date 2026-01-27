using HouseParty.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisClient("cache");

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSingleton<RoomService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        if (!builder.Environment.IsDevelopment()) return;
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("frontend");
app.UseWebSockets();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();

app.MapDefaultEndpoints();
app.UseFileServer();
app.Run();
