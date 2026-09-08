# Third-party notices

## Microsoft Windows App SDK

Mediance is built with Microsoft Windows App SDK and distributes the runtime files placed with the application by its NuGet packages for self-contained deployment. Those files remain governed by the Microsoft software license terms included with the corresponding packages.

Source: https://github.com/microsoft/WindowsAppSDK
Package: https://www.nuget.org/packages/Microsoft.WindowsAppSDK

## NAudio.Wasapi

Mediance uses the NAudio.Wasapi NuGet package for Windows Core Audio endpoint and session enumeration.
NAudio is Copyright © 2008–2026 Mark Heath and is distributed under the MIT License.

Source: https://github.com/naudio/NAudio
License: https://github.com/naudio/NAudio/blob/main/LICENSE

## EarTrumpet reference

The isolated `AudioPolicyConfigAdapter` ABI layout and Windows-version selection were verified against
EarTrumpet's `IAudioPolicyConfigFactory` implementation. EarTrumpet is Copyright © 2015 its contributors
and is distributed under its repository's MIT-derived license, including its listed excluded entities.

Source: https://github.com/File-New-Project/EarTrumpet
License: https://github.com/File-New-Project/EarTrumpet/blob/master/LICENSE

## Lyrics services

Mediance can request lyrics or catalogue metadata from external services when the user opens the lyrics panel. These services are not bundled libraries and remain subject to their own terms and availability. The current provider list and transmitted fields are documented in `docs/PRIVACY.md`.
