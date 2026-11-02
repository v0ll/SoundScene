# Third-party notice

SoundScene is an independent, unofficial project. It is **not affiliated with, endorsed by, or
supported by** Dolby Laboratories, Inc. or ASUSTeK Computer Inc.

"Dolby", "Dolby Atmos", "Dolby Access", and "ASUS ROG" are trademarks of their respective owners and
are used here only to describe interoperability.

This repository contains no Dolby code, binaries, or assets. The MIT license in
[`LICENSE.md`](LICENSE.md) applies solely to the original source code in this repository.

`native/idl/DAX.idl` is interface metadata generated from the locally installed Dolby component for
interoperability. It contains no Dolby implementation code and is not a redistribution of a Dolby
binary.

## .NET runtime

The self-contained release bundles the .NET runtime so users do not need to install it separately.
Its MIT license is included as `DOTNET-LICENSE.txt`, and its third-party notices as
`DOTNET-THIRD-PARTY-NOTICES.txt`. Those files cover the runtime, not SoundScene's own source, which
remains under [`LICENSE.md`](LICENSE.md).
