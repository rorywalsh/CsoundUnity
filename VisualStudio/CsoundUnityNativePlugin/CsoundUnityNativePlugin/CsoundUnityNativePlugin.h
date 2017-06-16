#pragma once

extern "C" __declspec(dllexport) void setCsoundInputSample(void *p, int pos, double sample);
extern "C" __declspec(dllexport) double getCsoundOutputSample(void *p, int pos);