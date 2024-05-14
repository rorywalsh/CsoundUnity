<Cabbage>
form caption("AudioClipRead") size(400, 300), colour(58, 110, 182)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
; Initialize the global variables. 
ksmps = 32
nchnls = 2
0dbfs = 1

/* This UDO will read function tables created using the 
 CsoundUnity.CreateTable(). If the AudioClip is stereo, the table will 
 contain interleaved audio samples. */
opcode AudioClipRead, a[],i
setksmps 1
iTable xin
kCount init 0
iNumChannels tab_i 0, iTable
print iNumChannels
aOutArr[]  init iNumChannels
iNumSamples = ftlen(iTable)
if iNumChannels == 1 then
    aOutArr[0] tab 1+kCount, iTable
    kCount = (kCount<iNumSamples ? kCount+1 : 0)
elseif iNumChannels == 2 then
    aOutArr[0] tab 1+kCount, iTable
    aOutArr[1] tab 1+kCount+1, iTable
    kCount = (kCount<iNumSamples ? kCount+2 : 0)
endif

xout aOutArr
 
endop

/* This instrument, which uses the above UDO,
 will test AudioClip playback, stereo or mono */
instr 1
    iTableNumber = p4
    prints "Instr 1, Reading from (stereo or mono) table %d", iTableNumber
    iLen = ftlen(iTableNumber)
    prints "Instr 1, Printing table size: %d\n", iLen  
    print tab_i(0, iTableNumber)
    aSig[] AudioClipRead iTableNumber
    print lenarray:i(aSig)
    k1 downsamp aSig[0]

    if lenarray:i(aSig) == 1 then
        outs aSig[0], aSig[0] 
    else
        outs aSig[0], aSig[1]
    endif
endin

/* This instrument does simple playback of an AudioClip, but 
 only reads a single channel. This is the the default behaviour of
 CsoundUnity.CreateTable(), i.e, it will only write a single channel to 
 a function table. This is because Csound doesn't handle stereo function
 tables, without the use of a custom UDO as shown above */
instr 2
    iTableNumber = p4
    prints "Instr 2, Reading from mono table %d\n", iTableNumber
    iLen = ftlen(iTableNumber)
    prints "Instr 2, Printing table size: %d\n", iLen  
    aPhs phasor (sr/iLen)
    aFile table aPhs, iTableNumber, 1
    outs aFile, aFile
endin


</CsInstruments>
<CsScore>
f0 z
i1 1 2 9000
i2 3 4 9001
i1 7 2 9002
i2 9 4 9003
</CsScore>
</CsoundSynthesizer>
