// CsoundUnityNative.cpp : Defines the exported functions for the DLL application.
//

#include "CsoundUnityNativePlugin.h"
#include <csound.h>

extern "C" {
	void setCsoundInputSample(void *p, int pos, double sample) {
		MYFLT *spin = csoundGetSpin((CSOUND*) p);
		spin[pos] = sample;
	}

	double getCsoundOutputSample(void *p, int pos) {
		MYFLT *spout = csoundGetSpout((CSOUND*)p);
		return spout[pos];
	}
}
