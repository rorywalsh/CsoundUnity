<CsoundSynthesizer>
<CsOptions>
-odac     ;;;realtime audio out
</CsOptions>
<CsInstruments>

/*

This is the example file for getrowlin

getrowlin
=========

  Given a 2D array (i- or k- array), or a table representing a
  2D matrix, get a row of this matrix (possibly a slice). If krow is
  not an integer, the values are the result of the interpolation
  between the two adjacent rows.

  Assuming such a 2D matrix containing multiple rows of sampled streams
  (for instance, the amplitudes of a set of oscillators, sampled at a
  regular interval), this opcode extracts one row of that data with
  linear interpolation between adjacent rows and places the result in
  a 1D array

  NB: The destination array (the left hand term) does not
      need to be previously initialized

  NB2: if the destination array is too small to fit the data, it will
       be enlarged

Syntax
======

kOut[] getrowlin kMtrx[], krow, kstart=0, kend=0, kstep=1
kOut[] getrowlin krow, ifn, inumcols, iskip=0, istart=0, iend=0, istep=1

      
kMtrx[]  : a 2D array
krow     : the row to read (can be a fractional number, in which case interpolation
           with the next row is performed)
kstart   : start index to read from the row 
kend     : end index to read from the row (not inclusive)
kstep    : step used to read the along the row
iskip    : in the case of using a table as input, iskip indicates the start of the
           sampled data (skipping a possible header in the data)
inumcols : in the case of using a table as input, inumcols indicates the number of
           columns of the 2D matrix.
      
*/

sr = 44100
ksmps = 128
nchnls = 1
0dbfs  = 1

; just a simple test of the bare functionality
instr 1
  ; make a 4x3 array
  kMtx[] init 3, 4
  kMtx fillarray  0,  1,  2,  3,   \
                 10, 11, 12, 13,   \
                 20, 21, 22, 23
  krow linseg 0, p3, 2
  printk2 krow, 20
  kOut[] getrowlin kMtx, krow
  printarray kOut, -1, "", "kOut"

  ; the same, but use cosine interpolation
  krow0 = floor(krow)
  krow1 = krow0 + 1
  krowcos = lincos(krow, krow0, krow1, krow0, krow1)
  kOut2[] getrowlin kMtx, krowcos
  printarray kOut2, -1, "", "kOut2"
endin

; simplified example taken from beadsynt, to show a practical use case
; this uses the file fox.mtx.wav, which is not a real soundfile, but
; a 2D matrix saved as a soundfile, generated with loristrck_pack
; (see https://github.com/gesellkammer/loristrck)      
instr 2
  ifn ftgentmp 0,0,0,-1, "fox.mtx.wav", 0, 0, 0
  ; ifn is organized as follow:
  ; header: iskip, idt, inumcols, inumrows, itimestart
  ; multiple rows of the form f0, a0, bw0, f1, a1, bw1, ...    
  iskip      tab_i 0, ifn
  idt        tab_i 1, ifn
  inumcols   tab_i 2, ifn
  inumrows   tab_i 3, ifn
  itimestart tab_i 4, ifn
  inumpartials = inumcols / 3
  imaxrow = inumrows - 2
  idur = inumrows * idt
  kfreqscale cosseg 1, idur, 2  
  kplayhead  phasor (1/idur)*idur
  krow = limit(kplayhead / idt, 0, imaxrow)
  ;                                              start end step  
  kFreqs[] getrowlin krow, ifn, inumcols, iskip, 0,    0,  3
  kAmps[]  getrowlin krow, ifn, inumcols, iskip, 1,    0,  3
  kBws[]   getrowlin krow, ifn, inumcols, iskip, 2,    0,  3
  aout beadsynt kFreqs, kAmps, kBws, -1, 0, kfreqscale
  outch 1, aout
  if(timeinsts() > idur) then
    event "e", 0, 0, 0
  endif
endin
 
</CsInstruments>
<CsScore>
i 1 0 2
i 2 2 3600
</CsScore>
</CsoundSynthesizer> 
