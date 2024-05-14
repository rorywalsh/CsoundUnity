<Cabbage> bounds(0, 0, 0, 0)
form caption("Bouncing Ball by incontri-hannover") size(400, 300)
rslider bounds(0, 0, 77, 87) channel("gain") range(0, 2, 1, 1, 0.001)  valueTextBox(1) text("Gain") 
rslider bounds(80, 00, 77, 87) channel("carBaseFreq") range(10, 300, 80, 1, 0.001)  valueTextBox(1) text("Carrier Base Freq") 
rslider bounds(80, 100, 77, 87) channel("carDecayFreq") range(10, 300, 130, 1, 0.001)  valueTextBox(1) text("Carrier Decay Freq")
rslider bounds(0, 100, 77, 87) channel("modFreq") range(10, 300, 70, 1, 0.001)  valueTextBox(1) text("Modulator Freq")  
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d ;-odac -m128
</CsOptions>
<CsInstruments>
sr = 44100
ksmps = 32
nchnls = 2
0dbfs = 1

; Implementing Andy Farnell’s ‘bouncing ball’ in Csound 
; Marijana Janevska, James Anderson, Joachim Heintz  
; Incontri – Institute for contemporary music at the HMTM Hannover  
; janevskam@stud.hmtm-hannover.de 
; https://github.com/incontri-hannover/ICSC2022/tree/main/Examples

instr All_bounces
 kLine = linseg:k(p4, p3, p5)
 kMetroFreq = 1/(p3/10*kLine)
 kTrigger = metro(kMetroFreq)
  if  kTrigger == 1 then
   schedulek("One_bounce",0,p6,kLine,p7,p8)
  endif 
endin

;instr One_bounce
; iDecay = p4 //receives what is kLine in 'All_bounces'
; kMFRandom = random:k(1100, 2000)
; kMFRandom2 = random:k(1/5, 5)
; kModFreq = randomi:k(p5+1000, p5+kMFRandom, kMFRandom2)
; aMod = poscil:a(iDecay*70,kModFreq)
; aEnv = linseg:a(0,0.002,1,p3-0.002,0)^2
; aCarrFreq = 1+p6*iDecay*aEnv^2
; aCar = poscil:a(iDecay*0.2,aCarrFreq+aMod)
; outall(aCar*aEnv*0.5)
;endin

instr One_bounce
iDecay = p4 //receives what is kLine in 'All_bounces'
aMod = poscil:a(iDecay*chnget:i("modFreq"),120)
aEnv = linseg:a(0,0.002,1,p3-0.002,0)^2
aCarFreq = chnget:i("carBaseFreq")+chnget:i("carDecayFreq")*iDecay*aEnv^2
aCar = poscil:a(iDecay*0.2,aCarFreq+aMod)
outall(aCar*aEnv)
endin
</CsInstruments>
<CsScore>
;               s.t.  dur.  s.L.  e.L. 1bDur  MF    CF   
;i "All_bounces" 0     1.2   1     0    0.3    72    740 
;i "All_bounces" 2.3   7.4   1     0    3.4    40    70
;i "All_bounces" 9     6.2   0     1    4.6    30    500
;i "All_bounces" 17.3  1     0     1    0.5    29    62
;i "All_bounces" 21    9.3   0     1    5.6    63    760
;i "All_bounces" 21.8  5.3   0     1    4.5    32    95
;i "All_bounces" 24.8  9.6   0     1    1      4     1300
;i "All_bounces" 26.3  2.7   0     1    1.4    15    900
;i "All_bounces" 28.2  10.5  0     1    4      53    280
;i "All_bounces" 32.7  5.2   0     1    0.6    63    66
;i "All_bounces" 33.5  3.4   1     0    1.2    75    420
;i "All_bounces" 36.5  13    0     1    0.7    47    450
;i "All_bounces" 43.2  9.5   1     0    3.6    58    270
;i "All_bounces" 47.3  2     1     0    1.4    76    59
;i "All_bounces" 49    7.8   0     1    1.2    37    60
;i "All_bounces" 52.2  5.4   0     1    3.8    45    390
;i "All_bounces" 55.8  1.4   0     1    1.2    20   170
;i "All_bounces" 58    6.8   0     1    3.8    17    34
;i "All_bounces" 62.3  6.5   1     0    2.7    3     530
;i "All_bounces" 66    4     1     0    4      26    20
;i "All_bounces" 70    4.5   1     0    3.5    3     600
;i "All_bounces" 71.5  8.5   0     1    5.3    9     260
;i "All_bounces" 75    2.6   1     0    1      22    62
;i "All_bounces" 81.2  4.3   1     0    1.4    33    64
;i "All_bounces" 83.3  3     1     0    1.9    7     59
;i "All_bounces" 86.5  4.2   1     0    2.3    20    300
;i "All_bounces" 89    1     1     0    0.2    9     30
</CsScore>
</CsoundSynthesizer>