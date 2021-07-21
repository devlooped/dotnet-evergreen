using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace Devlooped
{
    public static class Extensions
    {
        public static int Error(string message, int exitCode = -1)
        {
            AnsiConsole.Render(new Paragraph(message, new Style(Color.Red)));
            return exitCode;
        }

        public static int Exit(string? message = default, int exitCode = -1)
        {
            if (message != null)
                Error(message);

            Environment.Exit(exitCode);
            return exitCode;
        }

        public static int WaitForExitCode(this Process? process)
        {
            process?.WaitForExit();
            return process?.ExitCode ?? 0;
        }

        public static async Task<int> WaitForExitCodeAsync(this Process process, CancellationToken cancellation)
        {
            await process.WaitForExitAsync(cancellation);
            return process.ExitCode;
        }

#if !NET5_0_OR_GREATER
        // Pollyfill for Process.WaitForExitAsync
        /// <summary>
        /// Instructs the process component to wait for the associated process to exit, or for the cancellationToken to be cancelled.
        /// </summary>
        /// <param name="process">The process to wait for cancellation.</param>
        /// <param name="cancellationToken">An optional token to cancel the asynchronous operation.</param>
        /// <returns>A task that will complete when the process has exited, cancellation has been requested, or an error occurs.</returns>
        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<object?>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);
            if (cancellationToken != default)
                cancellationToken.Register(tcs.SetCanceled);

            return tcs.Task;
        }
#endif

        public static bool TryExecute(string program, string arguments, out string output)
        {
            var info = new ProcessStartInfo(program, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                var proc = Process.Start(info);
                if (proc == null)
                {
                    output = "";
                    return false;
                }

                var gotError = false;
                proc.ErrorDataReceived += (_, __) => gotError = true;

                output = proc.StandardOutput.ReadToEnd();
                if (!proc.WaitForExit(5000))
                {
                    proc.Kill();
                    return false;
                }

                return !gotError && proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return false;
            }
        }
    }
}
