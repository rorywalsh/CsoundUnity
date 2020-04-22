<Cabbage>
form caption("Read soundfile"), size(300, 200)

</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d -m0d
</CsOptions>
<CsInstruments>

sr = 48000
ksmps = 32
nchnls = 2
0dbfs = 1.0


instr 1
print sr
prints p4 

;ibufsize = 64
;ar1, ar2 mp3in p4, 0, 0, 0, ibufsize
a1, a2 diskin2 p4, 1, 0, 1
outs a1, a2

endin

</CsInstruments>
<CsScore>

</CsScore>
</CsoundSynthesizer>
<bsbPanel>
 <label>Widgets</label>
 <objectName/>
 <x>100</x>
 <y>100</y>
 <width>320</width>
 <height>240</height>
 <visible>true</visible>
 <uuid/>
 <bgcolor mode="nobackground">
  <r>255</r>
  <g>255</g>
  <b>255</b>
 </bgcolor>
</bsbPanel>
<bsbPresets>
</bsbPresets>
