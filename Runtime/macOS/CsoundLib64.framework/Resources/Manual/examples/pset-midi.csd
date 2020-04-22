<CsoundSynthesizer> 
<CsOptions> 
; Select audio/midi flags here according to platform
-odac -+rtmidi=virtual -M0 --midi-key-oct=4 --midi-velocity=5   ;;;realtime audio out and virtual midi keyboard
;-iadc    ;;;uncomment -iadc if realtime audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o pset-midi.wav -W ;;; for file output any platform
</CsOptions> 
<CsInstruments> 

sr = 44100 
ksmps = 32
0dbfs  = 1 
nchnls = 2 

instr 1

            pset 0, 0, 3600, 0, 0, 0
iinstrument = p1
istarttime  = p2
iattack     = 0.005
isustain    = p3
irelease    = 0.06
p3          = isustain + iattack + irelease
kdamping    linsegr 0.0, iattack, 1.0, isustain, 1.0, irelease, 0.0

ioctave     = p4
ifrequency  = cpsoct(ioctave)
iamplitude  = p5*.15			;lower volume

print p1, p2, p3, p4, p5
asig STKBandedWG ifrequency, iamplitude
     outs asig, asig

endin
</CsInstruments>
<CsScore>
f 0 60	; runs 69 seconds
e
</CsScore>
</CsoundSynthesizer>
