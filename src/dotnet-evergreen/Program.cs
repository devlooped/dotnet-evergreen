﻿using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Devlooped;
using DotNetConfig;
using Spectre.Console;
using static Devlooped.Extensions;

const string NuGetFeed = "https://api.nuget.org/v3/index.json";

var config = Config.Build().GetSection("evergreen");

// Only append symbols we can't gather from configuration, which makes it 
// possible to avoid consuming arguments from the tool being executed.
var interval = (int?)config.GetNumber("interval");
var source = config.GetString("source") ?? NuGetFeed;
var arguments = new List<string>(args);

var toolArg = new Argument<string>("tool", "Package Id of tool to run.");
var toolArgs = new Argument<string[]>("args", "Additional arguments and options supported by the tool");
var sourceOpt = new Option<string>(new[] { "-s", "--source", "/s", "/source" }, () => source, "NuGet feed to check for updates.");
var intervalOpt = new Option<int>(new[] { "-i", "--interval", "/i", "/interval" }, () => interval.GetValueOrDefault(5), "Time interval in seconds for the update checks.");
var exitOpt = new Option<bool>(new[] { "-e", "--exit", "/e", "/exit" }, () => config.GetBoolean("exit") ?? true, "Automatically exit if tool exits.");
var forceOpt = new Option<bool>(new[] { "-f", "--force", "/f", "/force" }, () => config.GetBoolean("force") ?? true, "Stop all running tool processes to apply updates.");
var singletonOpt = new Option("--singleton", "Ensure a single tool process is running.");
var quietOpt = new Option(new[] { "-q", "--quiet", "/q", "/quiet" }, "Do not display any informational messages.");
var prereleaseOpt = new Option<bool>(new[] { "-p", "--prerelease", "/p", "/prerelease" }, () => config.GetBoolean("prerelease") ?? false, "Whether to include pre-release packages.");
var helpOpt = new Option(new[] { "-h", "/h", "--help", "-?", "/?" });

// First do full parse to detect tool/source
var parser = new Parser(toolArg, toolArgs, sourceOpt, singletonOpt, intervalOpt, exitOpt, forceOpt, quietOpt, prereleaseOpt, helpOpt);
var result = parser.Parse(args);
var tool = result.ValueForArgument(toolArg);

int ShowHelp() => new CommandLineBuilder(new RootCommand("Run an evergreen version of a tool")
    {
        toolArg,
        toolArgs,
        sourceOpt,
        singletonOpt,
        intervalOpt,
        exitOpt,
        forceOpt,
        prereleaseOpt,
        quietOpt,
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
result = new Parser(sourceOpt, singletonOpt, intervalOpt, exitOpt, forceOpt, quietOpt, prereleaseOpt, helpOpt).Parse(arguments.Take(arguments.IndexOf(tool!)).ToArray());

if (result.FindResultFor(helpOpt) != null)
    return ShowHelp();

var quiet = result.FindResultFor(quietOpt) != null;
var singleton = result.FindResultFor(singletonOpt) != null;
var exitOnToolExit = result.ValueForOption(exitOpt);
var force = result.ValueForOption(forceOpt);
var prerelease = result.ValueForOption(prereleaseOpt);
var app = new Application(quiet);

interval = result.ValueForOption(intervalOpt);
source = result.ValueForOption(sourceOpt);

if (!Tools.TryCreate(source!, quiet, prerelease, out var tools) || tools == null)
    return Error("Failed to locate dotnet");

// Ensure dotnet-stop is installed, from default source
if (!await new Tools(tools.DotNetPath, quiet, prerelease).InstallOrUpdateAsync("dotnet-stop"))
    // Whatever tool install/update error would have already been written to output at this point.
    return Exit();

if (!await tools.InstallOrUpdateAsync(tool!, firstRun: true, force: force))
    return Exit();

var info = tools.Installed.First(x => x.PackageId == tool);

// We actually run the provided command from the tool, not the package id
var command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", info.Commands);
if (Environment.OSVersion.Platform == PlatformID.Win32NT)
    command += ".exe";

if (!File.Exists(command))
    return Error($"Tool '{tool}' not found at expected location '{command}'");

// From index of command forward, pass it on as-is to the tool.
var start = new ProcessStartInfo(command, string.Join(' ', arguments.Skip(arguments.IndexOf(tool!) + 1)));
var toolCancellation = new CancellationTokenSource();
var process = app.Start(start, toolCancellation, singleton);
process.Exited += OnToolExit;

// Declare first so we can use it in CheckUpdates
Timer? timer = null;
timer = new Timer(_ => CheckUpdates(), default, TimeSpan.FromSeconds((double)interval), TimeSpan.FromSeconds((double)interval));

if (!app.ShutdownToken.IsCancellationRequested && !quiet)
    AnsiConsole.MarkupLine("[grey]Press Ctrl+C to exit.[/]");

while (!app.ShutdownToken.IsCancellationRequested)
    Thread.Sleep(100);

return Environment.ExitCode;

void OnToolExit(object? sender, EventArgs args)
{
    if (exitOnToolExit)
        app?.Stop();

    if (process != null)
        process.Exited -= OnToolExit;
}

void CheckUpdates()
{
    // Disable timer while we update, since this could take a while, depending on the tool size.
    timer?.Change(Timeout.Infinite, Timeout.Infinite);
    try
    {
        Task.Run(async () =>
        {
            var update = await tools.FindUpdateAsync(info.PackageId, info.Version);
            if (update != null)
            {
                if (!quiet)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[yellow]Update v{update.ToNormalizedString()} found.[/]");
                }

                var exitOption = exitOnToolExit;
                // Disable auto-exit while we update, since otherwise we'd never be able to apply updates
                exitOnToolExit = false;

                try
                {
                    // Causes the running tool to be stopped while we update. See Application.Start.
                    toolCancellation.Cancel();

                    // Make sure we don't trigger update until process is entirely gone.
                    process.Stop(5000);

                    if (!tools.Update(tool!, force: force))
                        return Exit($"Failed to update {tool}");

                    info = tools.Installed.First(x => x.Commands == tool || x.PackageId == tool);
                    // Restart the updated tool.
                    start = new ProcessStartInfo(info.Commands, string.Join(' ', arguments.Skip(arguments.IndexOf(tool!) + 1)));
                    toolCancellation = new CancellationTokenSource();
                    process = app.Start(start, toolCancellation, singleton);
                    process.Exited += OnToolExit;
                }
                finally
                {
                    exitOnToolExit = exitOption;
                }
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
