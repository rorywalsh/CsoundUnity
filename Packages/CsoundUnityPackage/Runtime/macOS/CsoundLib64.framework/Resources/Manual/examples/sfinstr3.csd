<CsoundSynthesizer> 
<CsOptions> 
; Select audio/midi flags here according to platform
-odac -+rtmidi=virtual -M0 ;;;realtime audio out
;-iadc    ;;;uncomment -iadc if realtime audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o sfinstr3.wav -W ;;; for file output any platform
</CsOptions> 
<CsInstruments> 

sr = 44100 
ksmps = 32
nchnls = 2
0dbfs  = 1 

gi24   ftgen 1, 0, 32, -2, 24, 2, 261.626, 60, 1, 1.0293022, 1.059463, 1.0905076, 1.1224619, 1.1553525, 1.1892069, \
             1.2240532, 1.2599207, 1.2968391, 1.33483924, 1.3739531, 1.414213, 1.4556525, 1.4983063, 1.54221, 1.5874001, \
             1.6339145, 1.6817917, 1.73107, 1.7817962, 1.8340067, 1.8877471, 1.9430623,  2 ;table for microtuning, a 24 tone equal temperament

giSF	sfload	"sf_GMbank.sf2" 
        sfilist giSF 

instr 1 

	mididefault	60, p3
	midinoteonkey	p4, p5
ikey	= p4
ivel	= p5
aenv    linsegr	1, 1, 1, 1, 0			;envelope
icps    cpstuni ikey, 1				;24 tones per octave
iamp    = 0.0002				;scale amplitude
iamp    = iamp * ivel * 1/128 			;make velocity-dependent
aL, aR	sfinstr3 ivel, ikey, iamp, icps, 180, giSF, 1 ;= Slap Bass 3
aL      = aL * aenv 
aR      = aR * aenv 
        outs aL, aR 

endin 
</CsInstruments> 
<CsScore> 
f0 60	;play for 60 seconds

i1 0 1 60 100 1	;using ftable 1
i1 + 1 62 <   .
i1 + 1 65 <   .
i1 + 1 69 40  .

e 
</CsScore> 
</CsoundSynthesizer> 