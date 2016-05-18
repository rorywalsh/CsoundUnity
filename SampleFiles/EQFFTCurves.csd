<Cabbage>
form caption("Write FFT data to table and create simple EQ"), size(300, 200)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d -m0d
;-odac
</CsOptions>
<CsInstruments>
sr 	= 	48000 
ksmps 	= 	32
nchnls 	= 	2
0dbfs	=	1 

;FFT table
giampFFT  ftgen 1001,0,1024,2,0

;instrument writes FFT table to a table, it writes 1024 bands, but we
;only access the lower bands in Unity. 
instr FFT2Table

	;create 64 band instruments. Each one can reduce energy
	;at the given centre frequency which is determined by the value
	;of iCnt. See EQ_BANDS instrument for further information. 
	iCnt = 0
	until iCnt>64 do
	event_i "i", "EQ_BANDS", 0, 3600, iCnt
	iCnt = iCnt+1
	enduntil

	gaLeft randi .5, 5000
	gaRight = gaLeft;
	ain = (gaLeft+gaRight)/2
	ifftsize = 1024
	ioverlap = ifftsize/4
	iwinsize = ifftsize
	iwintype = 1
	ain reverb ain, 1
	fsig  pvsanal ain, ifftsize, ioverlap, iwinsize, iwintype
	kflag  pvsftw fsig, giampFFT	 


endin 

;simple band-pass filter instrument. It reads data from a channel called
;bandNamp", where N is the band, and reduces energy at that spectrum by
;reducing the output volum. Roll off is is more prominant in the higher frequency
;bands 
instr EQ_BANDS
	SChannel sprintf "band%damp", p4
	kGain init 0
	kGain chnget SChannel
	kQ = 10

	asig reson (gaLeft+gaRight)/2, (p4*20), (p4*20)/kQ, 2

	kGain = kGain*((64-p4)/64)

	outs kGain*(asig/32), kGain*(asig/32)
endin

</CsInstruments>
<CsScore>
f0 z
</CsScore>
</CsoundSynthesizer>