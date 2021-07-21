using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Devlooped;
using DotNetConfig;
using NuGet.Versioning;
using Spectre.Console;
using static Devlooped.Extensions;

const string NuGetFeed = "https://api.nuget.org/v3/index.json";

var config = Config.Build().GetSection("evergreen");
var app = new Application();

// Only append symbols we can't gather from configuration, which makes it 
// possible to avoid consuming arguments from the tool being executed.
var interval = (int?)config.GetNumber("interval");
var source = config.GetString("source") ?? NuGetFeed;
var arguments = new List<string>(args);

var toolArg = new Argument<string>("tool", "Package Id of tool to run.");
var toolArgs = new Argument<string[]>("args", "Additional arguments and options supported by the tool");
var sourceOpt = new Option<string>(new[] { "-s", "--source", "/s", "/source" }, () => source, "NuGet feed to check for updates.");
var intervalOpt = new Option<int>(new[] { "-i", "--interval", "/i", "/interval" }, () => interval.GetValueOrDefault(5), "Time interval in seconds for the update checks.");
var helpOpt = new Option(new[] { "-h", "/h", "--help", "-?", "/?" });

var symbols = new List<Symbol>
{
    toolArg,
    toolArgs,
    intervalOpt,
    sourceOpt,
    helpOpt,
};

// First do full parse to detect tool/source
var parser = new Parser(symbols.ToArray());
var result = parser.Parse(args);
var tool = result.ValueForArgument(toolArg);

int ShowHelp() => new CommandLineBuilder(new RootCommand("Run an evergreen version of a tool")
    {
        toolArg,
        toolArgs,
        sourceOpt,
        intervalOpt,
    })
    .UseHelpBuilder(context => new ToolHelpBuilder(context.Console))
    .UseHelp()
    .UseVersionOption()
    .Build()
    .Invoke("-h");

if (string.IsNullOrEmpty(tool))
    return ShowHelp();   

// Now that we know we have a tool command, strip the arguments *after* the tool 
// and re-parse the options, just so we don't accidentally grab a tool option as ours.
result = new Parser(sourceOpt, intervalOpt, helpOpt).Parse(arguments.Take(arguments.IndexOf(tool!)).ToArray());

if (result.FindResultFor(helpOpt) != null)
    return ShowHelp();

interval = result.ValueForOption(intervalOpt);
source = result.ValueForOption(sourceOpt);

if (!Tools.TryCreate(out var tools) || tools == null)
    return Error("Failed to locate dotnet");

// Ensure dotnet-stop is installed
if (!await tools.InstallOrUpdateAsync("dotnet-stop"))
    // Whatever tool install/update error would have already been written to output at this point.
    return Exit();

if (!await tools.InstallOrUpdateAsync(tool))
    return Exit();

var info = tools.Installed.First(x => x.PackageId == tool);
var packageId = info.PackageId;

// We actually run the provided command from the tool, not the package id
var command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", info.Commands);
if (Environment.OSVersion.Platform == PlatformID.Win32NT)
    command += ".exe";

if (!File.Exists(command))
    return Error($"Tool '{tool}' not found at expected location '{command}'");

// From index of command forward, pass it on as-is to the tool.
var start = new ProcessStartInfo(command, string.Join(' ', arguments.Skip(arguments.IndexOf(tool!) + 1)));
var toolCancellation = new CancellationTokenSource();
var process = app.Start(start, toolCancellation);

// Declare first so we can use it in CheckUpdates
Timer? timer = null;
timer = new Timer(_ => CheckUpdates(), default, TimeSpan.FromSeconds((double)interval), TimeSpan.FromSeconds((double)interval));

if (!app.ShutdownToken.IsCancellationRequested)
    AnsiConsole.MarkupLine("[grey]Press Ctrl+C to exit.[/]");

while (!app.ShutdownToken.IsCancellationRequested)
    Thread.Sleep(100);

return Environment.ExitCode;

void CheckUpdates()
{
    // Disable timer while we update, since this could take a while, depending on the tool size.
    timer?.Change(Timeout.Infinite, Timeout.Infinite);
    try
    {
        Task.Run(async () =>
        {
            var update = await tools.FindUpdateAsync(info.PackageId, info.Version, source!);
            if (update != null)
            {
                AnsiConsole.MarkupLine($"[yellow]Update v{update.ToNormalizedString()} found.[/]");
                // Causes the running tool to be stopped while we update. See Application.Start.
                toolCancellation.Cancel();

                if (!tools.Update(tool))
                    return Exit($"Failed to update {tool}");

                info = tools.Installed.First(x => x.Commands == tool || x.PackageId == tool);
                // Restart the updated tool.
                start = new ProcessStartInfo(info.Commands, string.Join(' ', arguments.Skip(arguments.IndexOf(tool!) + 1)));
                toolCancellation = new CancellationTokenSource();
                process = app.Start(start, toolCancellation);
            }
            return 0;
        }).Wait();
    }
    finally
    {
        // Restart timer now.
        timer?.Change(TimeSpan.FromSeconds((double)interval), TimeSpan.FromSeconds((double)interval));
    }
}

record Tool(string PackageId, NuGetVersion Version, string Commands);

/// <summary>
/// Customizes the help output so the tool command gets "dotnet" prepended, 
/// since that's the actual usage.
/// </summary>
class ToolHelpBuilder : HelpBuilder
{
    public ToolHelpBuilder(IConsole console) : base(new EvergreenConsole(console)) { }

    protected override void AddSynopsis(ICommand command)
    {
        WriteHeading("dotnet " + command.Name, command.Description);
        Console.Out.Write(Environment.NewLine);
    }

    protected override void AddUsage(ICommand command)
    {
        WriteHeading(Resources.Instance.HelpUsageTile(), "dotnet " + GetUsage(command));
        Console.Out.Write(Environment.NewLine);
    }

    class EvergreenConsole : IConsole
    {
        readonly IConsole console;
        readonly IStandardStreamWriter writer;

        public EvergreenConsole(IConsole console) 
        {
            this.console = console;
            writer = new EvergreenWriter(console.Out);
        }

        public IStandardStreamWriter Out => writer;

        public bool IsOutputRedirected => console.IsOutputRedirected;

        public IStandardStreamWriter Error => console.Error;

        public bool IsErrorRedirected => console.IsErrorRedirected;

        public bool IsInputRedirected => console.IsInputRedirected;

        class EvergreenWriter : IStandardStreamWriter
        {
            readonly IStandardStreamWriter writer;

            public EvergreenWriter(IStandardStreamWriter writer) => this.writer = writer;

            public void Write(string value)
            {
                var index = value.IndexOf("evergreen");
                if (index == -1)
                {
                    writer.Write(value);
                }
                else
                {
                    writer.Write(value.Substring(0, index));
                    var color = System.Console.ForegroundColor;
                    try
                    {
                        System.Console.ForegroundColor = ConsoleColor.Green;
                        writer.Write("evergreen");
                    }
                    finally
                    {
                        System.Console.ForegroundColor = color;
                    }
                    writer.Write(value.Substring(index + 9));
                }
            }
        }
    }
}