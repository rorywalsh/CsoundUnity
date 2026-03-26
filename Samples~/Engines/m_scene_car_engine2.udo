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
	aNoise = fractalnoise(kVolume, 0)
	aNoise2 = fractalnoise((kVolume * .25), 1)
	aHpNoise = butterhp(aNoise2, kHpFreq)
	aLpNoise = butterlp(aHpNoise, kLpFreq)

	; Engine noise
	; Pulses for the cylinders
	aPulses = vco2((kPulseVolume * .5), kPulseFreq, 2, .2)
	aPulses2 = vco2(kPulseVolume, (kPulseFreq * .5), 2, .8, .351)
	aPulses3 = vdelay(aPulses, a(1/(kPulseFreq * 3)), 75)
	aPulses4 = vdelay(aPulses2, a(1/(kPulseFreq * 5)), 75)
	
	; Resonate pulses and noise for additional sound
	aResonator = dcblock2(wguide1((aNoise*.02 + aPulses + aPulses2 + aPulses3 \
		+ aPulses4), kResFreq, 120, kResFb))
	aHpRes = butterhp(aResonator, 300)
	aLpRes = butterlp(aHpRes, (kResLpFreq * .75))

	; Add tire and engine noise
	aOut = ((aLpRes * 5.5) + aLpNoise)

	; Output the audio
	xout(aOut * 6)
endop
