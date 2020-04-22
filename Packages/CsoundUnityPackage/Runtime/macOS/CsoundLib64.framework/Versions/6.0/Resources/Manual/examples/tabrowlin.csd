<CsoundSynthesizer>
<CsOptions>
-odac     ;;;realtime audio out
</CsOptions>
<CsInstruments>

/*

This is the example file for tabrowlin

tabrowlin
=========

  This opcode assumes the use of a table, which is a simple 1D array,
  to hold a 2D matrix with a given row length.
  Assuming such a 2D table containing multiple rows of sampled streams
  (for instance, the amplitudes of a set of oscilators, sampled at a
  regular interval), this opcode can extract one row of that data with
  linear interpolation between adjacent rows (if row is not a whole number)
  and place the result in another table

Syntax
======

tabrowlin krow, ifnsrc, ifndest, inumcols, ioffset=0, istart=0, iend=0, istep=1


krow     : the row to read (can be a fractional number, in which case interpolation
           with the next row is performed)
ifnsrc   : index of the source table
ifndest  : index of the dest table
inumcols : the number of columns a row has, in the source table
ioffset  : an offset to where the data starts (used to skip a header, if present)
istart   : start index to read from the row (not the start index of the table)
iend     : end index to read from the row (not inclusive)
istep    : step used to read the along the row

If reading out of bounds a PerformanceError will be raised. Because we
interpolate between rows, the last row that can be read is

  maxrow = (ftlen(ifnsrc)-ioffset)/inumcols - 2

*/

sr = 44100
ksmps = 128
nchnls = 1
0dbfs  = 1

instr 1
  ; just a simple test of the bare functionality
  ; generate a 4x3 table
  isource ftgentmp 0, 0, -12, -2, \
       0,  1,  2,  3,   \
      10, 11, 12, 13,   \
      20, 21, 22, 23
  ; create an empty table able to hold one row (4 elements)
  idest ftgentmp 0, 0, -4, -2, 0
  print ftlen(isource)
  ; we exceed the max. row to show what happens (the row is clipped
  ; to the max row possible and a message is printed to show the error)
  krow linseg 0, p3, 2.05  
  printk2 krow, 20
  tabrowlin krow, isource, idest, 4
  ftprint idest, -1
endin

</CsInstruments>
<CsScore>
i 1 0 2
</CsScore>
</CsoundSynthesizer> 
