using enfasis_color;

Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices(static (hostContext, services) =>
    {
        IConfiguration configuration = hostContext.Configuration;
        SID? sid = configuration.GetSection("SID").Get<SID>();

        services.AddSingleton(sid);
        services.AddHostedService<Worker>();
    })
    .Build()
    .Run();