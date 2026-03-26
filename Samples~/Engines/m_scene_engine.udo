/*
m_scene_engine - semi-physical model of an electric motor

DESCRIPTION
A semi-physical model of an electric motor, described by Andy Farnell in his
book: Designing Sound. This UDO is based on a Csound implementation of that
model by Rory Walsh.

SYNTAX
aout m_scene_engine itoprpm, icasingfreq, kspeed

INITIALIZATION
itoprpm - Top RPM/frequency of the engine in Hz
icasingfreq - Frequency of the engine housing/casing in Hz

PERFORMANCE
kspeed - Speed of the engine between 0 and 1, the value is save-guarded
	internally against instantaneous changes

CREDITS
Author: Jeanette C., based on a model by Andy Farnell and a Csound
implementation by Rory Walsh.
*/

opcode m_scene_engine, a,iik
	itoprpm, icasingfreq, kspeed xin

	; Create required ftables
	isine = ftgenonce(0, 0, 32768, 10, 1)

	; Set up motor speed/frequency
	kspeed = port:k(limit:k(kspeed, 0, 1), .1) ; avoid instantaneous speed change
	kfreqvar = randi:k(.01, ksmps) ; slight frequency/speed variation
	kfreqvar = port:k(kfreqvar, .1) ; smooth the variation
	krev = logcurve:k(kspeed, 2)

	; Make surethe engine spins completely down
	if (release() == 1) then
		iendrev = i(krev)
		krev = linsegr(iendrev, 1, .2, .5, 0, .2, 0)
	endif

	kfreq = krev * itoprpm + (itoprpm * kfreqvar)

	; Set up more basics for sound generation
	adrivephasor = phasor:a(kfreq)
	adrivephasormod = adrivephasor * adrivephasor
	adrivephasormod = adrivephasormod * adrivephasormod

	; Setup the rotor
	arotor init 0
	apink = fractalnoise(1, 1) ; Add a nosie component
	afiltpink = butterhp(apink, kfreq)
	arotor = ((arotor + .2) * adrivephasormod *.7) + (afiltpink *.02)
	arotornoise = arotor * afiltpink
	arotorout = butterlp((arotor*.99 + apink *.002), kfreq)

	; Set up the brushes
	anoise = fractalnoise(1, 1)
	abrush = butterbp(anoise, (kfreq * 8), (itoprpm * .5))
	abrushphasor = phasor:a(itoprpm)
	abrushphasor = abrushphasor * abrushphasor
	abrushphasor = abrushphasor * abrushphasor
	abrushsound = abrush * abrushphasor * krev
	abrushout = butterlp(abrushsound*krev, (itoprpm * 2))

	; Set up stator
	kstatorlfo = oscil:k(.1, 20*krev, -1)
	astator = oscil:k(krev+(kstatorlfo*krev), krev*itoprpm*4, -1, .5)
	astator = (astator * astator) * krev
	astatnoise = anoise * astator
	astatorout = (astator * .9 + astatnoise * .1)

	; Set up the casing/engine housing
	kdrivephasor = downsamp(adrivephasor)
	abuzz1 = buzz((kdrivephasor / 10), icasingfreq, 10, isine)
	abuzz2 = buzz((kdrivephasor / 10), (icasingfreq + .2), 10, isine)
	asubsonic = oscil:a(krev, icasingfreq, -1)
	isubsonicamp = 40 / (icasingfreq * icasingfreq)
	acasesound = (abuzz1 + abuzz2) * krev
	acase = lowpass2(acasesound, icasingfreq, (4000 / icasingfreq))

	aout = (arotorout * 10 + abrushout * .5 + astatorout * .5 + acase + asubsonic * isubsonicamp)

	; run that through a waveguide for better housing/casing impression
	awgout = dcblock2(wguide1(aout, icasingfreq, kfreq, .6))

	xout(awgout)
endop
