# ADR 0001 — Preserve the master-plan boundaries

Date: 2026-09-06. Status: accepted product direction; platform spikes still pending.

- Native C# / WinUI 3 enables the required Windows floating-window and Desktop Acrylic behavior. .NET 10 is installed locally for repeatable development. No web-based main UI.
- GSMTC is the universal media boundary. Spotify is one source; no OAuth or account requirement is introduced.
- A session pin overrides automatic choice until that session disappears. Without a pin, automatic choice prefers Spotify, then browser music/YouTube Music, then the Windows current session; playback status is the final fallback.
- Audio routing is an isolated, OS-sensitive integration with a mandatory feasibility gate before its final UI. It must never change the global output in place of the requested application route.
- LRCLIB is the first lyrics provider. The 2026-09-07 product decision permits isolated, identity-validated plain-text page adapters only after an LRCLIB miss; content is never persisted or logged.
- The development deliverable begins with a real media probe. Passing unit tests does not mark device, source, routing, Acrylic or multi-monitor acceptance tests complete.
