var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.NpAspire_Api>("api")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
