using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
var signalr = builder.AddAzureSignalR("signalr");

if (builder.Environment.IsDevelopment())
{
    signalr.RunAsEmulator();
    cache.WithRedisInsight();
}

var backend = builder.AddProject<Projects.HouseParty_Server>("backend")
    .WithReference(cache)
    .WithReference(signalr)
    .WaitFor(cache)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var frontend = builder.AddViteApp("frontend", "../frontend")
    .WithReference(backend)
    .WithEnvironment("VITE_BACKEND_API_URL", backend.GetEndpoint("http"))
    .WaitFor(backend);

backend.PublishWithContainerFiles(frontend, "wwwroot");

builder.Build().Run();
