<Cabbage> bounds(0, 0, 0, 0)

form caption("Sample Flooper") size(650, 480), guiMode("queue") pluginId("def1")

button bounds(56, 172, 80, 40) channel("trigger") text("Trigger")
button bounds(56, 270, 80, 40) channel("stop") text("Stop")

rslider bounds(58, 214, 76, 53), channel("dur"), range(0.2, 120, 10, 1, 0.01), text("Duration"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

label  bounds(252, 164, 80, 20), text("Sample"), fontColour(255, 255, 255, 255) channel("label44")
combobox bounds(246, 190, 95, 28), channel("sound"),fontColour("White"),text("Pins","Dog","Traffic","Bird","Fire","Hello","Perotin","Brahms","Tomek1","Tomek2")

groupbox bounds(188, 12, 104, 105) channel("groupbox2") text("Pitch") colour(0, 0, 0, 0) outlineColour(0, 0, 0, 50), textColour(255, 255, 255, 255) fontColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255)
rslider bounds(202, 34, 79, 66), channel("pitch"), range(0.1, 4, 1, 1, 0.01), text("Pitch"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

groupbox bounds(306, 10, 106, 105) channel("groupbox1") text("LoopStart") colour(0, 0, 0, 0) outlineColour(0, 0, 0, 50), textColour(255, 255, 255, 255) fontColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255)
rslider bounds(320, 36, 79, 66), channel("loopStrt"), range(0, 1, 0, 1, 0.01), text("LoopStart"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

groupbox bounds(418, 10, 106, 105) channel("groupbox3") text("LoopEnd") colour(0, 0, 0, 0) outlineColour(0, 0, 0, 50), textColour(255, 255, 255, 255) fontColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255)
rslider bounds(434, 38, 79, 66), channel("loopEnd"), range(0, 1, 1, 1, 0.01), text("LoopEnd"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

groupbox bounds(530, 10, 106, 105) channel("groupbox4") text("LoopDirection") colour(0, 0, 0, 0) outlineColour(0, 0, 0, 50), textColour(255, 255, 255, 255) fontColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255)
rslider bounds(546, 38, 79, 66), channel("loopDir"), range(0, 2, 0, 1, 0.01), text("F-B-FB"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

hslider bounds(208, 358, 181, 50) channel("pan"), range(0, 1, 0.5, 1, 0.001), text("Pan") textColour(0, 0, 0, 255),  trackerColour(255, 255, 255, 255)

rslider bounds(214, 248, 181, 74) channel("sampLvl"), range(0, 1, 0.75, 1, 0.001), text("Sampler") textColour(0, 0, 0, 255), trackerColour(255, 255, 255, 255)

label  bounds(476, 120, 104, 20), text("Global FX"), fontColour(255, 255, 255, 255) channel("label74")

rslider bounds(456, 142, 72, 51) channel("chorLvl"), range(0, 1, 0.76, 1, 0.001), text("Chorus") textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)
rslider bounds(536, 142, 71, 51) channel("chorMod"), range(0.01, 0.5, 0.25, 1, 0.001), text("ChorusMod") textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)

rslider bounds(458, 198, 68, 51), channel("echoLvl"), range(0, 1, 0.75, 1, 0.01), text("Echo"), textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)
rslider bounds(536, 198, 72, 51), channel("echoMod"), range(0.01, 2, 1, 1, 0.01), text("EchoMod"), textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)

rslider bounds(458, 252, 68, 51), channel("flangLvl"), range(0, 1, 0.75, 1, 0.01), text("Flanger"), textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)
rslider bounds(536, 252, 73, 51), channel("flangMod"), range(0.1, 1, 0.5, 1, 0.01), text("FlangerMod"), textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)

rslider bounds(460, 310, 68, 51), channel("stresLvl"), range(0, 1, 0.75, 1, 0.01), text("StringReson"), textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)
rslider bounds(534, 310, 73, 51), channel("stresMod"), range(0.25, 4, 1, 1, 0.01), text("StresonMod"), textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)

rslider bounds(462, 362, 65, 51), channel("verbLvl"), range(0, 1, 0.75, 1, 0.01), text("Verb"), textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)
rslider bounds(530, 362, 74, 51), channel("verbMod"), range(0.1, 0.98, 0.8, 1, 0.01), text("VerbMod"), textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)

rslider bounds(460, 418, 68, 51), channel("panLvl"), range(0, 1, 0.5, 1, 0.01), text("FX Pan"), textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)
rslider bounds(530, 418, 75, 51), channel("panMod"), range(0, 3, 0.1, 1, 0.01), text("FXpanMod"), textColour(255, 255, 255, 255), trackerColour(255, 255, 255, 255)

rslider bounds(40, 330, 119, 108), channel("masterLvl"), range(0, 1, 0.818, 1, 0.01), text("Master"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

label  bounds(42, 36, 80, 23), text("Presets"), fontColour(255, 255, 255, 255) channel("label37")
combobox bounds(76, 68, 100, 25), populate("*.snaps"), channelType("string") automatable(0) channel("combo31")  value("1") text("init", "pins - long+low", "dog - squeeky", "dog - groove", "traffic - rumble", "traffic - harmony", "bird - loop", "bird - loop2", "fire - low & rhythmic", "fire - back&forth", "fire - dry loop", "Boulanger - JeanLuc ;-)", "hello - grove", "hello - groove2", "Perotin - back+forth", "Perotin - Druids", "Brahms - Frozen Chord", "Brahms - wave of chords", "Tomek2 - with echo + rev", "Tomek - Tenet", "Tomek2 - StrangeAmbience", "Tomek2 - groove", "Tomek2 - Loop3")
filebutton bounds(14, 68, 60, 25), text("Save", "Save"), populate("*.snaps", "test"), mode("named preset") channel("filebutton32")
filebutton bounds(14, 98, 60, 25), text("Remove", "Remove"), populate("*.snaps", "test"), mode("remove preset") channel("filebutton33")

</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
sr = 48000
ksmps = 32
nchnls = 2
0dbfs = 1

; Sound Design and presets by Dr. Richard Boulanger and his students at Berkelee
; ported to CsoundUnity by gb, June 2023

; set this var to 0 when testing on Cabbage, to 1 when using this csd on Unity, 
; Unity comboboxes start index is 0, instead on Cabbage they start from 1 (0 is the unset value)
; we need to do this because we created the Unity presets from the Cabbage Snaps
; using the import Snaps feature (where the index read from the Cabbage Snap 
; is decreased by one - that is to show the correct combobox value in the channel inspector)
; so to obtain the same preset result and load the correct sample we need to compensate for this
; see line 154 below
giUnityCombobox init 1

gSfile1   = "./sounds/am_pinsM.wav"
gSfile2   = "./sounds/am_dogM.aif"
gSfile3   = "./sounds/am_trafficM.aif"
gSfile4   = "./sounds/am_blackbirdM.aif"
gSfile5   = "./sounds/am_fireM.wav"
gSfile6   = "./sounds/sp_hellorcbBerkleeM.aif"
gSfile7   = "./sounds/mu_PerotinM.wav"
gSfile8   = "./sounds/mu_brahmsM.aif"
gSfile9   = "./sounds/dl_TomekPansM.wav"
gSfile10  = "./sounds/dl_TomekBowlsM.aif"

giSmp1   ftgen  1, 0, 0, 1,  gSfile1, 0, 0, 0
giSmp2   ftgen  2, 0, 0, 1,  gSfile2, 0, 0, 0
giSmp3   ftgen  3, 0, 0, 1,  gSfile3, 0, 0, 0
giSmp4   ftgen  4, 0, 0, 1,  gSfile4, 0, 0, 0
giSmp5   ftgen  5, 0, 0, 1,  gSfile5, 0, 0, 0
giSmp6   ftgen  6, 0, 0, 1,  gSfile6, 0, 0, 0
giSmp7   ftgen  7, 0, 0, 1,  gSfile7, 0, 0, 0
giSmp8   ftgen  8, 0, 0, 1,  gSfile8, 0, 0, 0
giSmp9   ftgen  9, 0, 0, 1,  gSfile9, 0, 0, 0
giSmp10  ftgen 10, 0, 0, 1,  gSfile10, 0, 0, 0

gaChorL init 0
gaChorR init 0

gaEchoL init 0
gaEchoR init 0

gaFlangL init 0
gaFlangR init 0

gaStresL init 0
gaStresR init 0

gaMtapL init 0
gaMtapR init 0

gaRevL init 0
gaRevR init 0

gaFxPan init 0

giFun21 ftgen 21, 0, 4097, -7, 1, 4096, 0  ; Ramp down
giFun22 ftgen 22, 0, 4097, -7, 0, 4096, 1  ; Ramp up
giFun23 ftgen 23, 0, 4097, 19, .5, 1, 0, 0 ; Half Sine


instr 1
  kDur = chnget:k("dur")
  kTrig = chnget:k("trigger")
  
    if changed(kTrig) == 1 then
        event "i", "Flooper", 0, kDur
        kTrig = 0
    endif
    
    kStop = chnget:k("stop")
    if changed(kStop) == 1 then
       turnoff2 "Flooper", 0, 1
       kStop = 0
    endif
endin


instr Flooper

iDur       = chnget:i("dur")	
kGain      = chnget:k("masterLvl")
kPitch     = chnget:k("pitch")
kLoopStrt  = chnget:k("loopStrt") 
kLoopEnd   = chnget:k("loopEnd") 
iMode      = chnget:i("loopDir")
kInput     = chnget:k("sound")


flooperInit:

input     = i(kInput)           ;chnget:i("sound")

; this is to be overcome the difference in ComboBoxes behaviour on Unity, as explained above
input += giUnityCombobox

iLen init 0
iSmp init 0

if input == 1 then
iLen filelen gSfile1
iSmp = 1
elseif input == 2 then
iLen filelen gSfile2
iSmp = 2
elseif input == 3 then
iLen filelen gSfile3
iSmp = 3
elseif input == 4 then
iLen filelen gSfile4
iSmp = 4
elseif input == 5 then
iLen filelen gSfile5
iSmp = 5
elseif input == 6 then
iLen filelen gSfile6
iSmp = 6
elseif input == 7 then
iLen filelen gSfile7
iSmp = 7
elseif input == 8 then
iLen filelen gSfile8
iSmp = 8
elseif input == 9 then
iLen filelen gSfile9
iSmp = 9
else 
iLen filelen gSfile10
iSmp = 10
endif

prints "LOADING SAMPLE: %d Len: %d\n", iSmp, iLen

if kLoopStrt > kLoopEnd then
kLoopStrt = kLoopEnd-.1
endif
if kLoopEnd < kLoopStrt then
kLoopEnd = kLoopStrt+.1
endif

;asig1[,asig2] flooper2 kamp,  kpitch, kloopstart, kloopend, kcrossfade, ifn [, istart, imode, ifenv, iskip] 
aFloop         flooper2 kGain, kPitch, .001+(kLoopStrt * iLen), .001+(kLoopEnd * iLen), 0.05, iSmp, i(kLoopStrt) * iLen, int(iMode)

rireturn

kInputChanged changed kInput    ;chnget:k("sound")
if (kInputChanged == 1) then
    reinit flooperInit
endif

kEnv    transeg 0,iDur*.01,0,1,iDur*.8,0,1,iDur*.19,-1,0          
aDry    =       aFloop * kEnv * chnget:k("sampLvl")
aL,aR   pan2    aDry, chnget:k("pan")
        outs    aL * chnget:k("masterLvl"), aR * chnget:k("masterLvl")
        
        vincr   gaChorL, aL*chnget:k("chorLvl")
        vincr   gaChorR, aR*chnget:k("chorLvl") 
        vincr   gaEchoL, aL*chnget:k("echoLvl")
        vincr   gaEchoR, aR*chnget:k("echoLvl") 
        vincr   gaFlangL, aL*chnget:k("flangLvl")
        vincr   gaFlangR, aR*chnget:k("flangLvl") 
        vincr   gaStresL, aL*chnget:k("stresLvl")
        vincr   gaStresR, aR*chnget:k("stresLvl") 
        vincr   gaRevL,  aL*chnget:k("verbLvl")
        vincr   gaRevR,  aR*chnget:k("verbLvl")

endin

; -----------------------------------

instr   Chorus  ; from Lazzarini
     denorm gaChorL, gaChorR
 amod1  =   randi:a(3,.75)+oscili(2,.35)+31
 amod2  =   randi:a(2,.65)+oscili(3,.55)+29
 amodX  randi chnget:k("chorMod"), 10
 ;amodX  portk amodX,.1
 aL     =   vdelay(gaChorL,amod1*amodX,35)
 aR     =   vdelay(gaChorR,amod2*(1-amodX),35)
 
 vincr gaFxPan, (aL+aR)*.5
  
            outs  aL*chnget:k("masterLvl"), aR*chnget:k("masterLvl")
   clear gaChorL, gaChorR
endin

; -----------------------------

instr    Echo ; MN3011 BBD Reverb from Steven Cook
         denorm gaEchoL, gaEchoR
         
 ifdbkL    init .31
 iEchoL    init .34
 afdbkL    init 0
 idelayL   init 4.35
 
 kEchoMOD  randi .1*chnget:k("echoMod"), 2*chnget:k("echoMod")
 kEchoMOD portk kEchoMOD, .4

 gaEchoL      = gaEchoL + afdbkL*ifdbkL
 
 abufL     delayr  2
 atap1L    deltapi .0396*idelayL*kEchoMOD
 atap2L    deltapi .0662*idelayL
 atap3L    deltapi .1194*idelayL*kEchoMOD
 atap4L    deltapi .1726*idelayL
 atap5L    deltapi .2790*idelayL*kEchoMOD
 atap6L    deltapi .3328*idelayL
           delayw  gaEchoL
 
 afdbkL    butterlp  atap6L, 7000
 abbdL     sum  atap1L, atap2L, atap3L, atap4L, atap5L, atap6L
 abbdL     butterlp  abbdL, 7000
 
 aL = gaEchoL + (abbdL*iEchoL)
 
 ifdbkR    init .45
 iEchoR    init .35
 afdbkR    init 0
 idelayR   init 4.6
 
 gaEchoR      = gaEchoR + afdbkR*ifdbkR
 
 abufR     delayr  1
 atap1R    deltapi .0396*idelayR
 atap2R    deltapi .0662*idelayR*kEchoMOD
 atap3R    deltapi .1194*idelayR
 atap4R    deltapi .1726*idelayR*kEchoMOD
 atap5R    deltapi .2790*idelayR
 atap6R    deltapi .3328*idelayR*kEchoMOD
           delayw  gaEchoL
 
 afdbkR    butterlp  atap6R, 7000
 abbdR     sum  atap1R, atap2R, atap3R, atap4R, atap5R, atap6R
 
 abbdR     butterlp  abbdR, 7000
 
 aR = gaEchoR + (abbdR*iEchoR)
 
vincr gaFxPan, (aL+aR)*.5

            outs  aL*chnget:k("masterLvl"), aR*chnget:k("masterLvl")
        clear gaEchoL, gaEchoR
 endin

;---------------------------------------

 instr    Flanger ; Barberpole flanger from Steven Cook
 
 idepth   init 2/1000                        ; Depth in ms
 irate    init 1.618                         ; Rate
 ifeed    init .8532                         ; Feedback
; Mode: based on ftable 21 and ftable 22 = 21 is down, 22 is up
 
 kFlangMod = chnget:k("flangMod")
 kFLangMod portk kFlangMod, .1
 
 denorm gaFlangL, gaFlangR      
 
 asaw1L    oscili  idepth, irate+kFlangMod, giFun21       ; Saw 0 degrees
 asaw2L    oscili  idepth, irate+kFlangMod, giFun21, .25  ; Saw 90 degrees
 asaw3L    oscili  idepth, irate+kFlangMod, giFun21, .5   ; Saw 180 degrees
 asaw4L    oscili  idepth, irate+kFlangMod, giFun21, .75  ; Saw 270 degrees
 
 asin1L    oscili  1, irate+kFlangMod, giFun23                ; 1/2 Sine 0 degrees
 asin2L    oscili  1, irate+kFlangMod, giFun23, .25           ; 1/2 Sine 90 degrees
 asin3L    oscili  1, irate+kFlangMod, giFun23, .5            ; 1/2 Sine 180 degrees
 asin4L    oscili  1, irate+kFlangMod, giFun23, .75           ; 1/2 Sine 270 degrees
 
 adel1L    flanger   gaFlangL, asaw1L*kFlangMod, ifeed*kFlangMod, rnd(idepth) ; Flanger 1
 adel2L    flanger  gaFlangL, asaw2L*kFlangMod, ifeed*kFlangMod, rnd(idepth) ; Flanger 2
 adel3L    flanger  gaFlangL, asaw3L*kFlangMod, ifeed*kFlangMod, rnd(idepth) ; Flanger 3
 adel4L    flanger  gaFlangL, asaw4L*kFlangMod, ifeed*kFlangMod, rnd(idepth) ; Flanger 4
 
 aflangeL  = adel1L*asin1L + adel2L*asin2L + adel3L*asin3L + adel4L*asin4L
 aflangeL  dcblock  aflangeL
 adirectL  = gaFlangL*(asin1L + asin2L + asin3L + asin4L)

 asaw1R    oscili  idepth, irate+rnd(kFlangMod), giFun22       ; Saw 0 degrees
 asaw2R    oscili  idepth, irate+rnd(kFlangMod), giFun22, .25  ; Saw 90 degrees
 asaw3R    oscili  idepth, irate+rnd(kFlangMod), giFun22, .5   ; Saw 180 degrees
 asaw4R    oscili  idepth, irate+rnd(kFlangMod), giFun22, .75  ; Saw 270 degrees
 
 asin1R    oscili  1, irate+kFlangMod, giFun23                ; 1/2 Sine 0 degrees
 asin2R    oscili  1, irate+kFlangMod, giFun23, .25           ; 1/2 Sine 90 degrees
 asin3R    oscili  1, irate+kFlangMod, giFun23, .5            ; 1/2 Sine 180 degrees
 asin4R    oscili  1, irate+kFlangMod, giFun23, .75           ; 1/2 Sine 270 degrees
 
 adel1R    flanger  gaFlangR, asaw1R*kFlangMod, rnd(ifeed), idepth ; Flanger 1
 adel2R    flanger  gaFlangR, asaw2R*kFlangMod, rnd(ifeed), idepth ; Flanger 2
 adel3R    flanger  gaFlangR, asaw3R*kFlangMod, rnd(ifeed), idepth ; Flanger 3
 adel4R    flanger  gaFlangR, asaw4R*kFlangMod, rnd(ifeed), idepth ; Flanger 4
 
 aflangeR  = adel1R*asin1R + adel2R*asin2R + adel3R*asin3R + adel4R*asin4R
 aflangeR  dcblock  aflangeR
 adirectR  = gaFlangR*(asin1R + asin2R + asin3R + asin4R)

aL =    aflangeL + adirectL      
aR =    aflangeR + adirectR

vincr gaFxPan, (aL+aR) * .5 

           outs  aL*chnget:k("masterLvl"), aR*chnget:k("masterLvl")

 clear gaFlangL, gaFlangR 
 endin

;----------------------------

instr  Streson             ; 'Streson' resonator bank after Steven Cook
denorm gaStresL, gaStresR

 kMod =  chnget:k("stresMod")
 kMod portk kMod, .1
   
 ifdbk     = .99
   
 ipitch1L   = cpsmidinn(36)+int(rnd(6))   ; Filter 1 freq
 ipitch2L   = cpsmidinn(50)+int(rnd(5))   ; Filter 2 freq
 ipitch3L   = cpsmidinn(52)+int(rnd(4))   ; Filter 3 freq
 ipitch4L   = cpsmidinn(53)+int(rnd(3))   ; Filter 4 freq
 ipitch5L   = cpsmidinn(55)+int(rnd(5))  ; Filter 5 freq
 ipitch6L   = cpsmidinn(59)+int(rnd(8))  ; Filter 6 freq
             
 ares1L     streson  gaStresL, ipitch1L*kMod, ifdbk
 ares2L     streson  gaStresL, ipitch2L*kMod, ifdbk
 ares3L     streson  gaStresL, ipitch3L*kMod, ifdbk
 ares4L     streson  gaStresL, ipitch4L*kMod, ifdbk
 ares5L     streson  gaStresL, ipitch5L*kMod, ifdbk
 ares6L     streson  gaStresL, ipitch6L*kMod, ifdbk
 
 aL      = (ares1L + ares2L + ares3L + ares4L + ares5L + ares6L) * .5
 
 ipitch1R   = cpsmidinn(25)+rnd(3)   ; Filter 1 freq
 ipitch2R   = cpsmidinn(33)+rnd(3)   ; Filter 2 freq
 ipitch3R   = cpsmidinn(41)+rnd(3)   ; Filter 3 freq
 ipitch4R   = cpsmidinn(59)+rnd(3)   ; Filter 4 freq
 ipitch5R   = cpsmidinn(64)+rnd(3)   ; Filter 5 freq
 ipitch6R   = cpsmidinn(77)+rnd(3)   ; Filter 6 freq
             
 ares1R     streson  gaStresR, ipitch1R*kMod, ifdbk
 ares2R     streson  gaStresR, ipitch2R*kMod, ifdbk
 ares3R     streson  gaStresR, ipitch3R*kMod, ifdbk
 ares4R     streson  gaStresR, ipitch4R*kMod, ifdbk
 ares5R     streson  gaStresR, ipitch5R*kMod, ifdbk
 ares6R     streson  gaStresR, ipitch6R*kMod, ifdbk
 
 aR      = (ares1R + ares2R + ares3R + ares4R + ares5R + ares6R) * .5
 
  vincr gaFxPan, (aL+aR)*.5
              
 outs  aL*chnget:k("masterLvl"), aR*chnget:k("masterLvl") 
 clear gaStresL, gaStresR 
 endin
  
 ;---------------------------
 
instr   Reverb
        denorm gaRevL, gaRevR
kverbMod = chnget:k("verbMod")
kverbMod portk kverbMod, .2
aL, aR  reverbsc gaRevL, gaRevR, kverbMod, 12000, sr, 0.5, 1

   vincr gaFxPan, aL+aR

   outs  aL*chnget:k("masterLvl"), aR*chnget:k("masterLvl")
 clear gaRevL, gaRevR
endin  

; ----------------------------

instr    FXpan
 denorm gaFxPan
 kMod oscil .5, chnget:k("panMod")
 kMod = kMod + .5
 aL, aR pan2 gaFxPan, chnget:k("panLvl") * kMod
outs  aL*chnget:k("masterLvl"), aR*chnget:k("masterLvl") 
 clear gaFxPan 
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 [60*60*24*7] 
i "Chorus" 0 [60*60*24*7]  
i "Echo" 0 [60*60*24*7]
i "Flanger" 0 [60*60*24*7]
i "Streson" 0 [60*60*24*7]
i "Reverb" 0 [60*60*24*7] 
i "FXpan" 0 [60*60*24*7]
</CsScore>
</CsoundSynthesizer>
