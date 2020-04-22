<CsoundSynthesizer>
<CsOptions>
-odac     ;;;realtime audio out
</CsOptions>
<CsInstruments>

/*

This is the example file for beosc

beosc
=====

Band-Enhanced oscillator, a port of Loris's oscillator
(based on Supercollider's BEOsc)

The band-enhanced family of opcodes (beosc, beadsynt) implement
sound modeling and synthesis that preserves the elegance and
malleability of a sinusoidal model, while accommodating sounds
with noisy (non-sinusoidal) components. Analysis is done offline,
with an enhanced McAulay-Quatieri (MQ) style analysis that extracts
bandwidth information in addition to the sinusoidal parameters for
each partial. To produce noisy components, we synthesize with sine
wave oscillators that have been modified to allow the introduction
of variable bandwidth.

aout beosc xfreq, kbw, ifn=-1, iphs=0, inoisetype=0

aout: the generated sound
afreq / kfreq: the frequency of the oscillator
kbw: the bandwidth of the oscillator. 0=pure sinusoidal
ifn: a table holding a sine wave (use -1 for the builtin wave used for oscili)
iphs: the phase of the sine (use unirand:i(6.28) to produce a random phase)
inoisetype: in the original implementation, gaussian (normal) noise is used,
            in supercollider's port, a simple uniform noise is used.
            We implement both. 0=uniform, 1=normal

There is no control for amplitude. The user is supossed to just multiply
the output aout by any factor needed.

NB: watch the output of this example with a freq. scope
*/

sr = 44100
ksmps = 64
nchnls = 1
0dbfs  = 1

instr 1
  idur1 = 8
  ifreq = 440
  kfreq linseg ifreq, idur1, ifreq, idur1, ifreq*4, idur1, ifreq
  kbw   cosseg     0, idur1, 1,     idur1, 1,       idur1, 0
  ;          freq   bw   fn  phs              noisetype(0=uniform)
  aout  beosc kfreq, kbw, -1, unirand:i(6.28), 0
  aenv  linsegr 0, 0.1, 1, 0.1, 1, 0.1, 0
  aout *= (aenv * 0.2)
  outch 1, aout
endin

</CsInstruments>
<CsScore>
i 1 0 32
</CsScore>
</CsoundSynthesizer> 
