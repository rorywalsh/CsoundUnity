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

#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif

namespace Csound.Unity
{
    public partial class CsoundUnity
    {
        /// <summary>
        /// Create an i[] or k[] channel, or verify an existing channel's exact type and shape.
        /// Initialization may allocate and briefly suspend Unity audio processing; call during setup,
        /// never from an audio callback. Existing channels are not resized.
        /// </summary>
        /// <param name="name">The channel name.</param>
        /// <param name="type">Init or Control numeric elements.</param>
        /// <param name="sizes">One nonnegative size per axis.</param>
        /// <returns>The initialization or shape-validation result; UnsupportedPlatform in WebGL players.</returns>
        public CsoundArrayChannelStatus InitializeArrayChannel(string name, CsoundArrayChannelType type, params int[] sizes)
        {
            CsoundArrayChannelArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateShape(type, sizes);
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundArrayChannelStatus.UnsupportedPlatform;
#else
            return IsInitialized && csound != null ? csound.InitializeArrayChannel(name, type, sizes)
                : CsoundArrayChannelStatus.Unavailable;
#endif
        }

        /// <summary>Read numeric array metadata and axis sizes without creating a channel or allocating a buffer.</summary>
        /// <param name="name">The channel name.</param>
        /// <param name="sizes">Destination for axis sizes; unused trailing entries are unchanged.</param>
        /// <param name="info">Current metadata, also returned when a supplied buffer is too small.</param>
        /// <returns>The read result; UnsupportedPlatform in WebGL players.</returns>
        public CsoundArrayChannelStatus GetArrayChannelInfo(string name, int[] sizes, out CsoundArrayChannelInfo info)
        {
            CsoundArrayChannelArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateBuffer(sizes, nameof(sizes));
            info = default;
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundArrayChannelStatus.UnsupportedPlatform;
#else
            return IsInitialized && csound != null ? csound.GetArrayChannelInfo(name, sizes, out info)
                : CsoundArrayChannelStatus.Unavailable;
#endif
        }

        /// <summary>Copy a numeric array into a reusable flat buffer; unused trailing entries are unchanged.</summary>
        /// <param name="name">The channel name.</param>
        /// <param name="destination">Destination for the whole current array.</param>
        /// <param name="length">Number of elements copied, or required when BufferTooSmall is returned.</param>
        /// <returns>The copy result. No channel is created and no partial copy is performed.</returns>
        public CsoundArrayChannelStatus GetArrayChannel(string name, MYFLT[] destination, out int length)
        {
            CsoundArrayChannelArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateBuffer(destination, nameof(destination));
            length = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundArrayChannelStatus.UnsupportedPlatform;
#else
            return IsInitialized && csound != null ? csound.GetArrayChannel(name, destination, out length)
                : CsoundArrayChannelStatus.Unavailable;
#endif
        }

        /// <summary>Copy numeric data and its axis sizes under one channel lock, including when producers resize.</summary>
        /// <param name="name">The channel name.</param>
        /// <param name="destination">Destination for the whole current array.</param>
        /// <param name="sizes">Destination for the current axis sizes.</param>
        /// <param name="info">Metadata corresponding to this copy, or required capacity on BufferTooSmall.</param>
        /// <returns>The copy result. Neither buffer is modified when a buffer is too small.</returns>
        public CsoundArrayChannelStatus GetArrayChannel(string name, MYFLT[] destination, int[] sizes,
            out CsoundArrayChannelInfo info)
        {
            CsoundArrayChannelArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateBuffer(destination, nameof(destination));
            CsoundArrayChannelArguments.ValidateBuffer(sizes, nameof(sizes));
            info = default;
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundArrayChannelStatus.UnsupportedPlatform;
#else
            return IsInitialized && csound != null ? csound.GetArrayChannel(name, destination, sizes, out info)
                : CsoundArrayChannelStatus.Unavailable;
#endif
        }

        /// <summary>Replace all numeric elements of the channel's current shape; source length must match exactly.</summary>
        /// <param name="name">The channel name.</param>
        /// <param name="source">Flat i[] or k[] values, in Csound array order.</param>
        /// <returns>The write result. No channel is created or resized and no partial write is performed.</returns>
        public CsoundArrayChannelStatus SetArrayChannel(string name, MYFLT[] source)
        {
            CsoundArrayChannelArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateBuffer(source, nameof(source));
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundArrayChannelStatus.UnsupportedPlatform;
#else
            return IsInitialized && csound != null ? csound.SetArrayChannel(name, source)
                : CsoundArrayChannelStatus.Unavailable;
#endif
        }
    }
}
