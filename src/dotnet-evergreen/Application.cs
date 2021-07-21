using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Spectre.Console;

namespace Devlooped
{
    class Application
    {
        readonly CancellationTokenSource shutdownSource = new CancellationTokenSource();
        readonly bool quiet;
        bool stoppingChildProcess = false;

        public CancellationToken ShutdownToken => shutdownSource.Token;

        public Application(bool quiet = false)
        {
            this.quiet = quiet;
            Console.CancelKeyPress += (s, e) => e.Cancel = OnCancelKeyPress();
        }

        public Process Start(ProcessStartInfo start, CancellationTokenSource cancellation)
        {
            var process = Process.Start(start);
            if (!quiet)
                AnsiConsole.MarkupLine($"[grey]{Path.GetFileNameWithoutExtension(start.FileName)}:{process!.Id} Started[/]");

            process!.EnableRaisingEvents = true;
            var cancelled = false;

            // Register to stop process when cancellation is requested
            cancellation.Token.Register(() =>
            {
                // Flag that the process is exiting due to a tool cancellation
                // for an updating, so we don't cause the updater to exist below.
                cancelled = true;
                stoppingChildProcess = true;
                try
                {
                    if (!process.HasExited &&
                        Process.Start(new ProcessStartInfo(DotnetMuxer.Path!.FullName, $"stop {process.Id} -t 2000 -q")
                        {
                            // Avoid the output of the dotnet-stop tool from polluting ours, since we'll kill the 
                            // process if it doesn't exit cleanly anyway and we're getting output from it already.
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            UseShellExecute = true,
                        }).WaitForExitCode() != 0 &&
                        !process.HasExited)
                        process.Kill();
                }
                finally
                {
                    stoppingChildProcess = false;
                }
            });

            // Also cancel token when the app itself is cancelling too.
            ShutdownToken.Register(() => cancellation.Cancel());

            process.Exited += (s, e) =>
            {
                if (!quiet)
                {
                    if (!cancelled)
                    {
                        if (process.ExitCode == 0)
                            AnsiConsole.MarkupLine($"[grey]{Path.GetFileNameWithoutExtension(start.FileName)} Exited[/]");
                        else
                            AnsiConsole.MarkupLine($"[red]{Path.GetFileNameWithoutExtension(start.FileName)} Exited[/]");
                    }
                    else
                    {
                        if (ShutdownToken.IsCancellationRequested)
                            AnsiConsole.MarkupLine($"[grey]{Path.GetFileNameWithoutExtension(start.FileName)} Shutdown[/]");
                        else
                            AnsiConsole.MarkupLine($"[grey]{Path.GetFileNameWithoutExtension(start.FileName)} Restarting[/]");
                    }
                }

                // If the tool exits with an error, exit the application too,
                // unless it's a programmatic cancellation
                if (process.ExitCode != 0 && !cancelled)
                {
                    Environment.ExitCode = process.ExitCode;
                    shutdownSource.Cancel();
                }
            };

            return process;
        }

        bool OnCancelKeyPress()
        {
            // NOTE: we get the Ctrl+C/CancelKeyPress also when stopping the child process 
            // (at least on Windows), since it's part of the same process group, so we account 
            // for that here, only cancelling the app token when we're not stopping the child 
            // process ourselves (i.e. a Ctrl+C is actually entered manually by the user).
            if (!stoppingChildProcess)
            {
                if (!quiet)
                    AnsiConsole.MarkupLine("[yellow]Shutting down...[/]");

                shutdownSource.Cancel();
            }

            // We always cancel the Ctrl+C (CancelKeyPress) on the main app
            // since we use the ShutdownToken to perform clean exit from Main.
            return true;
        }
    }
}
