# Audio routing — Phase 0C gate

The Phase 0C implementation is active. A real output picker is available under `settings → Ses` and targets the selected media application's persisted Windows output route. It never changes the machine-wide default endpoint.

Implemented:

- active render-endpoint enumeration through NAudio.Wasapi 3.0.1 over Windows Core Audio;
- matching the GSMTC source executable to live Core Audio session process IDs;
- read/set/reset of the persisted application endpoint for the multimedia and console roles;
- validation that a requested endpoint is still active before writing;
- read-back verification after every write;
- `Varsayılan` represented by a null persisted endpoint, returning the application to the Windows system default;
- a read-only `Mediance.AudioProbe` plus explicit `set` and `default` commands;
- a live WinUI picker below the widget's media controls, plus the same picker in settings, with busy/error state and refresh;
- a persisted Elements setting that collapses the main-widget picker without leaving layout space.
- five-percent mouse-wheel changes through each matched session's `SimpleAudioVolume`, with a short percentage HUD;
- no use of the endpoint/master-volume API; changing Spotify volume cannot change the machine-wide volume.

Undocumented audio policy interop is isolated in `Mediance.Windows.Audio.AudioPolicyConfigAdapter`. Its Windows 11 21H2+ factory IID and method order were checked against the current EarTrumpet source. The adapter uses explicit HSTRING ownership and raw IInspectable vtable calls because modern .NET does not support the legacy automatic IInspectable marshalling used by EarTrumpet's .NET Framework host. Mediance targets Windows 11 24H2+, so the downlevel factory is intentionally excluded. Attribution and licenses are recorded in `THIRD_PARTY_NOTICES.md`.

Browser routing applies to the application/process family, not an individual tab. Read back the persisted route and verify actual audible output; neither alone proves the entire behavior. Routing failures must not stop playback or update the picker as if the operation succeeded.

Verified on this PC, Windows build 26200:

- `Hoparlör (High Definition Audio Device)` and `Kulaklıklar (4- Fuxi-H7)` were enumerated with their real endpoint IDs;
- Spotify's live Core Audio session mapped to PID 16524;
- the policy read correctly returned Spotify's existing persisted speaker endpoint;
- writing that same speaker endpoint and reading it back succeeded, proving the set/get ABI without moving the user's audible output;
- the native UI and Audio tab smoke test exited successfully.
- the user confirmed audible Spotify routing to the headphones and back to `Varsayılan`;
- the user confirmed the second application's output remained unchanged during Spotify routing.

The executable-backed Phase 0C exit gate is accepted. Packaged-app/AUMID matching remains future work; current matching is deliberately limited to executable-backed sources such as Spotify and desktop browsers.

The application-volume path compiles against NAudio.Wasapi 3.0.1 and its UI/HUD lifecycle passes native smoke. A physical-wheel audible check is still required. Browsers remain process-family scoped, so the wheel affects matching browser audio sessions rather than one tab.
