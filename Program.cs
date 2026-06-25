using System.Reflection;
using SideXP.Instadoc.Commands;
using Spectre.Console.Cli;

var app = new CommandApp<GenerateCommand>();

app.Configure(config =>
{
    config.SetApplicationName("instadoc");

    var version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion;
    if (!string.IsNullOrWhiteSpace(version))
    {
        config.SetApplicationVersion(version);
    }

    config.AddExample("--input", "./path/to/folder", "--output", "./docs/api", "--nav");
});

return app.Run(args);