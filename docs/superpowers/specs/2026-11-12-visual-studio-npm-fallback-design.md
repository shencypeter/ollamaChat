# Visual Studio npm Fallback Design

## Intent

Visual Studio Build/F5 must build the Vue application on a machine where npm is supplied by Visual Studio but its NodeJs directory is not on `PATH`. This prevents Windows error 9009 after the frontend build was integrated into MSBuild.

## Approaches

- Selected: resolve Visual Studio's bundled `npm.cmd` through `$(MSBuildExtensionsPath)\Microsoft\VisualStudio\NodeJs\npm.cmd` when it exists, then fall back to `npm` on `PATH`. This is portable across Visual Studio installation roots and still supports CLI/CI installations.
- Rejected: install Node/npm system-wide. That fixes one workstation but does not make the repository's Visual Studio pipeline predictable for colleagues.
- Rejected: hard-code the current Visual Studio 18 Community path. That breaks other editions and versions.

## Behavior and Constraints

- `NpmExecutable` remains overridable by MSBuild callers.
- Visual Studio's bundled npm is preferred only when the derived file exists.
- CLI/CI builds continue to use `npm` from `PATH` when no Visual Studio bundle is available.
- Existing incremental `npm ci`, always-run Vite build, skip flag, publish reuse, and failure propagation remain unchanged.
- The behavior test must reproduce a PATH without npm and prove the explicit Visual Studio fallback executes.
