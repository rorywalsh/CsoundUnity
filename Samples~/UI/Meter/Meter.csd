<Cabbage>
form caption("Meter") size(420, 360), guiMode("queue"), pluginId("mtr1")
vmeter  bounds( 20,  20,  30, 300) channel("meter") range(0, 1, 0, 1, 0.001) text("Peak")
vmeter  bounds( 60,  20,  30, 300) channel("rms")   range(0, 1, 0, 1, 0.001) text("RMS")
hslider bounds(110,  40, 290,  40) channel("level") range(0, 1, 0.8, 1, 0.001) text("Level") valuetextbox(1)
hslider bounds(110, 100, 290,  40) channel("rate")  range(0.25, 8, 1.5, 1, 0.01) text("Rate") valuetextbox(1)
hslider bounds(110, 160, 290,  40) channel("pitch") range(36, 84, 57, 1, 1) text("Pitch") valuetextbox(1)
</Cabbage>

<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

ksmps = 32
nchnls = 2
0dbfs = 1

giSine ftgen 0, 0, 4096, 10, 1, 0.3, 0.15, 0.08

instr 1

; A meter reads a channel the orchestra writes. Nothing in Unity pushes these two: the levels are
; measured here and sent out with chnset, and CsoundUnityMeter picks them up.

kLevel chnget "level"
kRate  chnget "rate"
kPitch chnget "pitch"

; A pulse that rises fast and falls slowly. A steady tone would park both bars at one height and
; say nothing.
kEnv loopseg kRate, 0,  0, 0,  0.01, 1,  0.45, 0.25,  0.54, 0, 0

aSig oscili kEnv * kLevel, cpsmidinn:k(kPitch), giSine

; Peak: follows the signal up instantly and falls back on its own, which is what a peak meter
; does. The fall is written in seconds and converted with kr, so it looks the same whatever rate
; the host runs Csound at.
iFall  = 0.6
iCoef  = exp(log(0.01) / (iFall * kr))
kAbs   downsamp abs(aSig)
kPeak  init 0
kPeak  = (kAbs > kPeak) ? kAbs : kPeak * iCoef
       chnset kPeak, "meter"

; RMS is the average power, so it reads lower than the peak and moves more slowly. On a sine at
; full level it settles around 0.7, and on this decaying pulse it stays under that. The two bars
; side by side are the whole point of the scene.
kRms   rms aSig
       chnset kRms, "rms"

out aSig, aSig

endin

</CsInstruments>
<CsScore>
f0 z
i1 0 z
</CsScore>
</CsoundSynthesizer>
