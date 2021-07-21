using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.IO;

namespace Devlooped
{

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
}