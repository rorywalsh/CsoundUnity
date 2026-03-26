<CsoundSynthesizer>
<CsOptions>
-o dac -d --messagelevel=2
</CsOptions>
; ==============================================
<CsInstruments>

sr	=	48000
ksmps	=	1
nchnls	=	2
0dbfs	=	1

gkSpeed chnexport "Speed", 1  ; input channel: Unity writes, Csound reads via chnget("Speed")
;#include "m_scene_boat_engine.udo"
;#include "m_scene_car_engine1.udo"
;#include "m_scene_car_engine2.udo"
;#include "m_scene_car_indicator.udo"
;#include "m_scene_engine.udo"
/*
m_scene_boat-Engine - a model of a boat/ship engine

DESCRIPTION
This is a model of a boat or ship engine with one to three cylinders. It is
heavily based on a work by Oeyvind Brandtsegg. It looks like a semi-physical
model.

SYNTAX
aOut m_scene_boat_engine kSpeed, kVolume, iBrightness, iNumberOfCylinders

INITIALIZATION
iBrightness - Brightness of the engine, this also depends on the speed,
	between 0 and 1
iNumberOfCylinders - The number of cylinders in the engine.
	<=1 - one cylinder
	<=2 - two cylinders
	>2 - three cylinders

PERFORMANCE
aOut - The sound of the engine
kSpeed - Speed, between 0 and 1 (translates to 4 to 64 RPM/Hz), with portamento
	to avoid instantaneous changes
kVolume - Volume of the engine, usually between 0 and 1

CREDITS
A modification by Jeanette C. from a CSD by Oeyvind Brandtsegg.
The original can be found here:
http://oeyvind.teks.no/CsFiles/engine.csd
*/

opcode m_scene_boat_engine, a, kkii
	kSpeed, kVolume, iBrightness, iNumberOfCylinders xin

	; Set up required function tables
	iAmpFunction    ftgen 0, 0, 1024, 7, 1, 4, 1, 508, .3, 510, .1, 1, 0, 1, 0
	iFilterFunction ftgen 0, 0, 1024, 7, 1, 70, .1, 10, .8, 50, .3, 270, .2, 612, 0

	; Initialise variables
	iBrightness = limit:i(iBrightness, 0, 1)
	kSpeed = port:k(limit:k(kSpeed, 0, 1), .1)
	kLowpassFreq = linlin:k(iBrightness, 35, 400)
	kLowpassFreq *= (1 + kSpeed)
	kRPM = linlin:k(kSpeed, 4, 64)

	; Setup filter and volume envelopes/oscillations
	kAmpEnv = oscil:k(kVolume, kRPM, iAmpFunction)
	kFilterEnv = oscil:k(kVolume, kRPM, iFilterFunction)
	kLowpassFreq += kFilterEnv * 200

	; Produce the audio and filter it
	aNoise = noise(kAmpEnv, 0)
	aLowpass = rezzy(aNoise, kLowpassFreq, 3)
	aBass = butterlp(aLowpass, 50)
	aTreble = butterlp(aLowpass, (100 + kSpeed * 100))

	; Sum filtered components to raw sound and reverberate
	aRaw = ((aLowpass * .2) + aBass + aTreble)
	aCylinders = aRaw

	; Create delayed versions for more cylinders
	if (iNumberOfCylinders > 1) then
		kDelayTime1 = (240 / kRPM)
		aDelayTime1 = interp(kDelayTime1)
		aDelayTime1 = tone(aDelayTime1, 2)
		aDelay1 = vdelay3(aRaw, aDelayTime1, 10000)
		aCylinders += aDelay1
	endif

	if (iNumberOfCylinders > 2) then
		kDelayTime2 = (520 / kRPM)
		aDelayTime2 = interp(kDelayTime2)
		aDelayTime2 = tone(aDelayTime2, 2)
		aDelay2 = vdelay3(aRaw, aDelayTime2, 10000)
		aCylinders += aDelay2
	endif

	aReverb = dcblock2(wguide1(aCylinders*.3, 50*(1 + kSpeed), 1000, .4))
	aOut = (aRaw + aReverb)*.8

	; Output audio
	xout(aOut * 1.4)
endop

/*
m_scene_car_engine1 - A simplistic car engine effect (not too realistic)

DESCRIPTION
m_scene_car_engine1 is a rough sound-alike of a petrol or diesel engine. It
does not sound overly realistic, but quite nice.

SYNTAX
aOut m_scene_car_engine1 kSpeed, iResonance, iBrightness

INITIALIZATION
iResonance - Resonant quality of the sound (the more resonant the more Diesel
	like it is), between 0 and 1
iBrightness - Overall brightness of the engine, between 0 and 1

PERFORMANCE
aOut - Sound of the engine
kSpeed - Speed of the car, between 0 and 1 (50 to 800Hz), portamento is used
	internally to avoid instantaneous changes

CREDITS
Author: Jeanette C.
*/

opcode m_scene_car_engine1, a, kii
	kSpeed, iResonance, iBrightness xin

	; Create the ftable for the modulation envelopes
	iRampFunction ftgen 0, 0, 4096, 8, 0, 48, 1, 4024, 0, 24, 0
	iSine         ftgen 0, 0, 32768, 10, 1

	; Calculate modulation depths for AM
	iBrightness = limit:i(iBrightness, 0, 1)
	iResonance = limit:i(iResonance, 0, 1)
	kSpeed = port:k(limit:k(kSpeed, 0, 1), .1)
	kRandomise = randi:k(.05, .5)
	kFreq = linlin:k(kSpeed, 50, 800)
	kFreq += kFreq * kRandomise
	kMod = limit:k((kFreq / 400), 0, 1)
	kMod1 = linlin:k(kMod, .25, .333)
	kLowpassFreq = linlin:k(iBrightness, 300, 2000)
	kLowpassFreq += kLowpassFreq * kMod * .75
	kVolume = linlin:k(kMod, .5, .7)
	kVcoShape = linlin:k((iResonance * kSpeed), .4, .1)
	iFeedback = linlin:i(iResonance, .1, .6)
	kAmpEnv = oscil:k(1, kFreq*kMod1, iRampFunction)
	kAmpEnv2 = oscil:k(kMod, kFreq*.6, iRampFunction)

	; Create the tonal component
	aMain = vco2((kAmpEnv + kAmpEnv2)/2, kFreq, 2, kVcoShape)

	; Create delayed versions of that for multiple cylinders
	aDummy delayr 500
	aMain2 = deltap3(2.4 / kFreq)
	aMain3 = deltap3(4.1 / kFreq)
	aMain4 = deltap3(6.7 / kFreq)
	aMain5 = deltap3(8.3 / kFreq)
	delayw(aMain)
	aSub = vco2((kAmpEnv2 * kVolume), (kFreq *.25), 4, .75)
	aDirect = (aMain + aMain2 + aMain3 + aMain4 + aMain5 + aSub*2) / 7
	aLowpassDirect = butterlp(aDirect, kLowpassFreq)

	; Noise components
	; Add a portamento to the base frequency
	kNoiseFreq = port:k(kFreq, 1)
	aNoise = noise(port:k(kAmpEnv2, .01), 0)
	aNoiseBand1 = butterbp(aNoise, (kNoiseFreq / 2), (kNoiseFreq / 8))
	aNoiseBand2 = butterbp(aNoise, kNoiseFreq, kNoiseFreq)
	aNoiseBand3 = butterbp((aNoise * .75), (kNoiseFreq * 3), (kNoiseFreq * 2))
	aNoiseBand4 = butterbp((aNoise * .3 * (1 + kMod)), (kNoiseFreq * 7), (kNoiseFreq *.2))
	aNoiseBands = aNoiseBand1 + aNoiseBand2 + aNoiseBand3 + aNoiseBand4

	; Engine resonance
	aOut = dcblock2(wguide1((aLowpassDirect + aNoiseBands * .5), 400, 2000, iFeedback))

	xout(aOut * kVolume * 3)
endop

/*
m_scene_car_engine2 - Sound-alike of a better/more luxurious car interior

DESCRIPTION
A sound-alike emulation of a more luxurious car interior. Compared to
m_scene_car_engine1 this sound is much more dominated by the sound of the
wheels and noise-like componenets of the engine.

SYNTAX
aOut m_scene_car_engine2 kSpeed

INITIALIZATION

PERFORMANCE
kSpeed - Symbolic speed control, between 0 and 1, used portamento internally to
	avoid instantaneous changes

CREDITS
Author: Jeanette C.
*/

opcode m_scene_car_engine2, a, k
	kSpeed xin

	; Initialise filter parameters
	kSpeed = port:k(limit:k(kSpeed, 0, 1), .1)
	; Controls for the tire noise
	kLpFreq = linlin:k(kSpeed, 20, 80)
	kHpFreq = linlin:k(kSpeed, 50, 300)
	kVolume = linlin:k(kSpeed, 4, 6)

	; Controls for the resonator and engine parameters
	kResFreq = linlin:k(logcurve:k(kSpeed, 2), 200, 300)
	kResFb = linlin:k(logcurve:k(kSpeed, 3), .01, .025)
	kResLpFreq = linlin:k(kSpeed, 160, 200)
	kPulseFreq = linlin:k(kSpeed, 20, 140)

	; Add portamento to the engine controls for better sound/realism
	kResFreq = port:k(kResFreq, 1.5)
	kResFb = port:k(kResFb, 1)
	kPulseFreq = port:k(kPulseFreq, 1)
	kPulseVolume = linlin:k(kPulseFreq, .06, .02, 20, 140)

	; Tires on the asphalt
	aNoise = noise(kVolume, 0)
	aNoise2 = noise((kVolume * .25), 1)
	aHpNoise = butterhp(aNoise2, kHpFreq)
	aLpNoise = butterlp(aHpNoise, kLpFreq)

	; Engine noise
	; Pulses for the cylinders
	aPulses = vco2((kPulseVolume * .5), kPulseFreq, 2, .2)
	aPulses2 = vco2(kPulseVolume, (kPulseFreq * .5), 2, .8, .351)
	aPulses3 = vdelay(aPulses, a(1/(kPulseFreq * 3)), 75)
	aPulses4 = vdelay(aPulses2, a(1/(kPulseFreq * 5)), 75)
	
	; Resonate pulses and noise for additional sound
	aResonator = dcblock2(wguide1((aNoise*.02 + aPulses + aPulses2 + aPulses3 + aPulses4), kResFreq, 120, kResFb))
	aHpRes = butterhp(aResonator, 300)
	aLpRes = butterlp(aHpRes, (kResLpFreq * .75))

	; Add tire and engine noise
	aOut = ((aLpRes * 5.5) + aLpNoise)

	; Output the audio
	xout(aOut * 6)
endop

/*
m_scene_car_indicator - Several styles of car indicators

DESCRIPTION
m_scene_car_indicator offers several styles of indicator sounds, from a typical
modern version, to the classical mechanical to heavy duty trucks and tractors.
This is no physical model.

SYNTAX
aOut m_scene_car_indicator kStyle

INITIALIZATION

PERFORMANCE
aOut - The indicator sound
kStyle - Style of indicator, possible values:
	0: Modern (short sine wave clicks at diferent pitch)
	1: Oldschool (mechanical sound)
	2: Heavy duty (beeping noise heard from heavy duty vehicles)

CREDITS
Author: Jeanette C.
*/
opcode m_scene_car_indicator, a, k
	kStyle xin

	; Initialisation and setup
	aOut init 0
	kStyle = round:k(kStyle)
	if (kStyle <= 0) then
		kAmpEnv = loopseg(3, 0, 0, 1, .01, 0, .99, 0, 0)
		kFreq = loopseg(1.5, 0, 0, 1, .5, 1, 0, .75, .5, .75, 0)
		aOut = oscil:a(kAmpEnv, 990*kFreq, -1)
	elseif (kStyle == 1) then
		kAmpEnv = loopseg(1.5, 0, 0, .77, .02, 0, .98, 0, 0, .2, .015, 0, .985, 0)
		aNoise = noise(kAmpEnv, 1)
		aOut = butterhp(limit(aNoise, -1,1), 600)
	elseif (kStyle >= 2) then
		kAmpEnv = loopseg(1, 0, 0, .95, .49, .95, .01, 0, .49, 0, .01)
		aOut = oscil:a((kAmpEnv * .5), 3000, -1)
	endif

	; Output and limit the audio
	aOut = limit:a((aOut * .5), -.5, .5)
	xout(aOut)
endop

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
	isine ftgen 0, 0, 32768, 10, 1

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
	apink = noise(1, 1) ; Add a noise component
	afiltpink = butterhp(apink, kfreq)
	arotor = ((arotor + .2) * adrivephasormod *.7) + (afiltpink *.02)
	arotornoise = arotor * afiltpink
	arotorout = butterlp((arotor*.99 + apink *.002), kfreq)

	; Set up the brushes
	anoise = noise(1, 1)
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



; --- Interactive instruments (numbers 1-4, driven by Unity via gkSpeed) ---

instr Motor
	iMaxSpeed init 300
	iCaseFreq init 500
	kSpeed = gkSpeed
	aMotor = m_scene_engine(iMaxSpeed, iCaseFreq, kSpeed)
	outs(aMotor, aMotor)
endin

instr Boat
	iBrightness init 1
	iNumCylinders init 3
	kVolume init .7
	kSpeed = gkSpeed
	aBoat = m_scene_boat_engine(kSpeed, kVolume, iBrightness, iNumCylinders)
	outs(aBoat, aBoat)
endin

instr Car1
	iResonance init .4
	iBrightness init .2
	kSpeed = gkSpeed
	aCar = m_scene_car_engine1(kSpeed, iResonance, iBrightness)
	outs(aCar, aCar)
endin

instr Car2
	kSpeed = gkSpeed
	aCar = m_scene_car_engine2(kSpeed)
	outs(aCar, aCar)
endin

; --- Demo/auto instruments (self-contained speed curves, for score playback) ---

instr MotorAuto
	iMaxSpeed init 300
	iCaseFreq init 500
	kRandom = rspline:k(-.1, .1, .5, 3) + .8
	kSpeed = linseg:k(0, p3/4, 1, p3/4, 1, p3/4, 0, p3/4, 0)
	kSpeed *= kRandom
	aMotor = m_scene_engine(iMaxSpeed, iCaseFreq, kSpeed)
	outs(aMotor, aMotor)
endin

instr BoatAuto
	iBrightness init 1
	iNumCylinders init 3
	kVolume init .7
	kRandom = rspline:k(-.1, .1, .5, 3) + .8
	kSpeed = linseg:k(0, p3/4, 1, p3/4, 1, p3/4, 0, p3/4, 0)
	kSpeed *= kRandom
	aBoat = m_scene_boat_engine(kSpeed, kVolume, iBrightness, iNumCylinders)
	outs(aBoat, aBoat)
endin

instr Car1Auto
	iResonance init .4
	iBrightness init .2
	kRandom = rspline:k(-.1, .1, .5, 3) + .8
	kSpeed = linseg:k(0, p3/4, 1, p3/4, 1, p3/4, 0, p3/4, 0)
	kSpeed *= kRandom
	aCar = m_scene_car_engine1(kSpeed, iResonance, iBrightness)
	outs(aCar, aCar)
endin

instr Car2Auto
	kRandom = rspline:k(-.1, .1, .5, 3) + .8
	kSpeed = linseg:k(0, p3/4, 1, p3/4, 1, p3/4, 0, p3/4, 0)
	kSpeed *= kRandom
	aCar = m_scene_car_engine2(kSpeed)
	outs(aCar, aCar)
endin

instr IndicatorAuto
	kStyle = linseg(0, (p3/3 - .01), 0, .01, 1, (p3/3 - .01), 1, .01, 2, p3/3, 2)
	aOut = m_scene_car_indicator(kStyle)
	outs(aOut, aOut)
endin

instr Indicator
	kStyle = p4
	aOut = m_scene_car_indicator(kStyle)
	outs(aOut, aOut)
endin

</CsInstruments>
; ==============================================
<CsScore>
; Instruments are triggered at runtime by Unity (EngineAudioController.cs).
; f0 z keeps the score alive indefinitely without auto-starting any instrument.
f0 z
</CsScore>
</CsoundSynthesizer>