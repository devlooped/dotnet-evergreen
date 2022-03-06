using System;
using System.Diagnostics;
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

        /// <summary>
        /// Runs "dotnet stop {process.Id} -t {timeout} -q"
        /// </summary>
        public static void Stop(this Process? process, int timeout)
        {
            if (process != null &&
                !process.HasExited &&
                !Process.Start(new ProcessStartInfo(DotnetMuxer.Path!.FullName, $"stop {process.Id} -t {timeout} -q")
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    // Avoid the output of the dotnet-stop tool from polluting ours, since we'll kill the 
                    // process if it doesn't exit cleanly anyway and we're getting output from it already.
                    UseShellExecute = true,
                })?.WaitForExit(timeout) != true &&
                !process.HasExited)
            {
                process.Kill();
            }
        }

        public static int WaitForExitCode(this Process? process, out string? output, out string? error)
        {
            output = null;
            error = null;

            if (process == null)
                return 0;

            process.WaitForExit();

            if (process.StartInfo.RedirectStandardOutput)
                output = process.StandardOutput.ReadToEnd();

            if (process.StartInfo.RedirectStandardError)
                error = process.StandardError.ReadToEnd();

            return process.ExitCode;
        }

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
