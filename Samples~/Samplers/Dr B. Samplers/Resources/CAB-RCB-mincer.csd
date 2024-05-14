<Cabbage> bounds(0, 0, 0, 0)

form caption("Sample Mincer by Dr.B") size(600, 200), guiMode("queue") pluginId("def1")

button bounds(26, 74, 80, 40) channel("trigger") text("Trigger")

label  bounds(136, 124, 80, 20), text("Sound"), fontColour(255, 255, 255, 255)
combobox bounds(130, 146, 95, 28), channel("sound"), fontColour(220, 220, 255, 255), text("Whisper", "Dog", "Traffic", "Bird", "QBfox", "DrB-hello", "Perotin", "Sheila", "Drum")

groupbox bounds(118, 14, 190, 105) text("Time") colour(0, 0, 0, 0) outlineColour(0, 0, 0, 50), textColour(255, 255, 255, 255) fontColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255)
rslider bounds(174, 40, 79, 66), channel("time"), range(0, 10, 2, 1, 0.01), text("Time"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

groupbox bounds(312, 14, 190, 105) text("Pitch") colour(0, 0, 0, 0) outlineColour(0, 0, 0, 50), textColour(255, 255, 255, 255) fontColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255)
rslider bounds(366, 38, 79, 66), channel("pitch"), range(-2, 2, .8, 1, 0.01), text("Pitch"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

rslider bounds(514, 96, 65, 51), channel("verbLvl"), range(0, 1, 0.75, 1, 0.01), text("Verb"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)
rslider bounds(34, 122, 65, 53), channel("masterLvl"), range(0, 1, 0.818, 1, 0.01), text("Gain"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

rslider bounds(512, 34, 65, 53), channel("mix"), range(0, 1, 0.25, 1, 0.01), text("Mix"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

rslider bounds(36, 14, 65, 53), channel("dur"), range(.2, 30, 10, 1, 0.01), text("Duration"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

label bounds(234, 122, 53, 18), text("Loop"), fontColour(255, 255, 255, 255)  channel("label66")
checkbox bounds(244, 144, 33, 28), channel("loop"), value(1), fontColour:0(255, 255, 255, 255) colour:1(255, 255, 255, 255)

combobox bounds(392, 122, 100, 25), populate("*.snaps"), channelType("string") automatable(0) channel("Snaps")  value("1") text("DronyBlur", "explosive", "swishy", "groovy", "dreamy", "sleepy")
filebutton bounds(330, 122, 60, 25), text("Save", "Save"), populate("*.snaps", "test"), mode("named preset") channel("filebutton32")
filebutton bounds(330, 152, 60, 25), text("Remove", "Remove"), populate("*.snaps", "test"), mode("remove preset") channel("filebutton33")

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

; set this var to 0 when testing on Cabbage (or comment the line), to 1 when using this csd on Unity, 
; Unity comboboxes start index is 0, instead on Cabbage they start from 1 (0 is the unset value)
; we need to do this because we created the Unity presets from the Cabbage Snaps
; using the import Snaps feature (where the index read from the Cabbage Snap 
; is decreased by one - that is to show the correct combobox value in the channel inspector)
; so to obtain the same preset result and load the correct sample we need to compensate for this
; see line 79 below
giUnityCombobox init 1

gSfile1 = "./sounds/am_whisperM.aif"
gSfile2 = "./sounds/am_dogM.aif"
gSfile3 = "./sounds/am_trafficM.aif"
gSfile4 = "./sounds/am_blackbirdM.aif"
gSfile5 = "./sounds/sp_foxM.wav"
gSfile6 = "./sounds/sp_hellorcbBerkleeM.aif"
gSfile7 = "./sounds/mu_PerotinM.wav"
gSfile8 = "./sounds/mu_SheilaM.wav"
gSfile9 = "./sounds/dl_breakM.aif"

giSmp1  ftgen 1, 0, 0, 1, gSfile1, 0, 0, 0
giSmp2  ftgen 2, 0, 0, 1, gSfile2, 0, 0, 0
giSmp3  ftgen 3, 0, 0, 1, gSfile3, 0, 0, 0
giSmp4  ftgen 4, 0, 0, 1, gSfile4, 0, 0, 0
giSmp5  ftgen 5, 0, 0, 1, gSfile5, 0, 0, 0
giSmp6  ftgen 6, 0, 0, 1, gSfile6, 0, 0, 0
giSmp7  ftgen 7, 0, 0, 1, gSfile7, 0, 0, 0
giSmp8  ftgen 8, 0, 0, 1, gSfile8, 0, 0, 0
giSmp9  ftgen 9, 0, 0, 1, gSfile9, 0, 0, 0

gaRvb init 0

instr 1 
  
  kTrig = chnget:k("trigger")
  kDur = chnget:k("dur")
  kInput = chnget:k("sound")

; this is to overcome the difference in ComboBoxes behaviour on Unity, as explained above
  kInput += giUnityCombobox
  
  if changed(kTrig) == 1 then
    event "i", "Mincer", 0, kDur, kInput
  endif
    
endin


instr Mincer

iTime      = chnget:i("time") 
kPitch     = chnget:k("pitch")

kinput      = p4

Sfile   = ""
ilen    init 0

iDur = p3
iInput = p4

;prints "iDur: %f, iInput: %f\n", iDur, iInput

if iInput = 0 then
    Sfile = gSfile1
elseif iInput = 1 then
    Sfile = gSfile1
elseif iInput = 2 then
    Sfile = gSfile2
elseif iInput = 3 then
    Sfile = gSfile3
elseif iInput = 4 then
    Sfile = gSfile4
elseif iInput = 5 then
    Sfile = gSfile5
elseif iInput = 6 then
    Sfile = gSfile6
elseif iInput = 7 then
    Sfile = gSfile7
elseif iInput = 8 then
    Sfile = gSfile8
else
    Sfile = gSfile9
endif

iLen    filelen Sfile 

prints "LOADING: %s, iLen: %d\n", Sfile, iLen

aSig    diskin2 Sfile, chnget:k("pitch"), 0, chnget:i("loop")
aTime   line   0,iLen,iLen*iTime
;asig mincer atimpt, kamp, kpitch, ktab, klock[,ifftsize,idecim]
aMinc   mincer aTime, 1, kPitch, iInput, 0 ;ilock

aOut    ntrpol       aSig, aMinc, chnget:k("mix") 
           
aOut = aOut * chnget:k("masterLvl"); * kEnv
            outs         aOut, aOut

    vincr gaRvb, aOut 

endin


instr Reverb                      
            denorm gaRvb
    aL, aR  reverbsc gaRvb, gaRvb, .8, 10000
            outs  aL*chnget:k("masterLvl")*chnget:k("verbLvl"), aR*chnget:k("masterLvl")*chnget:k("verbLvl")
            clear	gaRvb
            
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 [60*60*24*7] 
i "Reverb" 0 [60*60*24*7]  
</CsScore>
</CsoundSynthesizer>