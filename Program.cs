using enfasis_color;

Host.CreateDefaultBuilder(args)
    .UseWindowsService() // ← ¡Este es clave!
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<Worker>();
    })
    .Build()
    .Run();