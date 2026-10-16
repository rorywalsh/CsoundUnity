## Supported Platforms ##

CsoundUnity v4.0.0 ships **Csound 7** across all supported platforms: native libraries on desktop/mobile and a WASM bundle (`@csound/browser`) on WebGL.

| Platform | Csound | NativeAudioInput | MIDI |
|---|---|---|---|
| **macOS** | Csound 7 | CoreAudio / AUHAL | CoreMIDI |
| **Windows** | Csound 7 | WASAPI | WinMM |
| **iOS** | Csound 7 | AudioUnit RemoteIO | CoreMIDI |
| **visionOS** | Csound 7 | — (not shipped yet) | CoreMIDI |
| **Android** | Csound 7 | AAudio (API ≥ 26) / Microphone fallback | android.media.midi |
| **WebGL** | Csound 7 (beta) | `getUserMedia` (stereo max, HTTPS) | Web MIDI API (Chrome/Edge, HTTPS) |
| **Linux** | Not available | Not available | Not available |

> **visionOS is untested.** Csound itself is built for it and MIDI goes through CoreMIDI, but the
> low-latency input plugin has never been compiled for visionOS and nothing here has been run on a
> device. If you have an Apple developer account and a headset and would like to try it, open an
> issue: we would rather ship it tested than claim it.

---

### Minimum requirements ###

| Platform | Minimum |
|---|---|
| Unity | 2022.3 LTS (Unity 6 required for IAudioGenerator and RootOutput) |
| macOS | 10.14 Mojave |
| iOS / visionOS | iOS 13 |
| Android | API 21 (AAudio low-latency path requires API 26) |
| Windows | Windows 10 |

---

### WebGL ###

WebGL uses **Csound 7** via the `@csound/browser` WASM bundle, embedded in `csound.jspre` inside the package — no CDN dependency at runtime. [WebGL support](webgl_support.md) states the exact build and covers setup and limitations.

---

### Android ###

For minimum latency, set **Project Settings → Audio → System Sample Rate** to `48000`. On supported hardware the AAudio exclusive-mode path reports ≈4 ms of **input** latency at that rate; devices on API < 26 fall back to the Unity Microphone API, where the code assumes ≈50 ms. Neither figure is measured, and neither includes output — see [Native Audio Input](native_audio_input.md).

See [Native Audio Input](native_audio_input.md) for the full setup guide.

---

### iOS — Microphone permission ###

NativeAudioInput requires microphone access. Add `NSMicrophoneUsageDescription` to your `Info.plist` before submitting to the App Store. CsoundUnity requests the permission automatically on first use.

---

### Windows — WASAPI ###

Exclusive mode reaches the lowest latency but prevents other apps from using the device at the same time. Enable **Exclusive Mode** in the NativeAudioInputManager inspector. The plugin falls back to shared mode automatically if exclusive is unavailable.

---

### IAudioGenerator (Unity 6+) ###

On Unity 6 and above, two further audio paths can be selected per component: **IAudioGenerator**, whose latency is one block and perfectly steady, and **RootOutput**, which needs no AudioSource. The default stays `OnAudioFilterRead`. See [Audio Output Path](audio_output_path.md).
