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

<!-- include docs/footer.md -->
# Sponsors 

<!-- sponsors.md -->
<!-- sponsors -->

<a href='https://github.com/KirillOsenkov'>
  <img src='https://github.com/devlooped/sponsors/raw/main/.github/avatars/KirillOsenkov.svg' alt='Kirill Osenkov' title='Kirill Osenkov'>
</a>
<a href='https://github.com/augustoproiete'>
  <img src='https://github.com/devlooped/sponsors/raw/main/.github/avatars/augustoproiete.svg' alt='C. Augusto Proiete' title='C. Augusto Proiete'>
</a>
<a href='https://github.com/sandrock'>
  <img src='https://github.com/devlooped/sponsors/raw/main/.github/avatars/sandrock.svg' alt='SandRock' title='SandRock'>
</a>
<a href='https://github.com/aws'>
  <img src='https://github.com/devlooped/sponsors/raw/main/.github/avatars/aws.svg' alt='Amazon Web Services' title='Amazon Web Services'>
</a>
<a href='https://github.com/MelbourneDeveloper'>
  <img src='https://github.com/devlooped/sponsors/raw/main/.github/avatars/MelbourneDeveloper.svg' alt='Christian Findlay' title='Christian Findlay'>
</a>
<a href='https://github.com/clarius'>
  <img src='https://github.com/devlooped/sponsors/raw/main/.github/avatars/clarius.svg' alt='Clarius Org' title='Clarius Org'>
</a>
<a href='https://github.com/MFB-Technologies-Inc'>
  <img src='https://github.com/devlooped/sponsors/raw/main/.github/avatars/MFB-Technologies-Inc.svg' alt='MFB Technologies, Inc.' title='MFB Technologies, Inc.'>
</a>

<!-- sponsors -->

<!-- sponsors.md -->

<br>&nbsp;
<a href="https://github.com/sponsors/devlooped" title="Sponsor this project">
  <img src="https://github.com/devlooped/sponsors/blob/main/sponsor.png" />
</a>
<br>

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- docs/footer.md -->
