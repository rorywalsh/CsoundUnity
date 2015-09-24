<CsoundSynthesizer>
<CsOptions>
-odac -b64 -m0d
</CsOptions>
<CsInstruments>
sr 	= 	44100 
ksmps 	= 	32
nchnls 	= 	2
0dbfs	=	1 

;include some utility instruments
#include "UtilityInstruments.orc_"

instr 1
;add code here
endin

</CsInstruments>
<CsScore>
f0 [60*60*24*7]
;i5867.0 0 -1 "C:/Users/rory/Documents/CsoundInside/Assets/Audio/loop_1.wav" "loop1"
;i5867.1 0 -1 "C:/Users/rory/Documents/CsoundInside/Assets/Audio/loop_2.wav" "loop2"
;i5867.2 0 -1 "C:/Users/rory/Documents/CsoundInside/Assets/Audio/loop_3.wav" "loop3"
;i5867.3 0 -1 "C:/Users/rory/Documents/CsoundInside/Assets/Audio/loop_4.wav" "loop4"
;i5867.4 0 -1 "C:/Users/rory/Documents/CsoundInside/Assets/Audio/loop_5.wav" "loop5"
</CsScore>
</CsoundSynthesizer>