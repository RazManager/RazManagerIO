using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Channels;


WebApplicationBuilder builder;

var snap = System.Environment.GetEnvironmentVariable("SNAP");
if (string.IsNullOrEmpty(snap))
{
    builder = WebApplication.CreateBuilder();
}
else
{
    builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        ContentRootPath = snap
    });
}

builder.Logging.AddProvider(new RazManagerIO.Host.Services.MemoryLogger.MemoryLoggerProvider());

builder.Services.AddSingleton(serviceProvider =>
    new Queue<RazManagerIO.Host.Services.MemoryLogger.MemoryLoggerData>()
);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(3002);
});

builder.Services.AddSingleton(serviceProvider =>
    Channel.CreateUnbounded<bool>(new UnboundedChannelOptions
    {
        SingleWriter = false,
        SingleReader = true
    })
);

builder.Services.AddSingleton<RazManagerIO.Host.Services.CpuInfo.ICpuInfoService>(serviceProvider =>
    new RazManagerIO.Host.Services.CpuInfo.CpuInfoService(serviceProvider.GetRequiredService<ILogger<RazManagerIO.Host.Services.CpuInfo.CpuInfoService>>())
);

builder.Services.AddSingleton<RazManagerIO.Host.Services.OsRelease.IOsReleaseService>(serviceProvider =>
    new RazManagerIO.Host.Services.OsRelease.OsReleaseService(serviceProvider.GetRequiredService<ILogger<RazManagerIO.Host.Services.OsRelease.OsReleaseService>>())
);

//builder.Services.AddSingleton<RazManager.InputOutput.Services.Settings.ISettingsService>(serviceProvider =>
//    new RazManager.InputOutput.Services.Settings.SettingsService
//    (
//        serviceProvider.GetRequiredService<Channel<bool>>(),
//        serviceProvider.GetRequiredService<ILogger<RazManager.InputOutput.Services.Settings.SettingsService>>()
//    )
//);

//builder.Services.AddSingleton(serviceProvider =>
//{
//    if (System.Environment.OSVersion.Platform == System.PlatformID.Unix)
//    {
//        return new System.Device.Gpio.GpioController();
//    }
//    return new System.Device.Gpio.GpioController(System.Device.Gpio.PinNumberingScheme.Logical, new RazManager.InputOutput.Utilities.WindowsDevelopmentGpioDriver());
//});

//builder.Services.AddSingleton<RazManager.InputOutput.Services.Gpio.IGpioService>(serviceProvider =>
//    new RazManager.InputOutput.Services.Gpio.GpioService
//    (
//        serviceProvider.GetRequiredService<System.Device.Gpio.GpioController>(),
//        serviceProvider.GetRequiredService<RazManager.InputOutput.Services.Settings.ISettingsService>(),
//        serviceProvider.GetRequiredService<ILogger<RazManager.InputOutput.Services.Gpio.GpioService>>()
//    )
//);


builder.Services.AddHostedService<RazManagerIO.Host.Services.MemoryLogger.MemoryLoggerService>();
builder.Services.AddHostedService<RazManagerIO.Host.Services.InputOutput.InputOutputService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddSignalR()
    .AddJsonProtocol(options => {
        options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors(builder =>
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader());
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == StatusCodes.Status404NotFound &&
        context.Request.Method == "GET" &&
        context.Request.Path.HasValue &&
        !context.Request.Path.Value.Contains("/api") &&
        !context.Request.Path.Value.Contains("."))
    {
        context.Request.Path = new PathString("/");
        await next();
    }
});

app.UseFileServer();

app.UseRouting();

app.MapControllers();
app.MapHub<RazManagerIO.Host.Hubs.LogHub>("hubs/log");

app.Run();