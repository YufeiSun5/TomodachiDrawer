using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TomodachiDrawerCn.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
