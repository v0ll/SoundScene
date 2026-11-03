# Compatibility

SoundScene reads the installed driver rather than assuming one model: the profile list, EQ band
count, EQ range, and band frequencies all come from the machine. This table records what has
actually been run on hardware.

| Model | OS | Dolby Access | Bands | Profiles | Result |
| --- | --- | --- | --- | --- | --- |
| ROG Zephyrus G14 GA403UV | Windows 11 (26100) | 3.27 | 20 | Movie, Music, Game, Voice, Personalize, Dynamic | Working |
| ROG Zephyrus G16 | — | — | — | — | Not tested yet |
| Other DAX3 ROG models | — | — | — | — | Not tested yet |

"Supported in code" is not the same as "tested". Windows 10 2004+ is supported by the build and
installer but has not been run on hardware yet.

## Adding a row

1. Open **Diagnostics** from the tray menu.
2. Copy the output. It lists the discovered package, service version, profile catalog, band
   count and range, and the tuning XML source.
3. Open an issue with the laptop model, Windows build, and that text.

A report that says "works on my G16" is as useful as a bug report, and it is what makes the next
person comfortable installing.
