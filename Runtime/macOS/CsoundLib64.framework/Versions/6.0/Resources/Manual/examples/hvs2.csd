<CsoundSynthesizer><CsOptions>; Select audio/midi flags here according to platform; Audio out   Audio in    No messages-odac           -iadc     -d     ;;;RT audio I/O</CsOptions><CsInstruments>
sr=44100ksmps=100nchnls=2
0dbfs = 1
ginumLinesX init	4ginumLinesY init	4ginumParms	init	3
giOutTab	ftgen	5,0,8, -2,      0giPosTab	ftgen	6,0,32, -2,     3,2,1,0,4,5,6,7,8,9,10, 11, 15, 14, 13, 12giSnapTab	ftgen	8,0,64, -2,     1,1,1,   2,0,0,  3,2,0,  2,2,2,  5,2,1,  2,3,4,  6,1,7,    0,0,0, \                              1,3,5,   3,4,4,  1,5,8,  1,1,5,  4,3,2,  3,4,5,  7,6,5,    7,8,9
tb0_init	giOutTab
        FLpanel	"Prova HVS2",600,400,10,100,0
gk1,    gk2,   ih1, ih2  FLjoy   "HVS controller XY", 0,    1,     1,     0,     0,     0,     -1,     -1,     300,    300,     0, 50 
; *ihandle,                      *numlinesX,   *numlinesY, *iwidth, *iheight, *ix, *iy,*image;gihandle	FLhvsBox	ginumLinesX,   ginumLinesY,  300,   300,      300,  50, 1
        FLpanel_end        FLrun

	instr	1
; Smooth control signals to avoid clickskx portk gk1, 0.02ky portk gk2, 0.02
;              kx,  ky,  inumParms,  inumlinesX,  inumlinesY,  iOutTab,  iPosTab,  iSnapTab [, iConfigTab]        hvs2  kx, ky, ginumParms, ginumLinesX, ginumLinesY, giOutTab, giPosTab, giSnapTab  ;, iConfigTab
;                       *kx, *ky, *ihandle;        FLhvsBoxSetValue gk1, gk2, gihandle
k0	init	0k1	init	1k2	init	2
printk2	tb0(k0)printk2	tb0(k1), 10printk2	tb0(k2), 20
  kris init 0.003  kdur init 0.02  kdec init 0.007
; Make parameters of synthesis depend on the table values produced by hvsares1 fof 0.2, tb0(k0)*100 + 50, tb0(k1)*100 + 200, 0, tb0(k2) * 10 + 50, 0.003, 0.02, 0.007, 20, \      1, 2, p3ares2 fof 0.2, tb0(k1)*100 + 50, tb0(k2)*100 + 200, 0, tb0(k0) * 10 + 50, 0.003, 0.02, 0.007, 20, \      1, 2, p3
outs ares1, ares2	endin
</CsInstruments><CsScore>f 1 0 1024 10 1  ;Sine wavef 2 0 1024 19 0.5 0.5 270 0.5  ;Grain envelope table
f0 3600
i1 0 3600
</CsScore></CsoundSynthesizer>