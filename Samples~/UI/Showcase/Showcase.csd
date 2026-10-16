<Cabbage>
form caption("UI Showcase") size(1140, 460), guiMode("queue"), pluginId("show")

; The bounds are the prefabs' own sizes, and the gaps are the room their labels need: the hrange
; hangs its caption 410 px to its left and its two values 40 px below, the vrange spreads 90 px
; either side and puts its values 120 px to the right, and the combobox puts its own 19 px above.
; Place them tighter and they overlap, which is what the first version of this file did.

label    bounds(  20,  12, 400,  36) channel("title") text("Every widget CsoundUnity builds")

hslider  bounds(  20,  64, 300,  30) channel("rate")  range(0.5, 16, 4, 0.5, 0.1) text("Repeats") valuetextbox(1)
nslider  bounds(  20, 104, 300, 100) channel("pitch") range(24, 84, 45, 1, 1) text("Note")
; Run is the master switch, so it sits at the top at twice the size of anything else: a control
; that silences the whole sample should not be the one you have to hunt for.
checkbox bounds( 440,  14, 320,  60) channel("hold") text("Run") value(1)

button   bounds(  20, 214, 100, 100) channel("ping") text("Ping")
combobox bounds( 140, 244, 160,  30) channel("wave") text("Saw","Square","Triangle") value(1)

vrange   bounds( 560, 170,  20, 160) channel("spreadLo", "spreadHi") range(-24, 24, 0, 0, 1, 1) text("Spread")

rslider  bounds( 790,  30, 100, 100) channel("cutoff") range(80, 6000, 900, 0.5, 1) text("Cutoff")
encoder  bounds( 900,  30, 100, 100) channel("noise")  range(0, 1, 0.35, 1, 0.01) text("Noise")
xypad    bounds( 790, 160, 180, 180) channel("reso", "drive") rangex(0, 1, 0.5) rangey(0, 1, 0.2) text("Q / Drive")

vslider  bounds(1030, 160,  30, 180) channel("level") range(0, 1, 0.7, 1, 0.001) text("Level")
meter    bounds(1090, 160,  30, 180) channel("out")   range(0, 1, 0, 1, 0.001) text("Out")

hrange   bounds( 450, 370, 300,  30) channel("durMin", "durMax") range(0.03, 1.2, 0.08, 0.35, 1, 0.01) text("Length")
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

ksmps = 32
nchnls = 2
0dbfs = 1

giSine ftgen 0, 0, 4096, 10, 1
gaBus  init 0

; A repeated note, not a held one: every control is heard against the note before it, which is
; what makes the difference obvious. One widget, one job.
;
;   Repeats  how often it fires      Note     the base note
;   Length   duration, random between the two handles
;   Spread   random transposition, between the two handles, in semitones
;   Cutoff   centre of the bandpass  Noise    how much noise is in the source
;   Q/Drive  the pad: X narrows the band, Y drives it
;   Wave     the tone's waveform     Level    volume
;   Run      the sequence on or off  Ping     one extra note now
;   Out      the level the meter shows
;
; instr 1 schedules, instr 10 is the voice, instr 99 is the output stage. 99 runs last, so the
; voices it mixes are the ones from this very control period.

instr 1

kRate chnget "rate"
kHold chnget "hold"
kDMin chnget "durMin"
kDMax chnget "durMax"
kSLo  chnget "spreadLo"
kSHi  chnget "spreadHi"

kTrig metro kRate

; The button toggles its channel, so a change is a press: one note, whatever Run says.
kPing chnget "ping"
kHit  = (changed(kPing) == 1) ? 1 : 0

if kTrig == 1 || kHit == 1 then
    if kHold > 0.5 || kHit == 1 then
        kDur  random min(kDMin, kDMax), max(kDMin, kDMax)
        kOff  random min(kSLo, kSHi), max(kSLo, kSHi)
        schedulek 10, 0, kDur, kOff
    endif
endif

endin

instr 10

; Read once, at the note's start: a sequencer's note keeps the settings it was born with.
iOff    = p4
iPitch  chnget "pitch"
iWave   chnget "wave"
iNoise  chnget "noise"
iCutoff chnget "cutoff"
iReso   chnget "reso"
iDrive  chnget "drive"

iFreq = cpsmidinn(iPitch + iOff)
iType = iWave < 1.5 ? 0 : iWave < 2.5 ? 10 : 12

; A rich source, or the filter would have nothing to take away. The encoder mixes the tone with
; noise: at 0 you hear the waveform, at 1 only the hiss the bandpass is carving.
aTone  vco2 1, iFreq, iType
aHiss  noise 1, 0
aSrc   = aTone * (1 - iNoise) + aHiss * iNoise

; Percussive, so each repeat is a separate event rather than a smear.
aEnv   linseg 0, 0.004, 1, p3 - 0.014, 0.6, 0.01, 0

; Bandpass: the knob is its centre, the pad's X narrows it. A narrow band on noise whistles.
iQ     = 0.7 + iReso * 24
iBw    = iCutoff / iQ
aBand  butterbp aSrc * aEnv, iCutoff, iBw
aBand  = aBand * (1 + iQ * 0.35)

; The pad's Y drives it, from clean to clearly dirty.
aVoice = tanh(aBand * (1 + iDrive * 25))

gaBus = gaBus + aVoice * 0.25

endin

instr 99

kLevel chnget "level"
aMix   = gaBus
gaBus  = 0

aOut   = aMix * kLevel
kRms   rms aOut
       chnset kRms, "out"

out aOut, aOut

endin

</CsInstruments>
<CsScore>
f0 z
i1  0 z
i99 0 z
</CsScore>
</CsoundSynthesizer>
