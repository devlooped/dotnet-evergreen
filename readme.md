![Icon](https://raw.githubusercontent.com/devlooped/dotnet-evergreen/main/assets/img/icon.png) dotnet-evergreen
============

[![Version](https://img.shields.io/nuget/v/dotnet-evergreen.svg?color=royalblue)](https://www.nuget.org/packages/dotnet-evergreen) [![Downloads](https://img.shields.io/nuget/dt/dotnet-evergreen.svg?color=green)](https://www.nuget.org/packages/dotnet-evergreen) [![License](https://img.shields.io/github/license/devlooped/dotnet-evergreen.svg?color=blue)](https://github.com/devlooped/dotnet-evergreen/blob/main/license.txt) [![Build](https://github.com/devlooped/dotnet-evergreen/workflows/build/badge.svg?branch=main)](https://github.com/devlooped/dotnet-evergreen/actions)

A dotnet global tool runner that automatically updates the tool package before running it, 
checks for updates while it runs, and restarts the tool as needed after updating it.

```
dotnet evergreen
  Run an evergreen version of a tool

Usage:
  dotnet evergreen [options] [<tool> [<args>...]]

Arguments:
  <tool>  Package Id of tool to run.
  <args>  Additional arguments and options supported by the tool

Options:
  -s, --source <source>      NuGet feed to check for updates. [default: https://api.nuget.org/v3/index.json]
  --singleton                Ensure a single tool process is running.
  -i, --interval <interval>  Time interval in seconds for the update checks. [default: 5]
  -f, --force                Stop all running tool processes to apply updates. [default: True]
  -p, --prerelease           Whether to include pre-release packages. [default: False]
  -q, --quiet                Do not display any informational messages.
  -?, -h, --help             Show help and usage information
  --version                  Show version information

```

Features:

* Automatically exits if the tool also runs to completion
* Forwards exit code from the tool
* Restarts tool as needed to apply updates
* Stops all other tool running processes so updating succeeds (can be opted out with `-f=false`)
* Ensures a single tool is running (`--singleton` option)
* Passes all tool options verbatim (except for `evergreen` options, specified *before* the tool argument)
* Automatically discovers tool *command* when it doesn't match the tool *package id* 
  (i.e. [dotnet-eventgrid](https://www.nuget.org/packages/dotnet-eventgrid) > `eventgrid`).
* Supports [dotnetconfig](https://dotnetconfig.org) for options. Example:

  ```
  [evergreen]
    interval = 5
    source = https://myfeed.com/index.json 
    singleton = true
    prerelease = true
  ```


Examples:

```
> dotnet evergreen dotnet-echo
> dotnet evergreen dotnet-tor
```

<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->
# Sponsors 

<!-- sponsors.md -->
[![Clarius Org](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/clarius.png "Clarius Org")](https://github.com/clarius)
[![Christian Findlay](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/MelbourneDeveloper.png "Christian Findlay")](https://github.com/MelbourneDeveloper)
[![C. Augusto Proiete](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/augustoproiete.png "C. Augusto Proiete")](https://github.com/augustoproiete)
[![Kirill Osenkov](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/KirillOsenkov.png "Kirill Osenkov")](https://github.com/KirillOsenkov)
[![MFB Technologies, Inc.](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/MFB-Technologies-Inc.png "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![SandRock](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/sandrock.png "SandRock")](https://github.com/sandrock)
[![Eric C](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/eeseewy.png "Eric C")](https://github.com/eeseewy)
[![Andy Gocke](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/agocke.png "Andy Gocke")](https://github.com/agocke)


<!-- sponsors.md -->

[![Sponsor this project](https://raw.githubusercontent.com/devlooped/sponsors/main/sponsor.png "Sponsor this project")](https://github.com/sponsors/devlooped)
&nbsp;

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->
