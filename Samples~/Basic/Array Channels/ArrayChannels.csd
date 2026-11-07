<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>
ksmps = 32
nchnls = 2
0dbfs = 1

giVectorShape[] fillarray 4
giMatrixShape[] fillarray 2, 3
giSmallShape[] fillarray 2
giTextShape[] fillarray 2, 2
chnarray "audioBus", 2, "a", giSmallShape
chnarray "hostAudio", 3, "a", giSmallShape
chnarray "hostS", 3, "S", giTextShape
chnarray "echoS", 2, "S", giTextShape
chnarray "hostK", 3, "k", giVectorShape
chnarray "echoK", 2, "k", giVectorShape
chnarray "hostI", 3, "i", giVectorShape
chnarray "echoI", 2, "i", giVectorShape
chnarray "matrix", 3, "k", giMatrixShape
chnarray "matrixEcho", 2, "k", giMatrixShape
chnarray "changing", 2, "k", giSmallShape

giSine ftgen 0, 0, 4096, 10, 1

instr 1
    SText[][] chnget "hostS"
    chnset SText, "echoS"
    kFreqs[] chnget "hostK"
    chnset kFreqs, "echoK"
    kGain chnget "gain"
    kGain portk kGain, 0.02
    kF0 portk max(kFreqs[0], 20), 0.02
    kF1 portk max(kFreqs[1], 20), 0.02
    kF2 portk max(kFreqs[2], 20), 0.02
    kF3 portk max(kFreqs[3], 20), 0.02
    a0 oscili 0.1, kF0, giSine
    a1 oscili 0.1, kF1, giSine
    a2 oscili 0.1, kF2, giSine
    a3 oscili 0.1, kF3, giSine
    aMix = (a0 + a1 + a2 + a3) * kGain
    outs aMix, aMix

    aBus[] init 2
    kAudioLeft chnget "audioLeftHz"
    kAudioRight chnget "audioRightHz"
    kAudioLeft portk max(kAudioLeft, 20), 0.02
    kAudioRight portk max(kAudioRight, 20), 0.02
    aBus[0] oscili 0.1, kAudioLeft, giSine
    aBus[1] oscili 0.1, kAudioRight, giSine
    chnset aBus, "audioBus"
    aReturn[] chnget "hostAudio"
    kAudioGain chnget "audioGain"
    kAudioGain portk kAudioGain, 0.02
    outs aReturn[0] * kAudioGain, aReturn[1] * kAudioGain

    kMatrix[][] chnget "matrix"
    kDoubled[][] init 2, 3
    kRow = 0
    while kRow < 2 do
        kColumn = 0
        while kColumn < 3 do
            kDoubled[kRow][kColumn] = kMatrix[kRow][kColumn] * 2
            kColumn += 1
        od
        kRow += 1
    od
    chnset kDoubled, "matrixEcho"

    ; Resizing uses the synchronized producer copy, never a runtime chnarray redeclaration.
    kSmall[] fillarray 101, 102
    kLarge[] fillarray 201, 202, 203, 204, 205, 206, 207
    kSize chnget "producerSize"
    if kSize > 2 then
        chnset kLarge, "changing"
    else
        chnset kSmall, "changing"
    endif
endin

instr 2
    iFreqs[] chnget "hostI"
    chnset iFreqs, "echoI"
    aEnvelope linen 1, 0.01, p3, 0.05
    a0 oscili 0.05, iFreqs[0], giSine
    a1 oscili 0.05, iFreqs[1], giSine
    a2 oscili 0.05, iFreqs[2], giSine
    a3 oscili 0.05, iFreqs[3], giSine
    aMix = (a0 + a1 + a2 + a3) * aEnvelope
    outs aMix, aMix
endin
</CsInstruments>
<CsScore>
f0 z
i 1 0 -1
</CsScore>
</CsoundSynthesizer>
