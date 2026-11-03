# Security policy

## Supported versions

Only the latest release is supported. Please reproduce on the newest version from the
[releases page](https://github.com/v0ll/SoundScene/releases/latest) before reporting.

## Reporting a vulnerability

Please **do not open a public issue** for a security problem.

Use GitHub's private vulnerability reporting: open the repository's **Security** tab and choose
**Report a vulnerability**. If that is unavailable, contact the maintainer through the GitHub
profile linked on the repository.

Include:

- SoundScene version and whether it is the installer or portable build
- Windows version and laptop model
- Steps to reproduce and the impact
- The Diagnostics window output, if it is not sensitive

## Scope

SoundScene runs with the current user's privileges. It reads the Dolby Access package, copies its
client DLL into a private cache, writes HKCU/app-data settings, and talks to the local Dolby DAX
service. It does not bundle Dolby binaries, request elevation, open a network listener, or make any
network request.

The [security design](docs/security.md) page lists every file and registry value it touches, and
[verify-download.md](docs/verify-download.md) explains how to check a release.

Problems in Dolby Access, the ASUS audio driver, or Windows itself are outside this project's
scope and should be reported to the respective vendor.
