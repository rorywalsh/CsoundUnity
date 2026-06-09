<CsoundSynthesizer>
<CsOptions>
-d -M0
</CsOptions>
<CsInstruments>
0dbfs = 1
ksmps = 32
nchnls = 2

instr 1
  ifreq  cpsmidi
  iamp   ampmidi 0.5
  aenv   linsegr 0, 0.01, 1, 0.1, 0.7, 0.5, 0
  a1     oscili iamp * aenv, ifreq
  out    a1, a1
endin
</CsInstruments>
<CsScore>
f0 z
</CsScore>
</CsoundSynthesizer>
