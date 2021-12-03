<Cabbage>
form caption("Untitled") size(400, 300), colour(58, 110, 182), pluginid("def1")
keyboard bounds(8, 158, 381, 95)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
sr = 48000
ksmps = 64
nchnls = 2
0dbfs = 1

/* This UDO will read function tables created using the 
 CsoundUnity.CreateTable(). If the AudioClip is stereo, the table will 
 contain intervleaved audio samples. */
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
    iTableNumber = 9000
    print ftlen(iTableNumber)
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
    iTableNumber = 9001
    prints "Printing table size"
    print ftlen(iTableNumber)
    iLen = ftlen(iTableNumber)
    aPhs phasor (sr/iLen)
    aFile table aPhs, iTableNumber, 1
    outs aFile, aFile
endin

instr 3
    prints "Reading file directly"
    a1, a2 diskin2 "C:\\Users\\rory\\sourcecode\\CsoundUnityPackage\\Assets\\Scenes\\UnityTableRead\\Resources\\Samples\\232009__danmitch3ll__xylophone-sweep.wav", 1, 0, 1
    outs a1, a2
endin
</CsInstruments>
<CsScore>
f0 z
i1 1 2
i2 3 4
i3 7 10
</CsScore>
</CsoundSynthesizer>
