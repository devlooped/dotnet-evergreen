using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using static Devlooped.Extensions;

namespace Devlooped
{
    public record Tool(string PackageId, NuGetVersion Version, string Commands);

    public class Tools
    {
        public const string DefaultPackageFeed = "https://api.nuget.org/v3/index.json";

        public static bool TryCreate(string packageFeed, out Tools? tools)
        {
            tools = default;
            var dotnet = DotnetMuxer.Path?.FullName;
            if (dotnet == null)
                return false;

            tools = new Tools(dotnet, packageFeed);
            return true;
        }

        readonly string dotnet;
        readonly string packageFeed;
        readonly string feedArg = "";

        public Tools(string dotnet, string packageFeed = DefaultPackageFeed)
        {
            this.dotnet = dotnet;
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
            var metadata = await resource.GetMetadataAsync(packageId, false, false, new SourceCacheContext(), NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);

            var update = metadata
                .Select(x => x.Identity)
                .Where(x => x.Version > localVersion)
                .OrderByDescending(x => x.Version)
                .Select(x => x.Version)
                .FirstOrDefault();

            return update;
        }

        public bool Install(string packageId)
            => Process.Start(dotnet, "tool install -g --no-cache " + feedArg + packageId).WaitForExitCode() == 0 && Refresh();

        public bool Update(string packageId)
            => Process.Start(dotnet, "tool update -g --no-cache " + feedArg + packageId).WaitForExitCode() == 0 && Refresh();

        public async Task<bool> InstallOrUpdateAsync(string packageId)
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
                if (!Update(packageId))
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
