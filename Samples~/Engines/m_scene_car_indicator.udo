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
		kAmpEnv = loopseg(1.5, 0, 0, .77, .02, 0, .98, 0, 0, .2, .015, 0, \
			.985, 0)
		aNoise = fractalnoise(kAmpEnv, 1)
		aOut = butterhp(limit(aNoise, -1,1), 600)
	elseif (kStyle >= 2) then
		kAmpEnv = loopseg(1, 0, 0, .95, .49, .95, .01, 0, .49, 0, .01)
		aOut = oscil:a((kAmpEnv * .5), 3000, -1)
	endif

	; Output and limit the audio
	aOut = limit:a((aOut * .5), -.5, .5)
	xout(aOut)
endop
