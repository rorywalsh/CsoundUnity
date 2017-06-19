//
//  CsoundUnityNativePlugin.hpp
//  CsoundUnity
//
//  Created by Walshr on 16/06/2017.
//  Copyright © 2017 cabbageaudio. All rights reserved.
//

#ifndef CsoundUnityNativePlugin_hpp
#define CsoundUnityNativePlugin_hpp

#include <stdio.h>

extern "C" void setCsoundInputSample(void *p, int pos, double sample);
extern "C" double getCsoundOutputSample(void *p, int pos);

#endif /* CsoundUnityNativePlugin_hpp */
