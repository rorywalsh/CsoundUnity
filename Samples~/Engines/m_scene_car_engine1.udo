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
	iRampFunction = ftgenonce(0, 0, 4096, 8, 0, 48, 1, 4024, 0, 24, 0)
	iSine = ftgenonce(0, 0, 32768, 10, 1)

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
	aNoise = fractalnoise(port:k(kAmpEnv2, .01), 0)
	aNoiseBand1 = butterbp(aNoise, (kNoiseFreq / 2), (kNoiseFreq / 8))
	aNoiseBand2 = butterbp(aNoise, kNoiseFreq, kNoiseFreq)
	aNoiseBand3 = butterbp((aNoise * .75), (kNoiseFreq * 3), (kNoiseFreq * 2))
	aNoiseBand4 = butterbp((aNoise * .3 * (1 + kMod)), (kNoiseFreq * 7), \
		(kNoiseFreq *.2))
	aNoiseBands = aNoiseBand1 + aNoiseBand2 + aNoiseBand3 + aNoiseBand4

	; Engine resonance
	aOut = dcblock2(wguide1((aLowpassDirect + aNoiseBands * .5), 400, 2000, iFeedback))

	xout(aOut * kVolume * 3)
endop
