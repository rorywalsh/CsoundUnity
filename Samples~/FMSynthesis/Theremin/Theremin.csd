<Cabbage> bounds(0, 0, 0, 0)
form caption("Theremin") size(700, 300), guiMode("queue"), pluginId("thm1")
rslider    bounds(  0, 40, 80, 80), valueTextBox(1), textBox(1), text("Att."), channel("Attack"),  range(0,  5, 0.15)
rslider    bounds(100, 40, 80, 80), valueTextBox(1), textBox(1), text("Gain"), channel("Gain"),  range(0,  1, 0.65)
rslider    bounds(200, 40, 80, 80), valueTextBox(1), textBox(1), text("Glide"), channel("Glide"),  range(0,  1, 0.15)
rslider    bounds(300, 40, 80, 80), valueTextBox(1), textBox(1), text("Lfo Freq"), channel("Lfo"),  range(0,  100, 7)
rslider    bounds(300, 40, 80, 80), valueTextBox(1), textBox(1), text("Lfo Amp"), channel("LfoAmp"),  range(0,  100, 8.5)
rslider    bounds(400, 40, 80, 80), valueTextBox(1), textBox(1), text("Filter Freq"), channel("FiltFreq"),  range(100,  5000, 1000)
rslider    bounds(500, 40, 80, 80), valueTextBox(1), textBox(1), text("Filter Res"), channel("FiltRes"),  range(0,  0.95, .066)
rslider    bounds(600, 40, 80, 80), valueTextBox(1), textBox(1), text("Table"), channel("Table"),  range(0,  3.99, 1.5)
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

chn_k "Frequency", 1
chn_k "Amplitude", 1 

;courtesy Iain McCurdy
opcode    lineto2,k,kk
 kinput,ktime    xin
 ktrig    changed    kinput,ktime    ; reset trigger
 if ktrig==1 then                    ; if new note has been received or if portamento time has been changed...
  reinit RESTART
 endif
 RESTART:                            ; restart 'linseg' envelope
 if i(ktime)==0 then                 ; 'linseg' fails if duration is zero...
  koutput    =    i(kinput)          ; ...in which case output simply equals input
 else
  koutput    linseg    i(koutput),i(ktime),i(kinput)    ; linseg envelope from old value to new value
 endif
 rireturn
         xout    koutput
endop


instr 1

kat chnget "Attack"
kport chnget "Glide"
kfreq chnget "Frequency"
kamp chnget "Amplitude"
klfo chnget "Lfo"
klfoamp chnget "LfoAmp"
kfiltf chnget "FiltFreq"
kfiltres chnget "FiltRes"
kgain chnget "Gain"
ktable chnget "Table"

kPortTime linseg  0, 0.001, 1
kEnvTime linseg 0, 0.001, 1

kcps     lineto2    kfreq, kPortTime * kport    
kenv     lineto2    kamp, kEnvTime * kat 
kff      lineto2    kfiltf, 0.01
kfr      lineto2    kfiltres, 0.01

alfo lfo klfoamp, klfo, 0
ftmorf ktable, 99, 100
aosc oscili kenv, kcps + alfo, 100
aout moogladder aosc, kff, kfr

outs aout*kgain, aout*kgain
endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 3600
f1 0 16384 10 1                                          ; Sine
f2 0 16384 10 1 0.5 0.3 0.25 0.2 0.167 0.14 0.125 .111   ; Sawtooth
f3 0 16384 10 1 0   0.3 0    0.2 0     0.14 0     .111   ; Square
f4 0 16384 10 1 1   1   1    0.7 0.5   0.3  0.1          ; Pulse
f5 0 16384 10 1 0.3 0.05 0.1 0.01                        ; Custom
f99  0 5 -2 1 2 3 4 5                                    ; the table that contains the numbers of tables used by ftmorf
f100 0 16384 10 1                                        ; the table that will be written by ftmorf
i1 0 3600
</CsScore>
</CsoundSynthesizer>

