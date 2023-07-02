<Cabbage>
form caption("Csound Haikus") size(900,90), colour(0,0,0) pluginId("CsHa")
button bounds(  5, 5, 80, 80), text("OFF") channel("OFF") latched(0) colour:0(50,50,50) colour:1(150,150,150) radioGroup(1)
button bounds( 95, 5, 80, 80), text("I") channel("I") latched(1) colour:0(50,50,50) colour:1(150,150,150) radioGroup(1)
button bounds(185, 5, 80, 80), text("II") channel("II") latched(1) colour:0(50,50,50) colour:1(150,150,150) radioGroup(1)
button bounds(275, 5, 80, 80), text("III") channel("III") latched(1) colour:0(50,50,50) colour:1(150,150,150) radioGroup(1)
button bounds(365, 5, 80, 80), text("IV") channel("IV") latched(1) colour:0(50,50,50) colour:1(150,150,150) radioGroup(1)
button bounds(455, 5, 80, 80), text("V") channel("V") latched(1) colour:0(50,50,50) colour:1(150,150,150) radioGroup(1)
button bounds(545, 5, 80, 80), text("VI") channel("VI") latched(1) colour:0(50,50,50) colour:1(150,150,150) radioGroup(1)
button bounds(635, 5, 80, 80), text("VII") channel("VII") latched(1) colour:0(50,50,50) colour:1(150,150,150) radioGroup(1)
button bounds(725, 5, 80, 80), text("VIII") channel("VIII") latched(1) colour:0(50,50,50) colour:1(150,150,150) radioGroup(1)
button bounds(815, 5, 80, 80), text("IX") channel("IX") latched(1) colour:0(50,50,50) colour:1(150,150,150) radioGroup(1)
</Cabbage>

<CsoundSynthesizer>

<CsOptions>
-odac -dm0
</CsOptions>

<CsInstruments>
sr               =                44100
ksmps            =                32
nchnls           =                2
0dbfs            =                1
                 seed             0
                 ;alwayson         "scheduleHaikus"
                 ;alwayson         "reverb"
gisine           ftgen            0, 0, 4096, 10, 1
gasendL,gasendR  init             0
gamixL,gamixR    init             0
gicos            ftgen            0,0,131072,11,1


                 instr            scheduleHaikus
gkOFF            chnget  "OFF"
gkI              chnget  "I" 
gkII             chnget  "II" 
gkIII            chnget  "III" 
gkIV             chnget  "IV" 
gkV              chnget  "V" 
gkVI             chnget  "VI" 
gkVII            chnget  "VII" 
gkVIII           chnget  "VIII" 
gkIX             chnget  "IX" 

printk2 gkI
printk2 gkII
printk2 gkIII

if trigger:k(gkOFF,0.5,0)==1 then
                 chnset 0, "I"
                 chnset 0, "II"
                 chnset 0, "III"
                 chnset 0, "IV"
                 chnset 0, "V"
                 chnset 0, "VI"
                 chnset 0, "VII"
                 chnset 0, "VIII"
                 chnset 0, "IX"
endif

; delay time before each haiku starts after the button has been pressed to prevent too much overlap
iStartDelay      =                1

; start haikus and set reverb settings...

; Haiku I
if trigger:k(gkI,0.5,0)==1 then 
                 event            "i","start_long_notes", iStartDelay, 0
gkFBL            =                0.85
gkFCO            =                10000
endif

; Haiku II
if trigger:k(gkII,0.5,0)==1 then
                 event            "i","start_sequences", iStartDelay, 60*60*24*7
                 event            "i","sound_output", iStartDelay, 60*60*24*7
gkFBL            =                0.75
gkFCO            =                4000
endif

; Haiku III
if trigger:k(gkIII,0.5,0)==1 then
                 event            "i","start_sequencesIII", iStartDelay, 60*60*24*7
                 event            "i","spatialise", iStartDelay, 60*60*24*7
gkFBL            =                0.95
gkFCO            =                10000
endif

; Haiku IV
if trigger:k(gkIV,0.5,0)==1 then
                 event            "i","trigger_notesIV", iStartDelay, 60*60*24*7
gkFBL            =                0.95
gkFCO            =                10000
endif

; Haiku V
if trigger:k(gkV,0.5,0)==1 then
                 event            "i","start_sequencesV", iStartDelay, 60*60*24*7
gkFBL            =                0.85
gkFCO            =                5000
endif

; Haiku VI
if trigger:k(gkVI,0.5,0)==1 then
                 event            "i","trigger_6_notes_and_plucks", iStartDelay, 60*60*24*7
gkFBL            =                0.85
gkFCO            =                10000
endif

; Haiku VII
if trigger:k(gkVII,0.5,0)==1 then
                 event            "i","trigger_sequenceVII", iStartDelay, 60*60*24*7
gkFBL            =                0.95
gkFCO            =                10000
endif

; Haiku VIII
if trigger:k(gkVIII,0.5,0)==1 then
                 event            "i","start_layers", iStartDelay, 0
gkFBL            =                0.75
gkFCO            =                10000
endif

; Haiku IX
if trigger:k(gkIX,0.5,0)==1 then
                 event            "i","trigger_arpeggio", iStartDelay, 60*60*24*7
gkFBL            =                0.88
gkFCO            =                10000
endif
                 endin









;; HAIKU I     
                 instr            start_long_notes
                 event_i          "i","trombone",0,60*60*24*7
                 event_i          "i","trombone",1,60*60*24*7
                 event_i          "i","trombone",2,60*60*24*7
                 event_i          "i","trombone",3,60*60*24*7
                 event_i          "i","trombone",4,60*60*24*7
                 event_i          "i","trombone",5,60*60*24*7
                 endin


                 instr            trombone
                 if               gkI==0 then
                 turnoff
                 endif
inote            random           54,66
knote            init             int(inote)
ktrig            metro            0.015
                 if ktrig==1 then
                 reinit           retrig
                 endif
retrig:
inote1           init             i(knote)
inote2           random           54, 66
inote2           =                int(inote2)
inotemid         =                inote1+((inote2-inote1)/2)
idur             random           22.5,27.5
icurve           =                2
                 timout           0,idur,skip
knote            transeg          inote1,idur/2,icurve,inotemid,idur/2,-icurve,inote2     
skip:
                 rireturn
kenv             linseg           0,25,0.05,p3-50,0.05,25,0
kdtn             jspline          0.05,0.4,0.8     
kmul             rspline          0.3,0.82,0.04,0.2
kamp             rspline          0.02,3,0.05,0.1
a1               gbuzz            kenv*kamp,cpsmidinn(knote)*semitone(kdtn),75,1,kmul^1.75,gicos
a1               *=               linsegr:a(1,0.1,0)
kpan             rspline          0,1,0.1,1
a1, a2           pan2             a1,kpan
                 outs             a1,a2
gasendL          =                gasendL+a1
gasendR          =                gasendR+a2
                 endin





;; HAIKU II
giampscl         ftgen            0, 0, 20000, -16, 1, 20, 0, 1, 19980, -5, 1
giwave1          ftgen            0,0,131073,9,  6,1,0,    9,1/10,0, 13,1/14,0, 17,1/18,0, 21,1/22,0,  25,1/26,0,  29,1/30,0,  33,1/34,0
giwave2          ftgen            0,0,131073,9,  7,1,0,   10,1/10,0, 14,1/14,0, 18,1/18,0, 22,1/22,0,  26,1/26,0,  30,1/30,0,  34,1/34,0
giwave3          ftgen            0,0,131073,9,  8,1,0,   11,1/10,0, 15,1/14,0, 19,1/18,0, 23,1/22,0,  27,1/26,0,  31,1/30,0,  35,1/34,0
giwave4          ftgen            0,0,131073,9,  9,1,0,   12,1/10,0, 16,1/14,0, 20,1/18,0, 24,1/22,0,  28,1/26,0,  32,1/30,0,  36,1/34,0
giwave5          ftgen            0,0,131073,9, 10,1,0,   13,1/10,0, 17,1/14,0, 21,1/18,0, 25,1/22,0,  29,1/26,0,  33,1/30,0,  37,1/34,0
giwave6          ftgen            0,0,131073,9, 11,1,0,   19,1/10,0, 28,1/14,0, 37,1/18,0, 46,1/22,0,  55,1/26,0,  64,1/30,0,  73,1/34,0
giwave7          ftgen            0,0,131073,9, 12,1/4,0, 25,1,0,    39,1/14,0, 63,1/18,0, 87,1/22,0, 111,1/26,0, 135,1/30,0, 159,1/34,0
giseq            ftgen            0,0,12,-2,      1, 1/3, 1/3, 1/3, 1, 1/3, 1/3, 1/3, 1/2, 1/2, 1/2, 1/2
gidurs           ftgen            0,0,100,-17,      0, 0.4, 50, 0.8, 90, 1.5
girescales       ftgen            0,0,-7,-2,6,7,8,9,10,11,12
                 instr            start_sequences
                 if               gkII==0 then
                 turnoff
                 endif
ktrig            metro            1/4
                 schedkwhennamed  ktrig, 0, 0, "play_sequence", 0, 48
                 endin

                 instr            play_sequence
                 if               gkII==0 then
                 turnoff
                 endif
itime_unit       random           2, 5
istart           random           0, 6
iloop            random           6, 13
ktrig_in         init             0
ktrig_out        seqtime          int(itime_unit)/3, int(istart), int(iloop), 0, giseq
inote            random           48, 100
ienvscl          =                ((1-(inote-48)/(100-48))*0.8)+0.2
ienvscl          limit            ienvscl,0.3,1
icps             =                cpsmidinn(int(inote))
ipan             random           0, 1
isend            random           0.3, 0.5
kamp             rspline          0.007, 0.6, 0.05, 0.2
kflam            random           0, 0.02
ifn              random           0, 7
                 schedkwhennamed  ktrig_out, 0, 0, "play_note", kflam, 0.01, icps, ipan, isend, kamp, int(ifn), ienvscl
                 endin

                 instr            play_note
idurndx          random           0, 100
p3               table            idurndx, gidurs                 
ijit             random           0.1, 1
acps             expseg           8000, 0.003, p4, 1, p4
aenv             expsega          0.001, 0.003, ijit^2, (p3-0.2-0.002)*p9, 0.002, 0.2, 0.001, 1, 0.001
adip             transeg          1, p3, 4, 0.99
iampscl          table            p4, giampscl
irescale         table            p8, girescales
idtn             random           0.995,1.005
a1               oscili           p7*aenv*iampscl, (acps*adip*idtn)/(6+irescale), giwave1+p8
adlt             rspline          1, 10, 0.1, 0.2
aramp            linseg           0, 0.02, 1
acho             vdelay           a1*aramp, adlt, 40
icf              random           0, 2
kcfenv           transeg          p4+(p4*icf^3), p9, -8, 1, 1, 0, 1
a1               tonex            a1, kcfenv
a1, a2           pan2             a1,p5
                 outs             a1,a2
gamixL           =                gamixL + a1
gamixR           =                gamixR + a2
gasendL          =                gasendL + (a1*(p6^2))
gasendR          =                gasendR + (a2*(p6^2))
                 endin

                 instr            sound_output
                 if               gkII==0 then
                 turnoff
                 endif
a1,a2            reverbsc         gamixL, gamixR, 0.01, 500
a1               =                a1*100
a2               =                a2*100
a1               atone            a1, 250
a2               atone            a2, 250
aEnv             expsegr          1, 2, 0.001
a1               *=               aEnv
a2               *=               aEnv
                 outs             a1, a2      
                 clear            gamixL, gamixR
                 endin



;; HAIKU III
giImpulseWave     ftgen            0,0,4097,10,1,1/2,1/4,1/8
gitims            ftgen            0, 0, 128, -7, 1, 100, 0.1
                  instr            start_sequencesIII
                  if               gkIII==0 then
                  turnoff
                  endif
krate             rspline          0, 1, 0.1, 2
krate             scale            krate^2,10,0.3
ktrig             metro            krate
koct              rspline          4.3, 9.5, 0.1, 1
kcps              =                cpsoct(koct)
kpan              rspline          0.1, 4, 0.1, 1
kamp              rspline          0.1, 1, 0.25, 2
kwgoct1           rspline          6, 9, 0.05, 1
kwgoct2           rspline          6, 9, 0.05, 1
                  schedkwhennamed  ktrig, 0, 0, "wguide2_note", 0, 4, kcps, kwgoct1, kwgoct2, kamp, kpan
                  endin

                  instr            wguide2_note
aenv              expon            1,10/p4,0.001
aimpulse          poscil           aenv-0.001,p4,giImpulseWave
ioct1             random           5, 11
ioct2             random           5, 11
aplk1             transeg          1+rnd(0.2), 0.1, -15, 1
aplk2             transeg          1+rnd(0.2), 0.1, -15, 1
idmptim           random           0.1, 3
kcutoff           expseg           20000, p3-idmptim, 20000, idmptim, 200, 1, 200
awg2              wguide2          aimpulse, cpsoct(p5)*aplk1, cpsoct(p6)*aplk2, kcutoff, kcutoff, 0.27, 0.23
aEnv              expsegr          1, 2, 0.001
awg2              *=               aEnv
awg2              dcblock2         awg2
arel              linseg           1, p3-idmptim, 1, idmptim, 0
awg2              =                awg2*arel
awg2              =                awg2/(rnd(4)+3)
aL,aR             pan2             awg2,p8
gasendL           =                gasendL+(aL*0.05)
gasendR           =                gasendR+(aR*0.05)
gamixL            =                gamixL+aL
gamixR            =                gamixR+aR
                  endin

                  instr            spatialise
if gkIII==0 then
 turnoff
endif
adlytim1          rspline          0.1, 5, 0.1, 0.4
adlytim2          rspline          0.1, 5, 0.1, 0.4
aL                vdelay           gamixL, adlytim1, 50
aR                vdelay           gamixR, adlytim2, 50
aEnv              expsegr          1, 2, 0.001
aL                *=               aEnv
aR                *=               aEnv
                  outs             aL, aR
gasendL           =                gasendL+(aL*0.05)
gasendR           =                gasendR+(aR*0.05)
                  clear            gamixL, gamixR
endin
                

                                
                                                                
; HAIKU IV
gioctfn           ftgen            0, 0, 4096, -19, 1, 0.5, 270, 0.5
ginotes           ftgen            0, 0, 100, -17, 0, 8.00, 10, 8.03, 15, 8.04, 25, 8.05, 50, 8.07, 60, 8.08, 73, 8.09, 82, 8.11
                  instr            trigger_notesIV
                  if               gkIV==0 then
                  turnoff
                  endif
krate             rspline          0.05, 0.12, 0.05, 0.1
ktrig             metro            krate
gktrans           trandom          ktrig,-1, 1
gktrans           =                semitone(gktrans)
idur              =                15
                  schedkwhen       ktrig, 0, 0, "hsboscil_note", rnd(2), idur
                  schedkwhen       ktrig, 0, 0, "hsboscil_note", rnd(2), idur
                  schedkwhen       ktrig, 0, 0, "hsboscil_note", rnd(2), idur
                  schedkwhen       ktrig, 0, 0, "hsboscil_note", rnd(2), idur
                  endin

                  instr            hsboscil_note
                  if               gkIV==0 then
                  turnoff
                  endif
ipch              table            int(rnd(100)),ginotes
icps              =                cpspch(ipch)*i(gktrans)*semitone(rnd(0.5)-0.25)
kamp              expseg           0.001,0.02,0.2,p3-0.01,0.001
ktonemoddep       jspline          0.01,0.05,0.2
ktonemodrte       jspline          6,0.1,0.2
ktone             oscil            ktonemoddep,ktonemodrte,gisine
kbrite            rspline          -2,3,0.0002,3
ibasfreq          init             icps
ioctcnt           init             2
iphs              init             0
a1                hsboscil         kamp, ktone, kbrite, ibasfreq, gisine, gioctfn, ioctcnt, iphs     
amod              oscil            1, ibasfreq*3.47, gisine
arm               =                a1*amod
kmix              expseg           0.001, 0.01, rnd(1), rnd(3)+0.3, 0.001
a1                ntrpol           a1, arm, kmix
a1                pareq            a1/10, 400, 15, .707
a1                tone             a1, 500
kpanrte           jspline          5, 0.05, 0.1
kpandep           jspline          0.9, 0.2, 0.4
kpan              oscil            kpandep, kpanrte, gisine
a1,a2             pan2             a1, kpan
a1                delay            a1, rnd(0.1)
a2                delay            a2, rnd(0.1)
kenv              linsegr          1, 1, 0
a1                =                a1*kenv
a2                =                a2*kenv
                  outs             a1, a2
gasendL           =                gasendL + a1/6
gasendR           =                gasendR + a2/6
                  endin



;; HAIKU V
                  instr            start_sequencesV
                  if               gkV==0 then
                  turnoff
                  endif
iBaseRate         random           1, 2.5
                  event_i          "i", "sound_instr",           0, 3600*24*7, iBaseRate, 0.9, 0.03, 0.06, 7, 0.5, 1
                  event_i          "i", "sound_instr", 1/(2*iBaseRate), 3600*24*7, iBaseRate, 0.9, 0.03, 0.06, 7, 0.5, 1
                  event_i          "i", "sound_instr", 1/(4*iBaseRate), 3600*24*7, iBaseRate, 0.9, 0.03, 0.06, 7, 0.5, 1
                  event_i          "i", "sound_instr", 3/(4*iBaseRate), 3600*24*7, iBaseRate, 0.9, 0.03, 0.06, 7, 0.5, 1
ktrig1            metro            iBaseRate/64
                  schedkwhennamed  ktrig1, 0, 0, "sound_instr", 1/iBaseRate, 64/iBaseRate, iBaseRate/16, 0.996, 0.003, 0.01, 3, 0.7, 1
                  schedkwhennamed  ktrig1, 0, 0, "sound_instr", 2/iBaseRate, 64/iBaseRate, iBaseRate/16, 0.996, 0.003, 0.01, 4, 0.7, 1
ktrig2            metro            iBaseRate/72
                  schedkwhennamed  ktrig2, 0, 0, "sound_instr", 3/iBaseRate, 72/iBaseRate, iBaseRate/20, 0.996, 0.003, 0.01, 5, 0.7, 1
                  schedkwhennamed  ktrig2, 0, 0, "sound_instr", 4/iBaseRate, 72/iBaseRate, iBaseRate/20, 0.996, 0.003, 0.01, 6, 0.7, 1
                  endin

                  instr            sound_instr
                  if               gkV==0 then
                  turnoff
                  endif
ktrig             metro            p4
                  if ktrig=1 then
                  reinit           PULSE
                  endif
PULSE:
ioct              random           7.3, 10.5
icps              init             cpsoct(ioct)
aptr              linseg           0, 1/icps, 1
                  rireturn
a1                tablei           aptr, gisine, 1
kamp              rspline          0.2, 0.7, 0.1, 0.8
a1                =                a1*(kamp^3)
kphsoct           rspline          6, 10, p6, p7
isep              random           0.5, 0.75
ksep              transeg          isep+1, 0.02, -50, isep
kfeedback         rspline          0.85, 0.99, 0.01, 0.1
aphs2             phaser2          a1, cpsoct(kphsoct), 0.3, p8, p10, isep, p5
iChoRate          random           0.5,2
aDlyMod           oscili           0.0005,iChoRate,gisine
acho              vdelay3          aphs2+a1, (aDlyMod+0.0005+0.0001)*1000,100
aphs2             sum              aphs2, acho
aphs2             butlp            aphs2, 1000
kenv              linsegr          1, p3-4, 1, 4, 0, 1, 0
kpan              rspline          0, 1, 0.1, 0.8
kattrel           linsegr          1, 1, 0
a1, a2            pan2             aphs2*kenv*p9*kattrel, kpan
a1                delay            a1, rnd(0.01)+0.0001
a2                delay            a2, rnd(0.01)+0.0001
ksend             rspline          0.2, 0.7, 0.05, 0.1
ksend             =                ksend^2
                  outs             a1*(1-ksend), a2*(1-ksend)
gasendL           =                gasendL+(a1*ksend)
gasendR           =                gasendR+(a2*ksend)
                  endin                    




;; HAIKU VI
                  instr            trigger_6_notes_and_plucks
                  if               gkVI==0 then
                  turnoff
                  endif
                  event_i          "i", "string", 0, 60*60*24*7, 40
                  event_i          "i", "string", 0, 60*60*24*7, 45
                  event_i          "i", "string", 0, 60*60*24*7, 50                                                                                  
                  event_i          "i", "string", 0, 60*60*24*7, 55        
                  event_i          "i", "string", 0, 60*60*24*7, 59
                  event_i          "i", "string", 0, 60*60*24*7, 64
krate             rspline          0.005, 0.15, 0.1, 0.2
ktrig             metro            krate
                  if ktrig==1 then
                  reinit           update
                  endif     
update:
aenv              expseg           0.0001, 0.02, 1, 0.2, 0.0001, 1, 0.0001
apluck            pinkish          aenv
                  rireturn     
koct              randomi          5, 10, 2
gapluck           butlp            apluck, cpsoct(koct)
                  endin

                  instr            string
                  if               gkVI==0 then
                  turnoff
                  endif
adlt              rspline          50, 250, 0.03, 0.06
apluck            vdelay3          gapluck, adlt, 500
adtn              jspline          15, 0.002, 0.02
astring           wguide1          apluck, cpsmidinn(p4)*semitone(adtn), 5000, 0.9995
aEnv              expsegr          1, 3, 0.001
astring           *=               aEnv
astring           dcblock          astring
kpan              rspline          0, 1, 0.1, 0.2
astrL, astrR      pan2             astring, kpan
                  outs             astrL, astrR
gasendL           =                gasendL+(astrL*0.6)
gasendR           =                gasendR+(astrR*0.6)
                  endin



;; HAIKU VII
giampsclVII       ftgen            0, 0, 20000, -16, 1, 20, 0, 1, 19980, -30, 0.1
giwave            ftgen            0, 0, 4097, 9, 3, 1, 0, 10,1/10,0, 18,1/14,0, 26,1/18,0, 34,1/22,0, 42,1/26,0, 50,1/30,0, 58,1/34,0
gicos             ftgen            0, 0, 131072, 11, 1
giseqVII          ftgen            0, 0, 12, -2, 3/2, 2, 3, 1, 1, 3/2, 1/2, 3/4, 5/2, 2/3, 2, 1
                  instr            trigger_sequenceVII
                  if               gkVII==0 then
                  turnoff
                  endif
ktrig             metro            0.2
                  schedkwhennamed  ktrig,0,0,"trigger_notesVII",0,30
kcrossfade        rspline          0, 1, 0.01, 0.1
gkcrossfade       =                kcrossfade^3
                  endin

                  instr            trigger_notesVII
                  if               gkVII==0 then
                  turnoff
                  endif
itime_unit        random           2, 10
istart            random           0, 6
iloop             random           6, 13
ktrig_out         seqtime          int(itime_unit), int(istart), int(iloop), 0, giseqVII
idur              random           8, 15
inote             random           0, 48
inote             =                (int(inote))+36
kSemiDrop         line             rnd(2), p3, -rnd(2)
kcps              =                cpsmidinn(inote+int(kSemiDrop))
ipan              random           0, 1
isend             random           0.05, 0.2
kflam             random           0, 0.02
kamp              rspline          0.008, 0.4, 0.05, 0.2
ioffset           random           -0.2, 0.2
kattlim           rspline          0, 1, 0.01, 0.1
                  schedkwhennamed  ktrig_out, 0, 0, "long_bell", kflam, idur, kcps*semitone(ioffset), ipan, isend, kamp
                  event_i          "i", "gbuzz_long_note", 0, 30, cpsmidinn(inote+19)
                  endin

                  instr            long_bell
                  if               gkVII==0 then
                  turnoff
                  endif
acps              transeg          1, p3, 3, 0.95
iattrnd           random           0, 1
iatt              =                (iattrnd>(p8^1.5)?0.002:p3/2)
aenv              expsegr          0.001, iatt, 1, p3-0.2-iatt, 0.002, 0.2, 0.001, 2, 0.001
aperc             expseg           10000, 0.003, p4, 1, p4
iampscl           table            p4, giampsclVII
ijit              random           0.5, 1
a1                oscili           p7*aenv*iampscl*ijit*(1-gkcrossfade), (acps*aperc                   )/2, giwave
a2                oscili           p7*aenv*iampscl*ijit*(1-gkcrossfade), (acps*aperc*semitone(rnd(.02)))/2, giwave
adlt              rspline          1, 5, 0.4, 0.8
acho              vdelay           a1, adlt, 40
a1                =                a1-acho
acho              vdelay           a2, adlt, 40
a2                =                a2-acho
icf               random           0, 1.75
icf               =                p4+(p4*(icf^3))
kcfenv            expseg           icf, 0.3, icf, p3-0.3, 20
a1                butlp            a1, kcfenv
a2                butlp            a2, kcfenv
a1                butlp            a1, kcfenv
a2                butlp            a2, kcfenv
                  outs             a1, a2
gasendL           =                gasendL+(a1*p6)
gasendR           =                gasendR+(a2*p6)
                  endin

                  instr            gbuzz_long_note
                  if               gkVII==0 then
                  turnoff
                  endif
kenv              expsegr          0.001, 3, 1, p3-3, 0.001, 2, 0.001
kmul              rspline          0.01, 0.1, 0.1, 1
kNseDep           rspline          0,1,0.2,0.4
kNse              jspline          kNseDep,50,100
agbuzz            gbuzz            gkcrossfade/80, p4/2*semitone(kNse), 5, 1, kmul*kenv, gicos
a1                delay            agbuzz, rnd(0.08)+0.001
a2                delay            agbuzz, rnd(0.08)+0.001
gasendL           =                gasendL+(a1*kenv)
gasendR           =                gasendR+(a2*kenv)
                  endin





;; HAIKU VIII
gigapsVIII        ftgen            0, 0, 100, -17, 0,32, 5,2, 45,1/2, 70,1/8, 90,2/9
gidursVIII        ftgen            0, 0, 100, -17, 0,0.4, 85,4
giwaveVIII        ftgen            0, 0, 131072, 10, 1, 0, 0, 0, 0.05
                  instr            start_layers
                  event_i          "i", "layer", 0, 3600*24*7
                  event_i          "i", "layer", 0, 3600*24*7
                  event_i          "i", "layer", 0, 3600*24*7
                  endin

                  instr            layer
                  if               gkVIII==0 then
                  turnoff
                  endif
kndx              randomh          0, 1, 1
kgap              table            kndx, gigapsVIII, 1
ktrig             metro            1/kgap
knote             randomh          0, 12, 0.1
kamp              rspline          0, 0.1, 1, 2
kpan              rspline          0.1, 0.9, 0.1, 1
kmul              rspline          0.1, 0.9, 0.1, 0.3
                  schedkwhen       ktrig, 0, 0, "note", rnd(0.1), 0.01, int(knote)*3, kamp, kpan, kmul
                  endin

                  instr            note
                  if               gkVIII==0 then
                  turnoff
                  endif
iratio            =                int(rnd(20))+1
p3                table            rnd(1), gidursVIII, 1
aenv              expsegr          1, p3, 0.001, 2, 0.001
aperc             expseg           5, 0.001, 1, 1, 1
iprob             random           0, 1
                  if iprob<=0.1    then
irange            random           -8, 4
icurve            random           -4, 4
abend             linseg           1, p3, semitone(irange)
aperc             =                aperc*abend
                  endif
kmul              expon            abs(p7), p3, 0.0001
a1                gbuzz            p5*aenv, cpsmidinn(p4)*iratio*aperc, int(rnd(500))+1, rnd(12)+1, kmul, giwaveVIII
iprob2            random           0,1
                  if iprob2<=0.2&&p3>1 then
kfshift           transeg          0, p3, -15, rnd(200)-100
ar,ai             hilbert          a1
asin              oscili           1, kfshift, gisine, 0
acos              oscili           1, kfshift, gisine, 0.25
amod1             =                ar*acos
amod2             =                ai*asin
a1                =                ((amod1-amod2)/3)+a1
                  endif
a1                butlp            a1, cpsoct(rnd(8)+4)
a1,a2             pan2             a1, p6
a1                delay            a1, rnd(0.03)+0.001
a2                delay            a2, rnd(0.03)+0.001
                  outs             a1, a2
gasendL           =                gasendL+a1*0.3
gasendR           =                gasendR+a2*0.3
                  endin     
                                





;; HAIKU IX
giwaveIX          ftgen            0, 0, 128, 10, 1, 1/4, 1/16, 1/64
giampsclIX        ftgen            0, 0, 20000, -16, 1, 20, 0, 1, 19980, -20, 0.01
                  instr            trigger_arpeggio
                  if               gkIX==0 then
                  turnoff
                  endif
krate             randomh          0.0005, 0.2, 0.04
ktrig             metro            krate
                  schedkwhennamed  ktrig, 0, 0, "arpeggio", 0, 25
                  endin

                  instr            arpeggio
                  if               gkIX==0 then
                  turnoff
                  endif
ibas              random           0, 24
ibas              =                cpsmidinn((int(ibas)*3)+24)
krate             rspline          0.1, 3, 0.3, 0.7
ktrig             metro            krate
kharm1            rspline          1, 14, 0.4, 0.8
kharm2            random           -3, 3
kharm             mirror           kharm1+kharm2, 1, 23
kamp              rspline          0, 0.05, 0.1, 0.2
                  schedkwhen       ktrig, 0, 0, "noteIX", 0, 4, ibas*int(kharm), kamp
                  endin

                  instr            noteIX
                  if               gkIX==0 then
                  turnoff
                  endif
aenv              linsegr          0, p3/2, 1, p3/2, 0, p3/2, 0  
iampscl           table            p4, giampsclIX
asig              oscili           p5*aenv*iampscl, p4, giwaveIX
adlt              rspline          0.01, 0.1, 0.2, 0.3
adelsig           vdelay           asig, adlt*1000, 0.1*1000
aL,aR             pan2             asig+adelsig, rnd(1)
                  outs             aL, aR
gasendL           =                gasendL+aL
gasendR           =                gasendR+aR
                  endin                                             



              

                  instr            reverb
kFBL              port             gkFBL, 0.5
kFCO              port             gkFCO, 0.5
aL, aR            reverbsc         gasendL,gasendR,kFBL,kFCO
                  outs             aL,aR
                  clear            gasendL, gasendR
                  endin




</CsInstruments>

<CsScore>
f 0 [60*60*24*7]
i"scheduleHaikus"   0   z
i"reverb"           0   z
</CsScore>

</CsoundSynthesizer>