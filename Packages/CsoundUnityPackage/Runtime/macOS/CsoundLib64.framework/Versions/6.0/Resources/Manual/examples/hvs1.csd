<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
; Audio out   Audio in    No messages
-odac           -iadc     -d     ;;;RT audio I/O
</CsOptions>
<CsInstruments>

sr=44100
ksmps=100
nchnls=2
0dbfs = 1

; Example by Gabriel Maldonado and Andres Cabrera

ginumLinesX init	16
ginumParms  init	3


giOutTab	ftgen	5,0,8, -2,      0
giPosTab	ftgen	6,0,32, -2,     3,2,1,0,4,5,6,7,8,9,10, 11, 15, 14, 13, 12
giSnapTab	ftgen	8,0,64, -2,     1,1,1,   2,0,0,  3,2,0,  2,2,2,  5,2,1,  2,3,4,  6,1,7,    0,0,0, \
                              1,3,5,   3,4,4,  1,5,8,  1,1,5,  4,3,2,  3,4,5,  7,6,5,    7,8,9
tb0_init	giOutTab

        FLpanel	"hsv1",500,100,10,10,0
gk1,ih1	FLslider "X", 0,1,  0,5, -1, 400,30, 50,20
        FLpanel_end
        FLrun

        instr	1
;               kx,   inumParms,  inumPointsX,  iOutTab,  iPosTab,  iSnapTab  [, iConfigTab] 
        hvs1    gk1,  ginumParms, ginumLinesX, giOutTab, giPosTab, giSnapTab  ;, iConfigTab

k0	init	0
k1	init	1
k2	init	2

printk2	tb0(k0)
printk2	tb0(k1), 10
printk2	tb0(k2), 20

aosc1 oscil tb0(k0)/20, tb0(k1)*100 + 200, 1
aosc2 oscil tb0(k1)/20, tb0(k2)*100 + 200, 1
aosc3 oscil tb0(k2)/20, tb0(k0)*100 + 200, 1
aosc4 oscil tb0(k1)/20, tb0(k0)*100 + 200, 1
aosc5 oscil tb0(k2)/20, tb0(k1)*100 + 200, 1
aosc6 oscil tb0(k0)/20, tb0(k2)*100 + 200, 1

outs aosc1 + aosc2 + aosc3, aosc4 + aosc5 + aosc6
	endin


</CsInstruments>
<CsScore>

f1 0 1024 10 1
f0 3600
i1 0 3600

</CsScore>
</CsoundSynthesizer>
