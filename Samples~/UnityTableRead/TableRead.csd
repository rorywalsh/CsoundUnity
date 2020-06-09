<Cabbage>
form caption("Untitled") size(400, 300), colour(58, 110, 182), pluginid("def1")
keyboard bounds(8, 158, 381, 95)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>

</CsOptions>
<CsInstruments>
; Initialize the global variables. 
ksmps = 32
nchnls = 2
0dbfs = 1


opcode UnityTableRead, a[],i
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
    kCount = (kCount<iNumSamples/2 ? kCount+2 : 0)
endif

xout aOutArr
 
endop

instr 1
    print ftlen(9000)
    print tab_i(0, 9000)
    aSig[] UnityTableRead 9000
    print lenarray:i(aSig)
    k1 downsamp aSig[0]

    if lenarray:i(aSig) == 1 then
        outs aSig[0], aSig[0] 
    else
        outs aSig[0], aSig[1]
    endif
endin

</CsInstruments>
<CsScore>
f0 z
i1 2 2
</CsScore>
</CsoundSynthesizer>
