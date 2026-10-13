<Cabbage>
form caption("FM Input Modulator"), size(300, 223)

hslider bounds(0,   0, 300, 32) range(20, 16000, 440, .5, 0.001) valuetextbox(1) channel("carrierFreq") value(440) velocity(50)
hslider bounds(0,  32, 300, 32) range(0,  500,   100,  1, 0.001) valuetextbox(1) channel("deviation")  value(100) velocity(50)
hslider bounds(0,  64, 300, 32) range(0,    1,   0.6,  1, 0.001) valuetextbox(1) channel("amp")        value(0.6) velocity(50)
hslider bounds(0,  96, 300, 32) range(0,    1,   1.0,  1, 0.001) valuetextbox(1) channel("output")     value(1.0) velocity(50)
hslider bounds(0, 128, 300, 32) range(0,   10,   0.0,  1, 0.001) valuetextbox(1) channel("pulseRate")  value(0.0) velocity(50)
hslider bounds(0, 160, 300, 32) range(0,    1,  0.75,  1, 0.001) valuetextbox(1) channel("pulseDuty")  value(0.75) velocity(50)
hslider bounds(0, 192, 300, 32) range(0,    2,  0.25,  1, 0.001) valuetextbox(1) channel("pulseRelease") value(0.25) velocity(50)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

; FM synthesis driven by stereo audio routed from another CsoundUnity instance.
;
; CsoundUnity setup:
;   - Set Audio Input Routes: source = StereoOscChannels,
;     "audioL" -> destSpinChannel 0,  "audioR" -> destSpinChannel 1.
;   - nchnls_i = 2 for stereo input.
;   - "Process Clip Audio" is NOT needed for audio input routes: routes are written
;     straight into spin. That setting only concerns the AudioSource clip audio.
;
; This instance is a mid-chain node, so it writes its result both to named audio
; channels ("fmL" / "fmR", for routing onwards) and to its own "outs" (for
; monitoring). The two are independent: set "output" to 0 to send this instance's
; audio down the chain while keeping its own AudioSource silent. Same pattern as
; StereoOscillators. Routing from "fmL"/"fmR" is preferable to the auto-generated
; spout channels ("main_out_0"/"main_out_1"), which are taken after "outs" and so
; cannot be attenuated independently of the send.
;
; The modulating signal (its frequency and timbre) comes entirely from the
; source instance.  "deviation" here controls the maximum carrier frequency
; swing in Hz when the input signal is at full amplitude (0dbfs = 1):
;
;   instantaneous deviation = aInL * deviation
;
; So "deviation 100" means the carrier can swing ±100 Hz around carrierFreq
; when the source oscillator is at peak amplitude.
;
; Exposed channels:
;   "carrierFreq"  - carrier base frequency in Hz  (default 440)
;   "deviation"    - max frequency deviation in Hz  (default 100)
;   "amp"          - output amplitude 0..1          (default 0.6)
;   "output"       - own AudioSource output gate 0..1, does not affect
;                    "fmL"/"fmR"                    (default 1)
;   "pulseRate"    - notes per second, 0 = held tone (default 0)
;   "pulseDuty"    - fraction of the period the note sounds (default 0.75)

sr       = 48000
ksmps    = 32
nchnls   = 2
nchnls_i = 2
0dbfs    = 1

instr 1

  ; ---- parameters -------------------------------------------------------
  kCarrierFreq chnget "carrierFreq"
  kDeviation   chnget "deviation"
  kAmp         chnget "amp"
  kOutput      chnget "output"

  ; Guard only frequency against zero/negative (poscil undefined there).
  ; deviation = 0 is valid (pure carrier, no FM).
  ; amp       = 0 is valid (silence).
  kCarrierFreq = (kCarrierFreq < 1 ? 440 : kCarrierFreq)

  ; Smooth parameter changes
  kCarrierFreq portk kCarrierFreq, 0.02
  kDeviation   portk kDeviation,   0.02
  kAmp         portk kAmp,         0.02

  ; ---- startup fade-in for the FM deviation ------------------------------
  ; Ramps modulation depth from 0 to 1 over 1.5 s so that even if the
  ; source instance becomes ready slightly after this one, the carrier
  ; frequency never jumps abruptly when audio starts arriving in the spin buffer.
  aDevFade linseg 0, 1.5, 1

  ; ---- read stereo input from spin buffer (audio input routes) ----------
  aInL inch 1
  aInR inch 2

  ; ---- FM: input signal drives the frequency deviation of the carrier ---
  aDevL = aInL * kDeviation * aDevFade
  aDevR = aInR * kDeviation * aDevFade

  aOutL poscil kAmp, kCarrierFreq + aDevL
  aOutR poscil kAmp, kCarrierFreq + aDevR

  ; ---- optional pulsing: repeated notes instead of one held tone ---------
  ; A held tone makes the interesting questions ambiguous: silence sounds like
  ; silence, a looped block sounds like the same tone, and two instances playing
  ; the same drone are impossible to tell apart. Repeating notes turn the rhythm
  ; into an audible clock — you can hear whether a pattern stayed in phase across
  ; a silence, and a repeated block reads as a stutter rather than a nuance.
  ;
  ; The shape is a flat-topped note, not a percussive pluck: holding the note is
  ; what lets you judge its timbre, and the gap after it is what makes "did it
  ; stop?" unambiguous. Fades are a few milliseconds — long enough not to click
  ; (clicks are exactly what these tests hunt for), short enough to stay crisp.
  ;
  ;   pulseRate = notes per second, 0 keeps the original held tone
  ;               (0.5 = one note every two seconds)
  ;   pulseDuty = fraction of that period the note sounds
  ;               (0.75 of a 2 s period = a 1.5 s note, then 0.5 s of silence)
  kPulseRate chnget "pulseRate"
  kPulseDuty chnget "pulseDuty"
  kPRate   = (kPulseRate > 0 ? kPulseRate : 1)
  kPulsePh phasor kPRate
  ; 10 ms of fade as a fraction of the period, never more than a third of the note
  kPulseRelease chnget "pulseRelease"
  ; Envelope: fast attack, long eased release. A linear ramp has a corner in its
  ; slope right where it reaches zero, and the ear reads that corner as an abrupt
  ; stop however short it is — so the release is shaped with a smoothstep, whose
  ; slope is zero at both ends, and given far more time than the attack.
  kPAtk  = max(0.05, 0.01 * kPRate)
  kPRel  = max(0.05, min(kPulseRelease * kPRate, kPulseDuty * 0.9))
  kPUp   = min(1, kPulsePh / kPAtk)
  kPDn   = min(1, max(0, (kPulseDuty - kPulsePh) / kPRel))
  kPDn   = kPDn * kPDn * (3 - 2 * kPDn)
  kPGate    = (kPulsePh >= kPulseDuty ? 0 : min(kPUp, kPDn))
  kPulseEnv = (kPulseRate <= 0 ? 1 : kPGate)

  aOutL = aOutL * kPulseEnv
  aOutR = aOutR * kPulseEnv

  ; ---- write to named audio channels for routing onwards ----------------
  ; Independent of "outs" below, so a destination can be fed at full level
  ; while this instance's own output is gated down by "output".
  chnset aOutL, "fmL"
  chnset aOutR, "fmR"

  outs aOutL * kOutput, aOutR * kOutput

endin

</CsInstruments>
<CsScore>
; Run indefinitely
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
