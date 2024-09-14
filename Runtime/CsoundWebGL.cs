/*

C S O U N D for WebGL

Simple wrapper building WebGL hosts for Csound 6 via the Csound API
and is licensed under the same terms and disclaimers as Csound described below.

Copyright (C) 2024 Rory Walsh, Giovanni Bedetti

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Written by Giovanni Bedetti, July 2024

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using MYFLT = System.Single;

// ReSharper disable once CheckNamespace
namespace CsoundWebGL
{
    public partial class Csound6
    {
        private const string DLLVersion = "__Internal";
        
        internal delegate void CsoundInitializeCallback(int instanceId);
        internal delegate void CsoundSetOptionCallback(int instanceId, int res);
        internal delegate void CsoundStopCallback(int instanceId);

        internal delegate void CsoundGetChannelCallback(int instanceId, string channel, float value);
        // private delegate void CsoundGetTableCallback(int instanceId, int tableLength, IntPtr tableId);

        public class NativeMethods
        {
            #region Instantiation

            [DllImport(DLLVersion)]
            internal static extern void csoundInitialize(int id, int variation, string csdText, string filesToLoad, int callback);
            // [DllImport(DLLVersion)]
            // internal static extern int csoundSetOption(int instanceId, string csOption, int callback);
            // [DllImport(DLLVersion)]
            // internal static extern void csoundStop(int instanceId, int callback);
           
            #endregion Instantiation

            [DllImport(DLLVersion)]
            internal static extern void csoundSetChannel(int instanceId, string channel, float value);
            [DllImport(DLLVersion)]
            internal static extern void csoundGetChannel(int instanceId, string channel, int callback);
            // [DllImport(DLLVersion)]
            // private static extern void csoundGetTable(int instanceId, int tableId, IntPtr callback);
            [DllImport(DLLVersion)]
            internal static extern int csoundInputMessage(int instanceId, string scoreEvent);
        }
    }
}

#endif