/*
Copyright (C) 2015 Rory Walsh. 

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

Contributors:

Bernt Isak Wærstad
Charles Berman
Giovanni Bedetti
Hector Centeno
NPatch

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
using System.Collections.Generic;
using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// A set of control channel values for one csd, saved as an asset.
    /// <para>
    /// A preset carries values and nothing else: the channel definition — type, range, skew,
    /// increment, a combobox's option list — belongs to the csd. Create one with
    /// <see cref="CsoundUnity.CreatePreset(string, string, List{CsoundChannelController})"/>,
    /// and apply it with <see cref="CsoundUnity.SetPreset(CsoundUnityPreset)"/>.
    /// </para>
    /// </summary>
    [Serializable]
    public class CsoundUnityPreset : ScriptableObject
    {
        /// <summary>The preset's name, which <c>CreatePreset</c> gives to the asset as well.</summary>
        public string presetName;

        /// <summary>The file name of the csd this preset was saved from.</summary>
        public string csoundFileName;

        /// <summary>
        /// The saved channels. Applying the preset writes their <c>value</c>, and the
        /// <c>value2</c> of a two-value widget such as an xypad or a range slider.
        /// </summary>
        public List<CsoundChannelController> channels;
#pragma warning disable 414
        [SerializeField] private bool _drawChannels = false;
#pragma warning restore 414
    }
}
