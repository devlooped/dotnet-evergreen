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
