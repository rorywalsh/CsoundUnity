<Cabbage>
form caption("Basic FM Synth") size(600, 600), guiMode("queue"), pluginId("fms1")
rslider bounds(10, 10, 100, 100) valueTextBox(1), textBox(1), range(0, 2.0, .6, 1, 0.001) text("Gain") channel("gain")
rslider bounds(110, 10, 100, 100) valueTextBox(1), textBox(1), range(0, 1.0, .3, 1, 0.001) text("Max Amp") channel("max_amp")
rslider bounds(210, 10, 100, 100) valueTextBox(1), textBox(1), range(0, 2.0, .16, 1, 0.001) text("Low Index") channel("low_index")
rslider bounds(310, 10, 100, 100) valueTextBox(1), textBox(1), range(0, 2.0, .1, 1, 0.001) text("Diff Index") channel("index_diff")
rslider bounds(10, 110, 100, 100) valueTextBox(1), textBox(1), range(0, 2000, 110, 1, 0.001) text("Car Freq") channel("car_freq")
rslider bounds(110, 110, 100, 100) valueTextBox(1), textBox(1), range(0, 2000, 10, 1, 0.001) text("Mod Freq") channel("mod_freq")
rslider bounds(210, 110, 100, 100), valueTextBox(1), textBox(1), text("Car Table"), channel("car_table"),  range(0,  3.99, 2.5)
rslider bounds(310, 110, 100, 100), valueTextBox(1), textBox(1), text("Mod Table"), channel("mod_table"),  range(0,  3.99, 1.1)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

sr = 48000
ksmps = 32
nchnls = 2
0dbfs = 1

instr 1

kgainl init 0

kgain chnget "gain"
kmaxamp chnget "max_amp"

klowndx chnget "low_index"
kndxdiff chnget "index_diff"

kcarfreq chnget "car_freq"
kmodfreq chnget "mod_freq"

kcartab chnget "car_table"
kmodtab chnget "mod_table"

kgain lineto kgain, 0.01
kmaxamp lineto kmaxamp, 0.01
klowndx lineto klowndx, 0.01
kndxdiff lineto kndxdiff, 0.01
kcarfreq lineto kcarfreq, 0.01
kmodfreq lineto kmodfreq, 0.01
kcartab lineto kcartab, 0.01
kmodtab lineto kmodtab, 0.01

ftmorf kcartab, 99, 100
ftmorf kmodtab, 99, 101

amodosc oscili (klowndx + kndxdiff) * kmodfreq, kmodfreq, 101 
acarosc oscili kmaxamp, kcarfreq + amodosc, 100

outs acarosc * kgain, acarosc * kgain

endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
f1 0 16384 10 1                                          ; Sine
f2 0 16384 10 1 0.5 0.3 0.25 0.2 0.167 0.14 0.125 .111   ; Sawtooth
f3 0 16384 10 1 0   0.3 0    0.2 0     0.14 0     .111   ; Square
f4 0 16384 10 1 1   1   1    0.7 0.5   0.3  0.1          ; Pulse
f5 0 16384 10 1 0.3 0.05 0.1 0.01                        ; Custom
f99  0 5 -2 1 2 3 4 5                                    ; the table that contains the numbers of tables used by ftmorf
f100 0 16384 10 1                                        ; the table that will be written by car ftmorf
f101 0 16384 10 1                                        ; the table that will be written by mod ftmorf
i1 0 3600
</CsScore>
</CsoundSynthesizer>
