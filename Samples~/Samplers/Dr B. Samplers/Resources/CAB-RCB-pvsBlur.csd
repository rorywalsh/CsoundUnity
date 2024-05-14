<Cabbage> bounds(0, 0, 0, 0)

form caption("Stereo Spectral Blur") size(800, 200), guiMode("queue") pluginId("def1")

button bounds(22, 72, 80, 40) channel("trigger") text("Trigger")

label    bounds(408, 128, 80, 20), text("FFT Size"), fontColour(255, 255, 255, 255)
combobox bounds(410, 154, 76, 24), text("128", "256", "512", "1024", "2048", "4096", "8192"), channel("att_table"), value(3), fontColour(220, 220, 255, 255)

label  bounds(160, 128, 80, 20), text("Sound"), fontColour(255, 255, 255, 255)
combobox bounds(154, 150, 95, 28), channel("sound"), value(3), fontColour(220, 220, 255, 255), text("Drums1", "Brahms", "Cage", "Bus", "Pad", "Drums2")
groupbox bounds(114, 12, 190, 105)text("Blur Time") colour(0, 0, 0, 0) outlineColour(0, 0, 0, 50), textColour(255, 255, 255, 255) fontColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255)
rslider bounds(170, 38, 79, 66), channel("blurTime"), range(0, 3, 0.618, 1, 0.01), text("Time"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

groupbox bounds(308, 12, 190, 105) text("Blur Rate") colour(0, 0, 0, 0) outlineColour(0, 0, 0, 50), textColour(255, 255, 255, 255) fontColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255)
rslider bounds(362, 36, 79, 66), channel("blurRate"), range(0, 3, 0.53, 1, 0.01), text("Rate"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

groupbox bounds(506, 12, 190, 105) text("Blur Depth") colour(0, 0, 0, 0) outlineColour(0, 0, 0, 50), textColour(255, 255, 255, 255) fontColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255) colour(0, 0, 0, 0) fontColour(255, 255, 255, 255) outlineColour(0, 0, 0, 50) textColour(255, 255, 255, 255)
rslider bounds(558, 38, 79, 66), channel("blurDepth"), range(0, 3, 0.35, 1, 0.01), text("Depth"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

rslider bounds(710, 118, 65, 53), channel("verbLvl"), range(0, 1, 0.65, 1, 0.01), text("Verb"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)
rslider bounds(32, 122, 65, 53), channel("masterLvl"), range(0, 1, 0.618, 1, 0.01), text("Gain"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)
rslider bounds(708, 24, 65, 53), channel("mix"), range(0, 1, 0.25, 1, 0.01), text("Mix"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)
rslider bounds(32, 12, 65, 53), channel("dur"), range(0, 20, 9, 1, 0.01), text("Duration"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

rslider bounds(272, 128, 65, 53), channel("speed"), range(-0.3, 1.3, 1.1, 1, 0.01), text("Speed"), trackerColour(255, 255, 255, 255), outlineColour(0, 0, 0, 50), textColour(0, 0, 0, 255)

label bounds(344, 128, 53, 18), text("Loop"), fontColour(255, 255, 255, 255)
checkbox bounds(354, 150, 33, 28), channel("loop"), value(1), fontColour:0(255, 255, 255, 255) colour:1(255, 255, 255, 255)

combobox bounds(558, 126, 100, 25), populate("*.snaps"), channelType("string") automatable(0) channel("Snaps")  value("1")
filebutton bounds(496, 126, 60, 25), text("Save", "Save"), populate("*.snaps", "test"), mode("named preset") channel("filebutton32")
filebutton bounds(496, 156, 60, 25), text("Remove", "Remove"), populate("*.snaps", "test"), mode("remove preset") channel("filebutton33")

</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

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
; see line 79 below
giUnityCombobox init 1

gSfile1 = "./sounds/drums1.aif"
gSfile2 = "./sounds/brahms.aif"
gSfile3 = "./sounds/cage.aif"
gSfile4 = "./sounds/bus.aif"
gSfile5 = "./sounds/pad.aif"
gSfile6 = "./sounds/drums2.aif"

gaRvb init 0
gkDur init 9

maxalloc "Blur", 3

/* FFT attribute tables - from Iain McCurdy*/
giFFTattributes1    ftgen    0, 0, 4, -2,  128,  64,  128, 1
giFFTattributes2    ftgen    0, 0, 4, -2,  256, 128,  256, 1
giFFTattributes3    ftgen    0, 0, 4, -2,  512, 128,  512, 1
giFFTattributes4    ftgen    0, 0, 4, -2, 1024, 256, 1024, 1
giFFTattributes5    ftgen    0, 0, 4, -2, 2048, 512, 2048, 1
giFFTattributes6    ftgen    0, 0, 4, -2, 4096,1024, 4096, 1
giFFTattributes7    ftgen    0, 0, 4, -2, 8192,2048, 8192, 1

instr 1
  gkDur     = chnget:k("dur")
  kInput    = chnget:k("sound")
  
  kTrig     = chnget:k("trigger")
    
    if changed(kTrig) == 1 then
        event "i", "Blur", 0, gkDur, kInput
    endif
endin

instr Blur	 ; spectral bluring instrument
 	
kGain      chnget "gain"
kBlurT     chnget "blurTime" 
kLFORate   chnget "blurRate"
kLFODepth  chnget "blurDepth"

/* SET FFT ATTRIBUTES - from Iain McCurdy*/
katt_table  chnget    "att_table"    ; FFT attribute table
katt_table  init    4
ktrig       changed    katt_table
    if ktrig==1 then
      reinit update
    endif
update:
iFFTsize    table    0, giFFTattributes1 + i(katt_table) - 1
ioverlap    table    1, giFFTattributes1 + i(katt_table) - 1
iwinsize    table    2, giFFTattributes1 + i(katt_table) - 1
iwintype    table    3, giFFTattributes1 + i(katt_table) - 1

input = p4

; this is to be overcome the difference in ComboBoxes behaviour on Unity, as explained above
input += giUnityCombobox


Sfile   = ""

if input = 0 then
    Sfile = gSfile1
elseif input = 1 then
    Sfile = gSfile1
elseif input = 2 then
    Sfile = gSfile2
elseif input = 3 then
    Sfile = gSfile3
elseif input = 4 then
    Sfile = gSfile4
elseif input = 5 then
    Sfile = gSfile5
else 
    Sfile = gSfile6
endif

prints "input: %f, fileName: %s\n", input, Sfile

aInL, aInR	diskin2 Sfile, chnget:k("speed"), 0, chnget:i("loop")
aLR = aInL+aInR

kEnv      transeg 1, i(gkDur), 0, 0
kLFO      lfo     kLFODepth,kLFORate,1

fSigLR	    pvsanal  aLR, iFFTsize, ioverlap, iwinsize, iwintype            
fBlur	    pvsblur  fSigLR, kBlurT + kLFO, 1				            	
aOut	    pvsynth  fBlur
								         		  
aMix       ntrpol       aOut, aLR, chnget:k("mix") 
           
aMix = aMix * chnget:k("masterLvl") * kEnv
            outs         aMix, aMix

    vincr gaRvb, aMix 

endin

instr Reverb                      
            denorm gaRvb
    aL, aR  reverbsc gaRvb, gaRvb, .8, 10000
            outs  aL*chnget:k("masterLvl")*chnget:k("verbLvl"), aR*chnget:k("masterLvl")*chnget:k("verbLvl")
            clear	gaRvb
endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
;starts instrument 1 and runs it for a week
i1 0 [60*60*24*7] 
i "Reverb" 0 [60*60*24*7]  
</CsScore>
</CsoundSynthesizer>