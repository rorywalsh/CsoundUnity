/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Text;
using System.Threading;

namespace Csound.Unity
{
    public partial class CsoundUnityBridge
    {
        private readonly object _channelDataGate = new object();
        private volatile bool _initializingChannelData;

#if !UNITY_WEBGL || UNITY_EDITOR
        private bool WaitForChannelInitialization()
        {
            // Existing callbacks can finish; new callbacks see _initializingChannelData and produce silence.
            var started = Environment.TickCount;
            while (Volatile.Read(ref _inAudio) > 0)
            {
                if (_closing || unchecked(Environment.TickCount - started) >= 200) return false;
                Thread.Yield();
            }
            return !_closing;
        }

        private static unsafe IntPtr EncodeChannelName(string name, byte* buffer)
        {
            int bytes;
            fixed (char* characters = name)
                bytes = Encoding.UTF8.GetBytes(characters, name.Length, buffer, CsoundChannelDataArguments.MaxNameBytes);
            buffer[bytes] = 0;
            return (IntPtr)buffer;
        }
#endif
    }
}
