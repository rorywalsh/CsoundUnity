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
	iAmpFunction = ftgenonce(0, 0, 1024, 7, 1, 4, 1, 508, .3, 510, .1, 1, 0, \
		1, 0)
	iFilterFunction = ftgenonce(0, 0, 1024, 7, 1, 70, .1, 10, .8, 50, .3, \
		270, .2, 612, 0)

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
	aNoise = fractalnoise(kAmpEnv, 1)
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
