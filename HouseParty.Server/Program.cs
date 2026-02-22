using HouseParty.GameEngine;
using HouseParty.GameEngine.Archetypes;
using HouseParty.GameEngine.Operations;
using HouseParty.GameEngine.Primitives;
using HouseParty.Server.Services;
using Microsoft.Azure.SignalR.Management;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisClient("cache");

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSingleton<RoomService>();
builder.Services.AddSingleton<IRoomSignalRService, RoomSignalRService>();
builder.Services.AddSingleton<IPolicies, Policies>();

// Primitives
builder.Services.AddSingleton<IPrimitives, Primitives>();

// Operations
builder.Services.AddSingleton<IGameOperations, GameOperations>();
builder.Services.AddSingleton<ICommitOperations, CommitOperations>();
builder.Services.AddSingleton<IExclusiveOperations, ExclusiveOperations>();
builder.Services.AddSingleton<IContestedOperations, ContestedOperations>();

// Archetypes
builder.Services.AddSingleton<IBaseGame, BaseGame>();
builder.Services.AddSingleton<ITurnBasedGame, TurnBasedGame>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("houseparty-frontend", policy =>
    {
        if (!builder.Environment.IsDevelopment()) return;
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// Serverless SignalR Setup
builder.Services.AddSingleton<IServiceManager>(_ =>
    (IServiceManager)new ServiceManagerBuilder()
        .WithOptions(options => options.ConnectionString = builder.Configuration.GetConnectionString("signalr"))
        .BuildServiceManager()
    );

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("houseparty-frontend");
app.UseWebSockets();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();

app.MapDefaultEndpoints();
app.UseFileServer();
app.Run();
