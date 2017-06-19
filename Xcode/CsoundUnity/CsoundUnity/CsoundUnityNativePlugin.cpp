//
//  CsoundUnityNativePlugin.cpp
//  CsoundUnity
//
//  Created by Walshr on 16/06/2017.
//  Copyright © 2017 cabbageaudio. All rights reserved.
//

#include "CsoundUnityNativePlugin.hpp"
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
    
    int main(){return 1;}
}
