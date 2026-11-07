# Array channels

Open **ArrayChannels.unity** and enter Play mode. This sample requires the native Csound bridge. It uses
ordinary IMGUI and needs no Canvas or Timeline. Its script compiles with the other Basic samples.

- Move the four note sliders: Unity writes their frequencies to `hostK` and Csound echoes them through
  `echoK`. Enable **Listen to k[] chord** to hear the values change continuously.
- **Capture and play i[] chord** writes `hostI`, then starts an instrument that reads it at init and
  echoes its actual capture through `echoI`. Moving the sliders afterwards does not change that note.
- The host writes a **2 × 3 matrix**; Csound doubles each cell and sends a separate matrix back. The
  panel reads data and shape atomically. **Add 1** changes all six source values.
- Select **2** or **7 elements**, or enable auto resizing. The orchestra changes the producer's shape
  through `chnset`; the host reuses a seven-element buffer and shows only its valid prefix.
- **Read into a 1-element buffer** should return `BufferTooSmall`, report the current required length,
  and leave the sentinel at -99. The audio and producer continue unchanged.
- **Pause processing** freezes the echoes; **Restart Csound** rebuilds the channels and reruns setup.

Echoes follow the next Csound control period, not the frame in which Unity wrote the data. Array
channel declarations happen during setup; the runtime resize example never redeclares `chnarray`.
The panel formats text periodically and IMGUI allocates: use an isolated API probe or profiler markers
to measure transfer allocations, rather than treating the whole sample as an allocation-free UI.

For repeatable pass/fail checks, import **Diagnostics** and run **API Smoke Test**. That sample checks
the numeric and string API contracts; this scene demonstrates values, timing and audible behavior. WebGL displays
`UnsupportedPlatform` and leaves this native-only CSD uninitialized.

## String arrays

The fourth panel edits a flat `string[]` representing a 2 × 2 `S[][]` channel. Csound reads `hostS`
and echoes it to `echoS` each control period. Enter Unicode text, send null/empty elements (both read
back empty), or send a repeated accented phrase longer than the initial native capacity. Text fields
scroll internally rather than widening the panel. The echo preview is truncated
for readability; the transfer contains the complete text. Shape and data come from one snapshot.

String APIs reuse scratch buffers and unchanged destination strings. New text can allocate; use
these APIs from host threads, not audio callbacks. The Diagnostics smoke test also verifies string
producer resizing and capacity/type failures.
