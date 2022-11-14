<Cabbage>
form caption("Sequencer"), size(300, 200)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d -m0d
</CsOptions>
<CsInstruments>
sr 	= 	44100 
ksmps 	= 	32
nchnls 	= 	2
0dbfs	=	1 

//sequences - right now they just indicate hits, but they could alsohold MIDI note numbers
giTable1 ftgen 1, 0, 16, 2, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0
giTable2 ftgen 2, 0, 16, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0
giTable3 ftgen 3, 0, 16, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
giTable4 ftgen 4, 0, 16, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
giTable5 ftgen 5, 0, 16, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
giTable6 ftgen 6, 0, 16, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
giTable7 ftgen 7, 0, 16, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
giTable8 ftgen 8, 0, 16, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0

gSSamples[] init 8
giTable[] init 8
  
instr SEQUENCER
    kBeat init 0
    kVoice = 0
    kBPM = abs(chnget:k("BPM"))
    
    if metro(kBPM/60) == 1 then
        chnset kBeat, "beatNumber"
        printk2 kBeat
        while kVoice < 8 do
            kValue tablekt kBeat, kVoice+1
            if(kValue == 1) then
                event "i", "PlaySample", 0, 2, kVoice
                prints "event i %d", kVoice
            endif
            kVoice += 1
        od
        kBeat = (kBeat < 15 ? kBeat+1 : 0)
    endif
endin

instr PlaySample
        
    giTable[0] chnget "sampletable900"
    giTable[1] chnget "sampletable901"
    giTable[2] chnget "sampletable902"
    giTable[3] chnget "sampletable903"
    giTable[4] chnget "sampletable904"
    giTable[5] chnget "sampletable905"
    giTable[6] chnget "sampletable906"
    giTable[7] chnget "sampletable907"
    
    ifn   = giTable[p4]
    ;prints "giTable p4 = %d, ifn = %d\n", p4, ifn
    ilen  =  nsamp(ifn)
    ;prints "actual numbers of samples = %d\n", ilen
    itrns =  1	; no transposition
    ilps  =  0	; loop starts at index 0
    ilpe  =  ilen	; ends at value returned by nsamp above
    imode =  1	; loops forward
    istrt =  0	; commence playback at index 0 samples
    ; lphasor provides index into f1 
    alphs lphasor itrns, ilps, ilpe, imode, istrt
    atab  tablei  alphs, ifn
    outs atab *.1, atab*.1
endin

instr UpdateSequencer
print p4, p5
    iColumn = p4
    iRow = p5
    iCurrentValue = table:i(iColumn, iRow)
    if iCurrentValue == 1 then
        tabw_i 0, iColumn, iRow
    else
        tabw_i 1, iColumn, iRow
    endif
endin


instr ClearSequencer
    iTable init 1
    iBeat init 0
    while iTable <= 8 do
        iBeat = 0
        while iBeat < 16 do
            tabw_i 0, iBeat, iTable
        iBeat += 1
        od
    iTable += 1
    od
endin


instr RandomSequencer
    iTable init 1
    iBeat init 0
    while iTable <= 8 do
        iBeat = 0
        while iBeat < 16 do
            tabw_i birnd(1) > .5 ? 1 : 0, iBeat, iTable
        iBeat += 1
        od
    iTable += 1
    od
endin


</CsInstruments>
<CsScore>
f0 z
i"SEQUENCER" 0 z
;i"RandomSequencer" 1 0
</CsScore>
</CsoundSynthesizer>