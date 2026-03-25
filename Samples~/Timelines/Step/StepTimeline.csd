
<Cabbage>
form caption("TR-808 Pattern Timeline") size(300, 90), guiMode("queue"), pluginId("drml")
rslider bounds(10, 10, 80, 80) channel("masterLevel") range(0, 1, 1, 1, 0.001) text("Master")
rslider bounds(100, 10, 80, 80) channel("reverbMix") range(0, 1, 0.1, 1, 0.001) text("Reverb")
</Cabbage>

; PatternTimeline.csd
; Based on TR-808.csd by Iain McCurdy (2012)
; https://iainmccurdy.org
; License: CC BY-NC-SA 4.0  https://creativecommons.org/licenses/by-nc-sa/4.0/
;
; Adapted for CsoundUnity Timelines by gb (2026).
; The internal step sequencer (instrs 1-4) has been replaced by Unity Timeline
; Drum clips, which trigger individual drum instruments directly via score events:
;
;   i <instrN> <onset> 0.001 <velocity>
;
;   instrN   : 101=BD  102=SN  103=OHH  104=CHH  105=HTom  106=MTom  107=LTom
;              108=Cym  109=Rim  110=Clv  111=CB   112=Clap  113=Mar
;              114=HConga  115=MConga  116=LConga
;   velocity : 0.0–1.0  (p4)
;
; Per-instrument timbre can be tweaked at runtime via CsoundUnity channels:
;   level1..16   – amplitude multiplier  (default 1)
;   tune1..16    – pitch offset in octaves (default 0)
;   dur1..16     – decay time multiplier  (default 1)
;   pan1..16     – stereo pan 0–1        (default 0.5)
;   masterLevel  – global output level    (default 1)

<CsoundSynthesizer>

<CsOptions>
-n -d
</CsOptions>

<CsInstruments>

sr      =   48000
ksmps   =   32
nchnls  =   2
0dbfs   =   1

gisine        ftgen 0, 0, 1024,  10, 1
gicos         ftgen 0, 0, 65536,  9, 1, 1, 90
giTR808RimShot ftgen 0, 0, 1024,  10, 0.971,0.269,0.041,0.054,0.011,0.013,0.08,0.0065,0.005,0.004,0.003,0.003,0.002,0.002,0.002,0.002,0.002,0.001,0.001,0.001,0.001,0.001,0.002,0.001,0.001

; ── Global defaults for per-instrument gk vars ───────────────────────────────
; These ensure drum instruments get sensible values even before instr 1's
; first k-pass (gklevel=0 would make all hits silent).
gklevel1  init 1   ;  BD  level
gklevel2  init 1   ;  SN  level
gklevel3  init 1   ;  OHH level
gklevel4  init 1   ;  CHH level
gklevel5  init 1   ;  HTom level
gklevel6  init 1   ;  MTom level
gklevel7  init 1   ;  LTom level
gklevel8  init 1   ;  Cym  level
gklevel9  init 1   ;  Rim  level
gklevel10 init 1   ;  Clv  level
gklevel11 init 1   ;  CB   level
gklevel12 init 1   ;  Clap level
gklevel13 init 1   ;  Mar  level
gklevel14 init 1   ;  HCo  level
gklevel15 init 1   ;  MCo  level
gklevel16 init 1   ;  LCo  level
gktune1   init 0
gktune2   init 0
gktune3   init 0
gktune4   init 0
gktune5   init 0
gktune6   init 0
gktune7   init 0
gktune8   init 0
gktune9   init 0
gktune10  init 0
gktune11  init 0
gktune12  init 0
gktune13  init 0
gktune14  init 0
gktune15  init 0
gktune16  init 0
gkdur1    init 1
gkdur2    init 1
gkdur3    init 1
gkdur4    init 1
gkdur5    init 1
gkdur6    init 1
gkdur7    init 1
gkdur8    init 1
gkdur9    init 1
gkdur10   init 1
gkdur11   init 1
gkdur12   init 1
gkdur13   init 1
gkdur14   init 1
gkdur15   init 1
gkdur16   init 1
gkpan1    init 0.5
gkpan2    init 0.5
gkpan3    init 0.5
gkpan4    init 0.5
gkpan5    init 0.5
gkpan6    init 0.5
gkpan7    init 0.5
gkpan8    init 0.5
gkpan9    init 0.5
gkpan10   init 0.5
gkpan11   init 0.5
gkpan12   init 0.5
gkpan13   init 0.5
gkpan14   init 0.5
gkpan15   init 0.5
gkpan16   init 0.5
gklevel   init 1   ;  masterLevel
ga1       init 0   ;  reverb send bus (mono)

; ── Global channel reader ────────────────────────────────────────────────────
; Reads per-instrument and master-level channels with sensible defaults.
; All drum instruments reference these gk vars at i-time and k-rate.
instr 1
    gklevel1    chnget  "level1"
    gklevel2    chnget  "level2"
    gklevel3    chnget  "level3"
    gklevel4    chnget  "level4"
    gklevel5    chnget  "level5"
    gklevel6    chnget  "level6"
    gklevel7    chnget  "level7"
    gklevel8    chnget  "level8"
    gklevel9    chnget  "level9"
    gklevel10   chnget  "level10"
    gklevel11   chnget  "level11"
    gklevel12   chnget  "level12"
    gklevel13   chnget  "level13"
    gklevel14   chnget  "level14"
    gklevel15   chnget  "level15"
    gklevel16   chnget  "level16"
    gktune1     chnget  "tune1"
    gktune2     chnget  "tune2"
    gktune3     chnget  "tune3"
    gktune4     chnget  "tune4"
    gktune5     chnget  "tune5"
    gktune6     chnget  "tune6"
    gktune7     chnget  "tune7"
    gktune8     chnget  "tune8"
    gktune9     chnget  "tune9"
    gktune10    chnget  "tune10"
    gktune11    chnget  "tune11"
    gktune12    chnget  "tune12"
    gktune13    chnget  "tune13"
    gktune14    chnget  "tune14"
    gktune15    chnget  "tune15"
    gktune16    chnget  "tune16"
    gkdur1      chnget  "dur1"
    gkdur2      chnget  "dur2"
    gkdur3      chnget  "dur3"
    gkdur4      chnget  "dur4"
    gkdur5      chnget  "dur5"
    gkdur6      chnget  "dur6"
    gkdur7      chnget  "dur7"
    gkdur8      chnget  "dur8"
    gkdur9      chnget  "dur9"
    gkdur10     chnget  "dur10"
    gkdur11     chnget  "dur11"
    gkdur12     chnget  "dur12"
    gkdur13     chnget  "dur13"
    gkdur14     chnget  "dur14"
    gkdur15     chnget  "dur15"
    gkdur16     chnget  "dur16"
    gkpan1      chnget  "pan1"
    gkpan2      chnget  "pan2"
    gkpan3      chnget  "pan3"
    gkpan4      chnget  "pan4"
    gkpan5      chnget  "pan5"
    gkpan6      chnget  "pan6"
    gkpan7      chnget  "pan7"
    gkpan8      chnget  "pan8"
    gkpan9      chnget  "pan9"
    gkpan10     chnget  "pan10"
    gkpan11     chnget  "pan11"
    gkpan12     chnget  "pan12"
    gkpan13     chnget  "pan13"
    gkpan14     chnget  "pan14"
    gkpan15     chnget  "pan15"
    gkpan16     chnget  "pan16"
    gklevel     chnget  "masterLevel"
endin

; ── Default channel values ────────────────────────────────────────────────────
; Written once at score start so chnget returns sensible values immediately.
instr 2
    chnset  1.0, "level1"
    chnset  1.0, "level2"
    chnset  1.0, "level3"
    chnset  1.0, "level4"
    chnset  1.0, "level5"
    chnset  1.0, "level6"
    chnset  1.0, "level7"
    chnset  1.0, "level8"
    chnset  1.0, "level9"
    chnset  1.0, "level10"
    chnset  1.0, "level11"
    chnset  1.0, "level12"
    chnset  1.0, "level13"
    chnset  1.0, "level14"
    chnset  1.0, "level15"
    chnset  1.0, "level16"
    chnset  0.0, "tune1"
    chnset  0.0, "tune2"
    chnset  0.0, "tune3"
    chnset  0.0, "tune4"
    chnset  0.0, "tune5"
    chnset  0.0, "tune6"
    chnset  0.0, "tune7"
    chnset  0.0, "tune8"
    chnset  0.0, "tune9"
    chnset  0.0, "tune10"
    chnset  0.0, "tune11"
    chnset  0.0, "tune12"
    chnset  0.0, "tune13"
    chnset  0.0, "tune14"
    chnset  0.0, "tune15"
    chnset  0.0, "tune16"
    chnset  1.0, "dur1"
    chnset  1.0, "dur2"
    chnset  1.0, "dur3"
    chnset  1.0, "dur4"
    chnset  1.0, "dur5"
    chnset  1.0, "dur6"
    chnset  1.0, "dur7"
    chnset  1.0, "dur8"
    chnset  1.0, "dur9"
    chnset  1.0, "dur10"
    chnset  1.0, "dur11"
    chnset  1.0, "dur12"
    chnset  1.0, "dur13"
    chnset  1.0, "dur14"
    chnset  1.0, "dur15"
    chnset  1.0, "dur16"
    chnset  0.5, "pan1"
    chnset  0.5, "pan2"
    chnset  0.5, "pan3"
    chnset  0.5, "pan4"
    chnset  0.5, "pan5"
    chnset  0.5, "pan6"
    chnset  0.5, "pan7"
    chnset  0.5, "pan8"
    chnset  0.5, "pan9"
    chnset  0.5, "pan10"
    chnset  0.5, "pan11"
    chnset  0.5, "pan12"
    chnset  0.5, "pan13"
    chnset  0.5, "pan14"
    chnset  0.5, "pan15"
    chnset  0.5, "pan16"
    chnset  1.0, "masterLevel"
    turnoff
endin

; ════════════════════════════════════════════════════════════════════════════
; DRUM VOICES  (101–116)
; Triggered by Unity Timeline Drum clips: i <N> <onset> 0.001 <velocity>
; p4 = velocity (0–1)
; ════════════════════════════════════════════════════════════════════════════

instr   101     ;BASS DRUM
    xtratim 0.1
    p3      =       2*i(gkdur1)
    kmul    transeg 0.2,p3*0.5,-15,0.01, p3*0.5,0,0
    kbend   transeg 0.5,1.2,-4, 0,1,0,0
    asig    gbuzz   0.5,50*octave(gktune1)*semitone(kbend),20,1,kmul,gicos
    aenv    transeg 1,p3-0.004,-6,0
    aatt    linseg  0,0.004,1
    asig    =       asig*aenv*aatt
    aenv    linseg  1,0.07,0
    acps    expsega 400,0.07,0.001,1,0.001
    aimp    oscili  aenv,acps*octave(gktune1*0.25),gisine
    amix    =       ((asig*0.5)+(aimp*0.35))*gklevel1*p4*gklevel
    aL,aR   pan2    amix,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   102     ;SNARE DRUM
    xtratim 0.1
    ifrq    =       342
    iNseDur =       0.3*i(gkdur2)
    iPchDur =       0.1*i(gkdur2)
    p3      =       iNseDur
    aenv1   expseg  1,iPchDur,0.0001,p3-iPchDur,0.0001
    apitch1 oscili  1,ifrq*octave(gktune2),gisine
    apitch2 oscili  0.25,ifrq*0.5*octave(gktune2),gisine
    apitch  =       (apitch1+apitch2)*0.75
    aenv2   expon   1,p3,0.0005
    anoise  noise   0.75,0
    anoise  butbp   anoise,10000*octave(gktune2),10000
    anoise  buthp   anoise,1000
    kcf     expseg  5000,0.1,3000,p3-0.2,3000
    anoise  butlp   anoise,kcf
    amix    =       ((apitch*aenv1)+(anoise*aenv2))*gklevel2*p4*gklevel
    aL,aR   pan2    amix,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   103     ;OPEN HIGH HAT
    xtratim 0.1
    kFrq1   =       296*octave(gktune3)
    kFrq2   =       285*octave(gktune3)
    kFrq3   =       365*octave(gktune3)
    kFrq4   =       348*octave(gktune3)
    kFrq5   =       420*octave(gktune3)
    kFrq6   =       835*octave(gktune3)
    p3      =       0.5*i(gkdur3)
    aenv    linseg  1,p3-0.05,0.1,0.05,0
    ipw     =       0.25
    a1      vco2    0.5,kFrq1,2,ipw
    a2      vco2    0.5,kFrq2,2,ipw
    a3      vco2    0.5,kFrq3,2,ipw
    a4      vco2    0.5,kFrq4,2,ipw
    a5      vco2    0.5,kFrq5,2,ipw
    a6      vco2    0.5,kFrq6,2,ipw
    amix    sum     a1,a2,a3,a4,a5,a6
    amix    reson   amix,5000*octave(gktune3),5000,1
    amix    buthp   amix,5000
    amix    buthp   amix,5000
    amix    =       amix*aenv
    kcf     expseg  20000,0.7,9000,p3-0.1,9000
    anoise  noise   0.8,0
    aenv    linseg  1,p3-0.05,0.1,0.05,0
    anoise  butlp   anoise,kcf
    anoise  buthp   anoise,8000
    anoise  =       anoise*aenv
    amix    =       (amix+anoise)*gklevel3*p4*0.55*gklevel
    aL,aR   pan2    amix,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   104     ;CLOSED HIGH HAT  (mutes Open HH when active)
    xtratim 0.1
    kFrq1   =       296*octave(gktune4)
    kFrq2   =       285*octave(gktune4)
    kFrq3   =       365*octave(gktune4)
    kFrq4   =       348*octave(gktune4)
    kFrq5   =       420*octave(gktune4)
    kFrq6   =       835*octave(gktune4)
    idur    =       0.088*i(gkdur4)
    p3      limit   idur,0.1,10
    iactive active  p1-1
    if iactive>0 then
     turnoff2 p1-1,0,0
    endif
    aenv    expsega 1,idur,0.001,1,0.001
    ipw     =       0.25
    a1      vco2    0.5,kFrq1,2,ipw
    a2      vco2    0.5,kFrq2,2,ipw
    a3      vco2    0.5,kFrq3,2,ipw
    a4      vco2    0.5,kFrq4,2,ipw
    a5      vco2    0.5,kFrq5,2,ipw
    a6      vco2    0.5,kFrq6,2,ipw
    amix    sum     a1,a2,a3,a4,a5,a6
    amix    reson   amix,5000*octave(gktune4),5000,1
    amix    buthp   amix,5000
    amix    buthp   amix,5000
    amix    =       amix*aenv
    anoise  noise   0.8,0
    aenv    expsega 1,idur,0.001,1,0.001
    kcf     expseg  20000,0.7,9000,idur-0.1,9000
    anoise  butlp   anoise,kcf
    anoise  buthp   anoise,8000
    anoise  =       anoise*aenv
    amix    =       (amix+anoise)*gklevel4*p4*0.55*gklevel
    aL,aR   pan2    amix,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   105     ;HIGH TOM
    xtratim 0.1
    ifrq    =       200*octave(i(gktune5))
    p3      =       0.5*i(gkdur5)
    aAmpEnv transeg 1,p3,-10,0.001
    afmod   expsega 5,0.125/ifrq,1,1,1
    asig    oscili  -aAmpEnv*0.6,ifrq*afmod,gisine
    aEnvNse transeg 1,p3,-6,0.001
    anoise  dust2   0.4,8000
    anoise  reson   anoise,400*octave(i(gktune5)),800,1
    anoise  buthp   anoise,100*octave(i(gktune5))
    anoise  butlp   anoise,1000*octave(i(gktune5))
    anoise  =       anoise*aEnvNse
    amix    =       (asig+anoise)*gklevel5*p4*gklevel
    aL,aR   pan2    amix,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   106     ;MID TOM
    xtratim 0.1
    ifrq    =       133*octave(i(gktune6))
    p3      =       0.6*i(gkdur6)
    aAmpEnv transeg 1,p3,-10,0.001
    afmod   expsega 5,0.125/ifrq,1,1,1
    asig    oscili  -aAmpEnv*0.6,ifrq*afmod,gisine
    aEnvNse transeg 1,p3,-6,0.001
    anoise  dust2   0.4,8000
    anoise  reson   anoise,400*octave(i(gktune6)),800,1
    anoise  buthp   anoise,100*octave(i(gktune6))
    anoise  butlp   anoise,600*octave(i(gktune6))
    anoise  =       anoise*aEnvNse
    amix    =       (asig+anoise)*gklevel6*p4*gklevel
    aL,aR   pan2    amix,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   107     ;LOW TOM
    xtratim 0.1
    ifrq    =       90*octave(i(gktune7))
    p3      =       0.7*i(gkdur7)
    aAmpEnv transeg 1,p3,-10,0.001
    afmod   expsega 5,0.125/ifrq,1,1,1
    asig    oscili  -aAmpEnv*0.6,ifrq*afmod,gisine
    aEnvNse transeg 1,p3,-6,0.001
    anoise  dust2   0.4,8000
    anoise  reson   anoise,40*octave(gktune7),800,1
    anoise  buthp   anoise,100*octave(i(gktune7))
    anoise  butlp   anoise,600*octave(i(gktune7))
    anoise  =       anoise*aEnvNse
    amix    =       (asig+anoise)*gklevel7*p4*gklevel
    aL,aR   pan2    amix,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   108     ;CYMBAL
    xtratim 0.1
    kFrq1   =       296*octave(gktune8)
    kFrq2   =       285*octave(gktune8)
    kFrq3   =       365*octave(gktune8)
    kFrq4   =       348*octave(gktune8)
    kFrq5   =       420*octave(gktune8)
    kFrq6   =       835*octave(gktune8)
    p3      =       2*i(gkdur8)
    aenv    expon   1,p3,0.0001
    ipw     =       0.25
    a1      vco2    0.5,kFrq1,2,ipw
    a2      vco2    0.5,kFrq2,2,ipw
    a3      vco2    0.5,kFrq3,2,ipw
    a4      vco2    0.5,kFrq4,2,ipw
    a5      vco2    0.5,kFrq5,2,ipw
    a6      vco2    0.5,kFrq6,2,ipw
    amix    sum     a1,a2,a3,a4,a5,a6
    amix    reson   amix,5000*octave(gktune8),5000,1
    amix    buthp   amix,10000
    amix    butlp   amix,12000
    amix    butlp   amix,12000
    amix    =       amix*aenv
    anoise  noise   0.8,0
    aenv    expsega 1,0.3,0.07,p3-0.1,0.00001
    kcf     expseg  14000,0.7,7000,p3-0.1,5000
    anoise  butlp   anoise,kcf
    anoise  buthp   anoise,8000
    anoise  =       anoise*aenv
    amix    =       (amix+anoise)*gklevel8*p4*0.85*gklevel
    aL,aR   pan2    amix,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   109     ;RIM SHOT
    xtratim 0.1
    ; table created at global scope (giTR808RimShot) — ftgenonce not available in CsoundUnity
    idur    =       0.027*i(gkdur9)
    p3      limit   idur,0.1,10
    aenv1   expsega 1,idur,0.001,1,0.001
    ifrq1   =       1700*octave(i(gktune9))
    aring   oscili  1,ifrq1,giTR808RimShot,0
    aring   butbp   aring,ifrq1,ifrq1*8
    aring   =       aring*(aenv1-0.001)*0.5
    anoise  noise   1,0
    aenv2   expsega 1,0.002,0.8,0.005,0.5,idur-0.002-0.005,0.0001,1,0.0001
    anoise  buthp   anoise,800
    kcf     expseg  4000,p3,20
    anoise  butlp   anoise,kcf
    anoise  =       anoise*(aenv2-0.001)
    amix    =       (aring+anoise)*gklevel9*p4*0.8*gklevel
    aL,aR   pan2    amix,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   110     ;CLAVES
    xtratim 0.1
    ifrq    =       2500*octave(i(gktune10))
    idur    =       0.045*i(gkdur10)
    p3      limit   idur,0.1,10
    aenv    expsega 1,idur,0.001,1,0.001
    afmod   expsega 3,0.00005,1,1,1
    asig    oscili  -(aenv-0.001),ifrq*afmod,gisine,0
    asig    =       asig*0.4*gklevel10*p4*gklevel
    aL,aR   pan2    asig,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   111     ;COWBELL
    xtratim 0.1
    ifrq1   =       562*octave(i(gktune11))
    ifrq2   =       845*octave(i(gktune11))
    ipw     =       0.5
    idur    =       0.7
    p3      =       idur*i(gkdur11)
    ishape  =       -30
    kenv1   transeg 1,p3*0.3,ishape,0.2, p3*0.7,ishape,0.2
    kenv2   expon   1,p3,0.0005
    kenv    =       kenv1*kenv2
    itype   =       2
    a1      vco2    0.65,ifrq1,itype,ipw
    a2      vco2    0.65,ifrq2,itype,ipw
    amix    =       a1+a2
    iLPF2   =       10000
    kcf     expseg  12000,0.07,iLPF2,1,iLPF2
    alpf    butlp   amix,kcf
    abpf    reson   amix,ifrq2,25
    amix    dcblock2 (abpf*0.06*kenv1)+(alpf*0.5)+(amix*0.9)
    amix    buthp   amix,700
    amix    =       amix*0.07*kenv*p4*gklevel11*gklevel
    aL,aR   pan2    amix,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   112     ;CLAP
    iTimGap =       0.01
    idur1   =       0.02
    idur2   =       2*i(gkdur12)
    idens   =       8000
    iamp1   =       0.5
    iamp2   =       1
    if frac(p1)==0 then
     event_i "i", p1+0.1, 0,         idur1, p4
     event_i "i", p1+0.1, iTimGap,   idur1, p4
     event_i "i", p1+0.1, iTimGap*2, idur1, p4
     event_i "i", p1+0.1, iTimGap*3, idur2, p4
    else
     kenv    transeg 1,p3,-25,0
     iamp    random  0.7,1
     anoise  dust2   kenv*iamp,idens
     iBPF    =       1100*octave(i(gktune12))
     ibw     =       2000*octave(i(gktune12))
     iHPF    =       1000
     iLPF    =       1
     kcf     expseg  8000,0.07,1700,1,800,2,500,1,500
     asig    butlp   anoise,kcf*iLPF
     asig    buthp   asig,iHPF
     ares    reson   asig,iBPF,ibw,1
     asig    dcblock2 (asig*iamp1)+(ares*iamp2)
     asig    =       asig*p4*i(gklevel12)*1.75*gklevel
     aL,aR   pan2    asig,(p5+1)*0.5
             outs    aL,aR
    ga1 += (aL + aR) * 0.5
    endif
endin

instr   113     ;MARACA
    xtratim 0.1
    idur    =       0.07*i(gkdur13)
    p3      limit   idur,0.1,10
    iHPF    limit   6000*octave(i(gktune13)),20,sr/2
    iLPF    limit   12000*octave(i(gktune13)),20,sr/3
    iBP1    =       0.4
    iDur1   =       0.014*i(gkdur13)
    iBP2    =       1
    iDur2   =       0.01*i(gkdur13)
    iBP3    =       0.05
    p3      limit   idur,0.1,10
    aenv    expsega iBP1,iDur1,iBP2,iDur2,iBP3
    anoise  noise   0.75,0
    anoise  buthp   anoise,iHPF
    anoise  butlp   anoise,iLPF
    anoise  =       anoise*aenv*p4*gklevel13*gklevel
    aL,aR   pan2    anoise,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   114     ;HIGH CONGA
    xtratim 0.1
    ifrq    =       420*octave(i(gktune14))
    p3      =       0.22*i(gkdur14)
    aenv    transeg 0.7,1/ifrq,1,1,p3,-6,0.001
    afrq    expsega ifrq*3,0.25/ifrq,ifrq,1,ifrq
    asig    oscili  -aenv*0.25,afrq,gisine
    asig    =       asig*p4*gklevel14*gklevel
    aL,aR   pan2    asig,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   115     ;MID CONGA
    xtratim 0.1
    ifrq    =       310*octave(i(gktune15))
    p3      =       0.33*i(gkdur15)
    aenv    transeg 0.7,1/ifrq,1,1,p3,-6,0.001
    afrq    expsega ifrq*3,0.25/ifrq,ifrq,1,ifrq
    asig    oscili  -aenv*0.25,afrq,gisine
    asig    =       asig*p4*gklevel15*gklevel
    aL,aR   pan2    asig,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

instr   116     ;LOW CONGA
    xtratim 0.1
    ifrq    =       227*octave(i(gktune16))
    p3      =       0.41*i(gkdur16)
    aenv    transeg 0.7,1/ifrq,1,1,p3,-6,0.001
    afrq    expsega ifrq*3,0.25/ifrq,ifrq,1,ifrq
    asig    oscili  -aenv*0.25,afrq,gisine
    asig    =       asig*p4*gklevel16*gklevel
    aL,aR   pan2    asig,(p5+1)*0.5
            outs    aL,aR
    ga1 += (aL + aR) * 0.5
endin

; ── Melodic synth (Step Sequencer) ──────────────────────────────────────────
; Triggered by a Step-mode Timeline clip.
; p4 = velocity 0–1   p5 = pan -1..1   p6 = pitch Hz
instr 10
    ivel    =       p4
    ifreq   =       p6
    iatt    =       0.005
    idec    =       0.08
    isus    =       0.5
    irel    =       p3 * 0.3

    ; Slightly detuned oscillators for width
    asig1   poscil  1, ifreq
    asig2   poscil  1, ifreq * 1.003
    asig    =       (asig1 + asig2) * 0.5

    ; Low-pass filter with slight resonance
    kcut    expseg  ifreq * 6, iatt, ifreq * 2, p3, ifreq * 1.2
    asig    moogladder asig, kcut, 0.3

    aenv    adsr    iatt, idec, isus, irel
    asig    =       asig * aenv * ivel

    kmaster chnget  "masterLevel"
    asig    =       asig * kmaster

    aL, aR  pan2    asig, (p5 + 1) * 0.5
            outs    aL, aR
    ga1     +=      (aL + aR) * 0.5
endin

; ── Reverb bus ───────────────────────────────────────────────────────────────
; Reads the mono ga1 bus, applies reverbsc, outputs wet signal, clears bus.
instr 99
    kMix    chnget  "reverbMix"
    aWetL, aWetR reverbsc ga1, ga1, 0.82, 8000
    outs    aWetL * kMix, aWetR * kMix
    ga1     =   0
endin

</CsInstruments>

<CsScore>
f 0 [3600*24*7]
; Instr 2 writes channel defaults once at t=0
i 2 0 0.001
; Instr 1 runs for the whole session, keeping global vars updated
i 1 0 [3600*24*7]
; Reverb bus processor
i 99 0 [3600*24*7]
</CsScore>

</CsoundSynthesizer>
