<CsoundSynthesizer>
<CsOptions>
; Csound only asks the host for MIDI when a MIDI device is requested, so without -M the messages
; sent with SendMidiNoteOn and the rest are queued and never collected. rtmidi=null because the
; device is CsoundUnity, not a driver.
-+rtmidi=null -M0
</CsOptions>
<CsInstruments>

; The orchestra the API smoke test talks to. Nothing here is meant to be listened to for its own
; sake: every instrument exists so that a public method has something to prove.
;
;   instr 10   MIDI-triggered: reports the note and velocity it received on channels
;   instr 20   passthrough, for the spin and spout methods
;   instr 30   always on: reports that Csound is running, and the value of a MIDI controller

ksmps    = 32
nchnls   = 2
nchnls_i = 2
0dbfs    = 1

massign 0, 10

instr 10
  iNote notnum
  iVel  veloc
  chnset iNote, "midiNote"
  chnset iVel,  "midiVel"

  kEnv madsr 0.01, 0.1, 0.7, 0.2
  aOut poscil 0.2 * kEnv * (iVel / 127), cpsmidinn(iNote)
  outs aOut, aOut
endin

instr 20
  ; The passthrough is silent unless "monitor" is set to 1: this instrument exists so that spin
  ; and spout carry something, and what lands in spin is read back from C#, not listened to.
  kMon chnget "monitor"
  aIn  inch 1
  outs aIn * kMon, aIn * kMon
endin

instr 30
  chnset 0.75, "alive"

  ; ctrl7 reads a controller in any instrument, unlike midic7, which needs a MIDI-activated one.
  kCC ctrl7 1, 1, 0, 127
  chnset kCC, "cc1"
endin

</CsInstruments>
<CsScore>
f0 z
i 20 0 -1
i 30 0 -1
</CsScore>
</CsoundSynthesizer>
