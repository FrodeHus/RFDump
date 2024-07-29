
using Microsoft.Extensions.DependencyInjection;
using QuiCLI;
using RFDump.Command;
using RFDump.Service;

var builder = QuicApp.CreateBuilder();
builder.Services.AddTransient<SerialService>();
var app = builder.Build();

app.AddCommand((sp) => new DumpCommand(sp.GetRequiredService<SerialService>()));
app.AddCommand((sp) => new PortCommand(sp.GetRequiredService<SerialService>()));
app.Run();