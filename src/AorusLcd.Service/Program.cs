using AorusLcd.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Headless NativeAOT Windows Service: pushes the panel's live sensor feed (E3)
// so the built-in dashboard widgets stay current without the GUI running.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "AorusLcdFeed");
builder.Services.AddHostedService<FeedWorker>();

var host = builder.Build();
host.Run();
