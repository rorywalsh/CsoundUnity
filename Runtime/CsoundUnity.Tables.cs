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

using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using Csound.Unity.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif
using ASU = Csound.Unity.Utilities.AudioSamplesUtils;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity
{
    public partial class CsoundUnity
    {
        #region TABLES

        /// <summary>
        /// Creates a table with the supplied float samples.
        /// Can be called during performance.
        /// </summary>
        /// <param name="tableNumber">The Csound function table number to create.</param>
        /// <param name="samples">Float array of samples to populate the table with.</param>
        /// <returns>Zero on success, or -1 on failure.</returns>
        public int CreateFloatTable(int tableNumber, float[] samples)
        {
            var myFlts = ASU.ConvertToMYFLT(samples);
            return CreateTable(tableNumber, myFlts);
        }

        /// <summary>
        /// Creates a table with the supplied samples.
        /// Can be called during performance.
        /// </summary>
        /// <param name="tableNumber">The Csound function table number to create.</param>
        /// <param name="samples">MYFLT array of samples to populate the table with.</param>
        /// <returns>Zero on success, or -1 on failure.</returns>
        public int CreateTable(int tableNumber, MYFLT[] samples)
        {
            if (samples.Length < 1) return -1;
            var resTable = CreateTableInstrument(tableNumber, samples.Length);
            if (resTable != 0)
                return -1;
            // copy samples to the newly created table
            CopyTableIn(tableNumber, samples);

            return resTable;
        }

        /// <summary>
        /// Creates an empty table, to be filled with samples later. 
        /// Please note that trying to read the samples from an empty folder will produce a crash.
        /// Can be called during performance.
        /// </summary>
        /// <param name="tableNumber">The number of the newly created table</param>
        /// <param name="tableLength">The length of the table in samples</param>
        /// <returns>0 If the table could be created</returns>
        public int CreateTableInstrument(int tableNumber, int tableLength)
        {
            var createTableInstrument = String.Format(@"gisampletable{0} ftgen {0}, 0, {1}, -7, 0, 0", tableNumber, -tableLength /** AudioSettings.outputSampleRate*/);
            return CompileOrc(createTableInstrument);
        }

        /// <summary>
        /// Returns the length of a function table (not including the guard point), or -1 if the table does not exist.
        /// </summary>
        /// <param name="table">The function table number to query.</param>
        /// <returns>The table length (excluding guard point), or -1 if the table does not exist.</returns>
        public int GetTableLength(int table)
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.TableLength(table);
        }

        /// <summary>
        /// Retrieves a single sample from a Csound function table.
        /// </summary>
        /// <param name="tableNumber">The function table number.</param>
        /// <param name="index">Zero-based sample index within the table.</param>
        /// <returns>The sample value at the given index, or 0 if not initialized.</returns>
        public MYFLT GetTableSample(int tableNumber, int index)
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetTable(tableNumber, index);
        }

        /// <summary>
        /// Stores values to function table 'numTable' in tableValues, and returns the table length (not including the guard point).
        /// If the table does not exist, tableValues is set to NULL and -1 is returned.
        /// </summary>
        /// <param name="tableValues">Output array filled with the table contents.</param>
        /// <param name="numTable">The function table number to read.</param>
        /// <returns>The table length, or -1 if the table does not exist.</returns>
        public int GetTable(out MYFLT[] tableValues, int numTable)
        {
            if (!IsInitialized || csound == null) { tableValues = null; return -1; }
            return csound.GetTable(out tableValues, numTable);
        }

        /// <summary>
        /// Stores the arguments used to generate function table 'tableNum' in args, and returns the number of arguments used.
        /// If the table does not exist, args is set to NULL and -1 is returned.
        /// NB: the argument list starts with the GEN number and is followed by its parameters.
        /// eg. f 1 0 1024 10 1 0.5 yields the list {10.0,1.0,0.5}
        /// </summary>
        /// <param name="args">Output array filled with the GEN number followed by its parameters.</param>
        /// <param name="index">The function table number to query.</param>
        /// <returns>The number of generation arguments, or -1 if the table does not exist.</returns>
        public int GetTableArgs(out MYFLT[] args, int index)
        {
            if (!IsInitialized || csound == null) { args = null; return -1; }
            return csound.GetTableArgs(out args, index);
        }

        /// <summary>
        /// Sets the value of a slot in a function table. The table number and index are assumed to be valid.
        /// </summary>
        /// <param name="table">The function table number.</param>
        /// <param name="index">Zero-based slot index to write.</param>
        /// <param name="value">The value to write into the slot.</param>
        public void SetTable(int table, int index, MYFLT value)
        {
            if (!IsInitialized || csound == null) return;
            csound.SetTable(table, index, value);
        }

        /// <summary>
        /// Copy the contents of a function table into a supplied array dest
        /// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
        /// </summary>
        /// <param name="table">The function table number to read from.</param>
        /// <param name="dest">Output array that receives the table contents.</param>
        public void CopyTableOut(int table, out MYFLT[] dest)
        {
            if (!IsInitialized || csound == null) { dest = null; return; }
            csound.TableCopyOut(table, out dest);
        }

        /// <summary>Queues a read into an array filled during a later performance cycle.</summary>
        /// <remarks>Use the overload with a request to determine when the array is ready.</remarks>
        /// <param name="table">The function table number to read.</param>
        /// <param name="dest">The array the copy will fill, sized for the table.</param>
        public void CopyTableOutAsync(int table, out MYFLT[] dest)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            CopyTableOutAsync(table, out dest, out _);
#else
            dest = Array.Empty<MYFLT>();
#endif
        }

        /// <summary>Queues a table read and returns the destination and a request to poll on the calling thread.</summary>
        /// <remarks>A null request means the read was not queued. Only read the destination after
        /// <see cref="CsoundTableCopyRequest.Succeeded"/> is true.</remarks>
        /// <param name="table">The function table number to read.</param>
        /// <param name="dest">The array the copy will fill, sized for the table.</param>
        /// <param name="request">The request to poll, or null when the copy was not queued.</param>
        /// <returns>Whether the copy was queued, and why it was not.</returns>
        public CsoundTableCopySubmission CopyTableOutAsync(int table, out MYFLT[] dest, out CsoundTableCopyRequest request)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var bridge = csound;
            if (IsInitialized && bridge != null) return bridge.TableCopyOutAsync(table, out dest, out request);
#endif
            dest = null;
            request = null;
#if !UNITY_WEBGL || UNITY_EDITOR
            return CsoundTableCopySubmission.NotReady;
#else
            return CsoundTableCopySubmission.UnsupportedPlatform;
#endif
        }

        /// <summary>
        /// Same as <see cref="CopyTableIn(int, MYFLT[])">CopyTableIn</see> but passing a float array.
        /// </summary>
        /// <param name="table">The function table number to write to.</param>
        /// <param name="source">Float array whose values will be copied into the table.</param>
        public void CopyFloatTableIn(int table, float[] source)
        {
            var myFlts = ASU.ConvertToMYFLT(source);
            CopyTableIn(table, myFlts);
        }

        /// <summary>
        /// Copy the contents of a supplied array into a function table
        /// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
        /// </summary>
        /// <param name="table">the number of the table</param>
        /// <param name="source">the supplied array</param>
        public void CopyTableIn(int table, MYFLT[] source)
        {
            if (!IsInitialized || csound == null) return;
            csound.TableCopyIn(table, source);
        }

        /// <summary>Queues a snapshot of the source for a later performance cycle.</summary>
        /// <param name="table">The function table number to write.</param>
        /// <param name="source">The samples to copy in. A snapshot is taken at submission.</param>
        public void CopyTableInAsync(int table, MYFLT[] source) =>
            CopyTableInAsync(table, source, out _);

        /// <summary>Queues a table write and returns a request to poll on the calling thread.</summary>
        /// <remarks>A null request means the write was not queued.</remarks>
        /// <param name="table">The function table number to write.</param>
        /// <param name="source">The samples to copy in. A snapshot is taken at submission.</param>
        /// <param name="request">The request to poll, or null when the copy was not queued.</param>
        /// <returns>Whether the copy was queued, and why it was not.</returns>
        public CsoundTableCopySubmission CopyTableInAsync(int table, MYFLT[] source, out CsoundTableCopyRequest request)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var bridge = csound;
            if (IsInitialized && bridge != null) return bridge.TableCopyInAsync(table, source, out request);
            request = null;
            return CsoundTableCopySubmission.NotReady;
#else
            request = null;
            return CsoundTableCopySubmission.UnsupportedPlatform;
#endif
        }

        #endregion TABLES
    }
}
