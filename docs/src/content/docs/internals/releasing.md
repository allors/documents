---
title: "Releasing"
description: "Build, test and pack with Task, how the version is stamped, and how a tag publishes the packages."
audience: "Maintainers"
kind: "How-to guide"
status: "Current"
---

> **Status: Current.** How-to guide for maintainers.

## Build

Prerequisites: [.NET SDK 10.0](https://dotnet.microsoft.com/download) and [Task](https://taskfile.dev).

- `task` or `task ci` — build, test and pack
- `task test` — build and run tests
- `task pack` — create the NuGet packages in `artifacts/nuget`
- `task clean` — clean build outputs

Versioning is handled by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (`version.json`).

## Release

Pushing a tag `vX.Y.Z` or `vX.Y.Z-rc.N` runs `release.yml`: it builds, tests and packs, publishes both packages to nuget.org through [trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) from the `nuget-release` environment, and creates a GitHub release. The package version comes from `version.json`, so set it to the tag's version before tagging.
