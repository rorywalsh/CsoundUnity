<Cabbage> bounds(0, 0, 0, 0)

form caption("Scanu2") size(300, 180), guiMode("queue") pluginId("def1")
rslider bounds(10, 14, 80, 80), channel("dur"), range(.2, 10, 2, 1, 0.01), text("Duration") 
rslider bounds(110, 14, 80, 80), channel("amp"), range(30, 90, 80, 1, 0.01), text("Amplitude[dB]")
rslider bounds(210, 14, 80, 80), channel("freq"), range(16.352, 2000, 220, 1, 0.01), text("Frequency")
button bounds(110, 110, 80, 40) channel("trigger") text("Trigger")

</Cabbage>
<CsoundSynthesizer>

<CsOptions>

</CsOptions>

<CsInstruments>

; https://csound.com/docs/manual/scanu2.html
; Author: John ffitch
; May 2021
; New in Csound version 6.16
; ported to CsoundUnity by gb, May 2023

sr = 48000
ksmps = 32
nchnls = 1

instr 1
  
  kDur = chnget:k("dur")
  kAmp = chnget:k("amp")
  kFreq = chnget:k("freq")
    
    kTrig chnget "trigger"
    if changed(kTrig) == 1 then
        event "i", "scan", 0, kDur, kAmp, kFreq
    endif
    
endin


instr scan

a0 init 0

irate = .01

; scanu init, irate, ifndisplace, ifnmass, ifnmatrix, ifncentr, ifndamp, kmass,
;       kmtrxstiff, kcentr, kdamp, ileft, iright, kpos, kdisplace, ain, idisp, id
scanu2 1, irate, 6, 2, 3, 4, 5, 2, 9, .01, .01, .1, .9, 0, 0, a0, 1, 2

;ar scans kamp, kfreq, ifntraj, id
a1 scans ampdb(p4), p5, 7, 2
out a1
endin

</CsInstruments>
<CsScore>
; Initial displacement condition
;f1 0 128 -7 0 64 1 64 0 ; ramp
f1 0 128 10 1 ; sine hammer
;f1 0 128 -7 0 28 0 2 1 2 0 96 0 ; a pluck that is 10 points wide on the surface

; Masses
f2 0 128 -7 1 128 1

; Spring matrices
f3 0 16384 -23 "string-128.matrxB"

; Centering force
f4 0 128 -7 1 128 1 ; uniform initial centering
;f4 0 128 -7 .001 128 1 ; ramped centering

; Damping
f5 0 128 -7 1 128 1 ; uniform damping
;f5 0 128 -7 .1 128 1 ; ramped damping

; Initial velocity - (displacement, vel, and acceleration
; Acceleration is from stiffness matrix pos effect - increases acceleration
;

f6 0 128 -7 .01 128 .01 ; uniform initial velocity

; Trajectories
f7 0 128 -5 .001 128 128

;i"scan" 2 12 86 7.00
;i"scan" 14 2 86 5.00
;i"scan" 16 2 86 6.00
;i"scan" 18 2 86 8.00
;i"scan" 20 2 98 10.00

i1 0 z
</CsScore>
</CsoundSynthesizer>