<Cabbage>
{
  "//": "Cabbage 3 writes its widgets as JSON. JSON has no comments, so this is a property.",
  "pluginId": "cu30",
  "channelConfig": [ { "name": "stereo", "ins": 0, "outs": 2 } ],
  "widgets": [
    {
      "type": "form",
      "caption": "Cabbage 3",
      "size": { "width": 460, "height": 320 }
    },
    {
      "type": "horizontalSlider",
      "bounds": { "left": 10, "top": 10, "width": 280, "height": 30 },
      "label": { "text": "Gain" },
      "channels": [
        { "id": "gain", "range": { "min": 0, "max": 1, "defaultValue": 0.25, "skew": 1, "increment": 0.001 } }
      ],
      "style": { "track": { "fillColor": "#93d200" } }
    },
    {
      "type": "rotarySlider",
      "bounds": { "left": 300, "top": 10, "width": 90, "height": 90 },
      "label": { "text": "Pitch" },
      "channels": [
        { "id": "freq", "range": { "min": 55, "max": 880, "defaultValue": 110, "skew": 0.5, "increment": 1 } }
      ]
    },
    {
      "//": "Two cutoffs, two widgets. Cabbage 3 documents no two-handle slider, and several channels on one widget are several channels, not several handles.",
      "type": "horizontalSlider",
      "bounds": { "left": 10, "top": 50, "width": 280, "height": 30 },
      "label": { "text": "Low cut" },
      "channels": [
        { "id": "lowCut", "range": { "min": 20, "max": 18000, "defaultValue": 120, "skew": 0.5, "increment": 1 } }
      ]
    },
    {
      "type": "horizontalSlider",
      "bounds": { "left": 10, "top": 90, "width": 280, "height": 30 },
      "label": { "text": "High cut" },
      "channels": [
        { "id": "highCut", "range": { "min": 20, "max": 18000, "defaultValue": 4000, "skew": 0.5, "increment": 1 } }
      ]
    },
    {
      "//": "The first item sends 0, and that is the number the orchestra below reads.",
      "type": "comboBox",
      "bounds": { "left": 10, "top": 130, "width": 140, "height": 30 },
      "channels": [
        { "id": "waveform", "range": { "defaultValue": 1 } }
      ],
      "items": [ "Sine", "Saw", "Square" ]
    },
    {
      "type": "checkBox",
      "bounds": { "left": 160, "top": 130, "width": 130, "height": 30 },
      "label": { "text": "Filter" },
      "channels": [
        { "id": "filterOn", "range": { "defaultValue": 1 } }
      ]
    },
    {
      "//": "An xyPad whose channels are the two directions of one drag, the one case read as a pair.",
      "type": "xyPad",
      "bounds": { "left": 10, "top": 170, "width": 140, "height": 140 },
      "label": { "text": "Motion" },
      "channels": [
        { "id": "tremoloRate", "event": "mouseDragX", "range": { "min": 0, "max": 12,   "defaultValue": 3,     "skew": 1, "increment": 0.01 } },
        { "id": "detune",      "event": "mouseDragY", "range": { "min": 0, "max": 0.03, "defaultValue": 0.004, "skew": 1, "increment": 0.0001 } }
      ]
    }
  ]
}
</Cabbage>

<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
sr      = 48000
ksmps   = 32
nchnls  = 2
0dbfs   = 1

instr 1
  kwave    chnget "waveform"
  kfiltOn  chnget "filterOn"

  ; A control channel steps once per control period, and a step in a gain or a filter cutoff is
  ; heard as zipper noise while a slider moves. portk ramps each one; the third argument is the
  ; value to start from, read from the same channel at init, so nothing sweeps up from zero when
  ; the instrument starts. The waveform and the filter switch are left alone: they choose a state,
  ; and halfway between two states is not a state.
  iGain    chnget "gain"
  iFreq    chnget "freq"
  iLowCut  chnget "lowCut"
  iHighCut chnget "highCut"
  iRate    chnget "tremoloRate"
  iDetune  chnget "detune"

  kgain    portk chnget:k("gain"),        0.02, iGain
  kfreq    portk chnget:k("freq"),        0.01, iFreq
  klowCut  portk chnget:k("lowCut"),      0.02, iLowCut
  khighCut portk chnget:k("highCut"),     0.02, iHighCut
  krate    portk chnget:k("tremoloRate"), 0.05, iRate
  kdetune  portk chnget:k("detune"),      0.02, iDetune

  ; A Cabbage 3 combobox sends the index of the selected item starting at 0, and that is what
  ; arrives here: 0 is the first entry of items[]. A Cabbage 2 csd would count from 1.
  kfn = (kwave == 1 ? 2 : (kwave == 2 ? 3 : 1))

  aLeft  oscilikt 1, kfreq, kfn
  aRight oscilikt 1, kfreq * (1 + kdetune), kfn

  if kfiltOn > 0 then
    aLeft  butterhp aLeft,  klowCut
    aLeft  butterlp aLeft,  khighCut
    aRight butterhp aRight, klowCut
    aRight butterlp aRight, khighCut
  endif

  ktrem oscili 0.35, krate, 1
  kamp  = kgain * (0.65 + ktrem)

  aLeft  = aLeft  * kamp
  aRight = aRight * kamp

  out aLeft, aRight
endin

</CsInstruments>
<CsScore>
f0 3600
f1 0 16384 10 1
f2 0 16384 10 1 0.5 0.333 0.25 0.2 0.166 0.142 0.125
f3 0 16384 10 1 0 0.333 0 0.2 0 0.142 0 0.111
i1 0 z
</CsScore>
</CsoundSynthesizer>
