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

/* This UDO reads multichannel function tables created by
 CsoundUnity.CreateTable() with interleaved samples. The table layout is:
   index 0           : channel count
   index 1..ftlen-1  : interleaved audio samples (L0,R0,L1,R1,... for stereo) */
opcode AudioClipRead, a[],i
iTable xin
iNumChannels tab_i 0, iTable
aOutArr[]  init iNumChannels
iNumSamples = ftlen(iTable)
iFrames = (iNumSamples - 1) / iNumChannels

; The samples were recorded at the clip's rate, not at sr, and a table carries no rate of its
; own: the C# side sets it on a channel named after the table. Reading at sr instead would play
; the clip at the ratio between the two rates.
Schan sprintf "table%dRate", iTable
iRate chnget Schan
if iRate <= 0 then
    iRate = sr
endif

; Audio-rate frame index via phasor — advances by 1 per clip sample,
; wraps to 0 at end of file.
aPhs phasor iRate/iFrames                    ; 0 <= aPhs < 1 over iFrames samples
aFrame = int(aPhs * iFrames)                 ; integer frame in [0, iFrames-1]

; Loop generalised over iNumChannels — works for mono, stereo, 5.1, 7.1, etc.
; `tab` is unchecked but aFrame is bounded so the read index
; 1 + aFrame*iNumChannels + kc stays within [1, iNumSamples-1].
kc = 0
while kc < iNumChannels do
    aOutArr[kc] tab 1 + aFrame*iNumChannels + kc, iTable
    kc += 1
od

xout aOutArr
endop

/* This instrument, which uses the above UDO,
 will test AudioClip playback, stereo or mono */
instr 1
    iTableNumber = p4
    prints "Instr 1, Reading from (stereo or mono) table %d\n", iTableNumber
    iLen = ftlen(iTableNumber)
    prints "Instr 1, Printing table size: %d\n", iLen
    aSig[] AudioClipRead iTableNumber

    if lenarray:i(aSig) == 1 then
        outs aSig[0], aSig[0]
    else
        outs aSig[0], aSig[1]
    endif
endin

/* This instrument does simple playback of an AudioClip, but 
 only reads a single channel. This is the the default behaviour of
 CsoundUnity.CreateTable(), i.e, it will only write a single channel to 
 a function table. This is because Csound doesn't handle multichannel function
 tables, without the use of a custom UDO as shown above */
instr 2
    iTableNumber = p4
    prints "Instr 2, Reading from mono table %d\n", iTableNumber
    iLen = ftlen(iTableNumber)
    prints "Instr 2, Printing table size: %d\n", iLen  
    Schan sprintf "table%dRate", iTableNumber
    iRate chnget Schan
    if iRate <= 0 then
        iRate = sr
    endif
    aPhs phasor (iRate/iLen)
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
