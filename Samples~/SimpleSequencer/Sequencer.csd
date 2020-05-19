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

gSSamples[] init 8;

instr SEQUENCER
    kBeat init 0
    kVoice = 0
    kBPM = abs(chnget:k("BPM"))

    gSSamples[0] chnget "sample1"
    gSSamples[1] chnget "sample2"
    gSSamples[2] chnget "sample3"
    gSSamples[3] chnget "sample4"
    gSSamples[4] chnget "sample5"
    gSSamples[5] chnget "sample6"
    gSSamples[6] chnget "sample7"
    gSSamples[7] chnget "sample8"

    
    if metro(kBPM/60) == 1 then
        chnset kBeat, "beatNumber"
        while kVoice < 8 do
            kValue tablekt kBeat, kVoice+1
            if(kValue == 1) then
                event "i", "PlaySample", 0, 2, kVoice
            endif
            kVoice += 1
        od
        kBeat = (kBeat < 15 ? kBeat+1 : 0)
    endif
endin

instr PlaySample
    a1, a2 diskin2 gSSamples[p4], 1, 0, 0
    outs a1*.1, a2*.1
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