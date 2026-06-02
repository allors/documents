# Allors Documents

Embed template tags inside open office documents. 

# Status

[![Build Status](https://github.com/Allors/Documents/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Allors/Documents/actions/workflows/ci.yml)

# Build

Prerequisites: [.NET SDK 10.0](https://dotnet.microsoft.com/download) and [Task](https://taskfile.dev).

- `task` or `task ci` — build, test and pack
- `task test` — build and run tests
- `task pack` — create the NuGet packages in `artifacts/nuget`
- `task clean` — clean build outputs

Versioning is handled by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (`version.json`).
