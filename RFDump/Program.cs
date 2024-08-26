
using Microsoft.Extensions.DependencyInjection;

using QuiCLI;

using RFDump;
using RFDump.Command;
using RFDump.Service;

var builder = QuicApp.CreateBuilder();
builder.Configure(config => config.CustomBanner = () =>
{
    Configure.PrintBanner();
    return string.Empty;
});

builder.Services.AddTransient<SerialService>();
builder.Commands.AddCommand<DumpCommand>("dump").UseMethod(x => x.Dump);
builder.Commands.AddCommand<PortCommand>("port").UseMethod(x => x.Ports);
var app = builder.Build();

app.Run();