# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- `AssemblyVersion` now moves only with the major version (for example `1.0.0.0` for every 1.x release), so a minor or patch upgrade keeps the same assembly identity. `FileVersion` and `InformationalVersion` keep the exact version. Previously `AssemblyVersion` was the full package version.
- The public API is tracked in `PublicAPI.Shipped.txt`; it is unchanged from 1.0.0.
- Updated the build to project-template v0.61.2: analyzer settings, workflows and agent instructions.

## [1.0.0] - 2026-01-19

Initial release.

[Unreleased]: https://github.com/NCodeGroup/Mediator/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/NCodeGroup/Mediator/releases/tag/v1.0.0
