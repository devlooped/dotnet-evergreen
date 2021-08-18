using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Spectre.Console;
using static Devlooped.Extensions;

namespace Devlooped
{
    public record Tool(string PackageId, NuGetVersion Version, string Commands);

    public class Tools
    {
        public const string DefaultPackageFeed = "https://api.nuget.org/v3/index.json";

        public static bool TryCreate(string packageFeed, bool quiet, out Tools? tools)
        {
            tools = default;
            var dotnet = DotnetMuxer.Path?.FullName;
            if (dotnet == null)
                return false;

            tools = new Tools(dotnet, quiet, packageFeed);
            return true;
        }

        readonly string dotnet;
        readonly bool quiet;
        readonly string packageFeed;
        readonly string feedArg = "";

        public Tools(string dotnet, bool quiet, string packageFeed = DefaultPackageFeed)
        {
            this.dotnet = dotnet;
            this.quiet = quiet;
            this.packageFeed = packageFeed;
            if (packageFeed != DefaultPackageFeed)
                feedArg = "--add-source " + packageFeed + " ";
        }

        public string DotNetPath => dotnet;

        public List<Tool> Installed { get; private set; } = new List<Tool>();

        public async Task<NuGetVersion?> FindUpdateAsync(string packageId, NuGetVersion localVersion)
        {
            var providers = Repository.Provider.GetCoreV3();
            var repository = new SourceRepository(new PackageSource(packageFeed), providers);
            var resource = await repository.GetResourceAsync<PackageMetadataResource>().ConfigureAwait(false);
            var metadata = await resource.GetMetadataAsync(packageId, false, false,
                new SourceCacheContext
                {
                    NoCache = true,
                    RefreshMemoryCache = true,
                },
                NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);

            var update = metadata
                .Select(x => x.Identity)
                .Where(x => x.Version > localVersion)
                .OrderByDescending(x => x.Version)
                .Select(x => x.Version)
                .FirstOrDefault();

            return update;
        }

        public bool Install(string packageId)
            => RunToolCommand("tool install -g --no-cache " + feedArg + packageId, "Installing " + packageId);

        public bool Update(string packageId, bool force)
        {
            if (RunToolCommand("tool update -g --no-cache " + feedArg + packageId, "Updating " + packageId))
                return true;
            else if (!force)
                return false;


            if (Installed.FirstOrDefault(x => x.PackageId == packageId) is var tool && tool != null)
            {
                // Otherwise, lookup all running tools, stop them all, and retry update.
                var executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? tool.Commands + ".exe" : tool.Commands;

                foreach (var process in Process.GetProcessesByName(executable))
                    process.Stop(2000);

                // Retry once more.
                return RunToolCommand("tool update -g --no-cache " + feedArg + packageId, "Updating " + packageId);
            }

            return false;
        }

        public async Task<bool> InstallOrUpdateAsync(string packageId, bool firstRun = false, bool force = false)
        {
            // We only refresh if list is empty.
            if (Installed.Count == 0)
                if (!Refresh())
                    return false;

            var tool = Installed.FirstOrDefault(x => x.PackageId == packageId);
            if (tool == null)
            {
                if (!Install(packageId))
                    return false;
            }
            else if (await FindUpdateAsync(packageId, tool.Version) != null)
            {
#if DEBUG
                // Don't apply first immediate update when running with the 
                // debugger attached. Useful for locally testing the update 
                // scenario, by installing an older version of a tool and running 
                // with the debugger attached. Will run the tool, check for updates, 
                // stop it and restart it.
                if (Debugger.IsAttached && firstRun)
                    return true;
#endif
                if (!Update(packageId, force))
                    return false;
            }

            return true;
        }

        public bool Refresh()
        {
            if (!TryExecute(dotnet, "tool list -g", out var output))
                return false;

            Installed = ParseTools(output);
            return true;
        }

        bool RunToolCommand(string command, string status)
        {
            string? output = null;
            string? error = null;

            var info = new ProcessStartInfo(dotnet, command)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            var exitCode = AnsiConsole.Status().Start(status, _ =>
                Process.Start(info).WaitForExitCode(out output, out error) == 0 && Refresh());

            if (!quiet && output != null)
                AnsiConsole.Render(new Paragraph(output, new Style(Color.Green)));

            if (!quiet && !string.IsNullOrEmpty(error))
                AnsiConsole.Render(new Paragraph(error, new Style(Color.Red)));

            return exitCode;
        }


        static List<Tool> ParseTools(string output) => output
            .Split(Environment.NewLine)
            .Skip(2)
            .Where(line => !string.IsNullOrEmpty(line))
            .Select(line =>
            {
                var packageId = new string(line.TakeWhile(c => c != ' ').ToArray());
                var version = new string(line
                    .SkipWhile(c => c != ' ')
                    .SkipWhile(c => c == ' ')
                    .TakeWhile(c => c != ' ')
                    .ToArray());

                var commands = new string(line
                    .SkipWhile(c => c != ' ')
                    .SkipWhile(c => c == ' ')
                    .SkipWhile(c => c != ' ')
                    .SkipWhile(c => c == ' ')
                    .TakeWhile(c => c != ' ')
                    .ToArray());

                return new Tool(packageId, new NuGetVersion(version), commands);
            })
            .ToList();
    }
}
