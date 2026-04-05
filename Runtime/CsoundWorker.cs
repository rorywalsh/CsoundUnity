using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Threading;

using static Csound.Unity.CsoundCSharp.Csound6;
using System.IO;

#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS
using MYFLT = System.Single;
#endif

namespace Csound.Unity
{
    public class CsoundWorker : CsoundUnityBridge
    {
        #region Fields

        Thread performance;

        private static string _csdEmptyTemplate =
            "<CsoundSynthesizer>\n" +
            "<CsInstruments>\n" +
            "0dbfs = 1\n" +
            "ksmps = 1\n\n" +
            "instr 9999\n" +
            "endin\n" +
            "</CsInstruments>\n" +
            "</CsoundSynthesizer>" +
            "<CsScore>\n" +
            "f0 z\n" +
            "i9999 0 z\n" +
            "</CsScore>";

        private static string _saveFileInstrTemplate =
            "instr {0}\n" +
            "\tians  ftaudio {0}, \"{1}\", {2}\n" +
            "\t\tturnoff\n" +
            "endin\n" +
            "schedule({0}, 0, 1)";

        private Dictionary<int, string> _createdInstruments = new Dictionary<int, string>();
        private HashSet<int> _createdTables = new HashSet<int>();
        private bool _running = false;

        public bool IsInitialized { get; set; }

        protected bool m_disposed = false;
        private IDictionary<string, GCHandle> m_callbacks = new Dictionary<string, GCHandle>();

        int nOperation = 1;

        #endregion Fields

        #region Constructors

        public CsoundWorker() : base()
        {
            Debug.Log("CsoundWorker construction");
            IsInitialized = false;

            NativeMethods.csoundInitialize(0);
            csound = NativeMethods.csoundCreate(System.IntPtr.Zero, null);
            if (csound == null)
            {
                Debug.LogError("Couldn't create Csound!");
                return;
            }

            NativeMethods.csoundCreateMessageBuffer(csound, 0);
            SetMessageCallback(RawMessageCallback);
            NativeMethods.csoundSetOption(csound, "-n");
            NativeMethods.csoundSetOption(csound, "-d");
            NativeMethods.csoundSetOption(csound, $"--sample-rate={AudioSettings.outputSampleRate}");

            var ret = NativeMethods.csoundCompileCSD(csound, _csdEmptyTemplate, 1, 0, null);
            NativeMethods.csoundStart(csound);
            var compiledOk = ret == 0;
            Debug.Log($"CsoundWorker created and started. CsoundCompile: {compiledOk}\n" +
                $"AudioSettings.outputSampleRate: {AudioSettings.outputSampleRate}\n" +
                $"GetSr: {GetSr()}\n" +
                $"GetKr: {GetKr()}\n" +
                $"Get0dbfs: {Get0dbfs()}\n" +
                $"GetKsmps: {GetKsmps()}");

            performance = new Thread(PerformanceThread);
            performance.Start();
            _running = true;

            IsInitialized = true;
        }

        #endregion Constructors

        #region Lifecycle

        public override void OnApplicationQuit()
        {
            _running = false;
            Debug.Log("Worker OnApplicationQuit");
            // Join the performance thread BEFORE destroying the native Csound instance.
            // Calling base.OnApplicationQuit() (csoundDestroy) while the thread is still
            // inside csoundPerformKsmps causes a deadlock: csoundDestroy tries to acquire
            // an internal Csound lock that csoundPerformKsmps is holding.
            // Disposing first guarantees the thread has fully exited before we free the instance.
            Dispose();
            base.OnApplicationQuit();
        }

        public void Destroy()
        {
            OnApplicationQuit();
        }

        protected virtual void Dispose()
        {
            if (m_disposed) return;

            Debug.Log("Disposing");

            if (m_callbacks != null)
            {
                foreach (GCHandle gch in m_callbacks.Values) gch.Free();
                m_callbacks.Clear();
                m_callbacks = null;
            }

            Debug.Log("Joining CsoundWorker thread");
            if (!performance.Join(millisecondsTimeout: 3000))
                Debug.LogWarning("[CsoundWorker] Performance thread did not exit within 3 s — proceeding with destroy anyway.");
            performance = null;

            m_disposed = true;
            Debug.Log("CsoundWorker Disposed");
        }

        #endregion Lifecycle

        #region Public API

        public static CsoundWorker Create()
        {
            return new CsoundWorker();
        }

        /// <summary>
        /// Scans a CSD string in a temporary, isolated Csound instance (−n −d, no audio I/O).
        /// After compiling the CSD and running one <c>PerformKsmps</c>, all channels allocated
        /// by the orchestra are queried and returned via <paramref name="onComplete"/>.
        /// <para>
        /// The callback is invoked on the background scan thread.  To update Unity or Editor
        /// state, marshal back to the main thread (e.g. via <c>EditorApplication.update</c>).
        /// </para>
        /// </summary>
        /// <param name="csdString">Full CSD content as a string.</param>
        /// <param name="onComplete">
        /// Invoked with a dictionary mapping channel name → <see cref="CsoundUnityBridge.ChannelInfo"/>.
        /// </param>
        public static void ScanCsdForChannels(
            string csdString,
            Action<IDictionary<string, CsoundUnityBridge.ChannelInfo>> onComplete)
        {
            // AudioSettings.outputSampleRate may only be read on the main thread —
            // capture it here, before the background thread is spawned.
            var sampleRate = AudioSettings.outputSampleRate;

            var thread = new Thread(() =>
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                Debug.Log("[CsoundWorker Scan] Starting channel scan …");

                var cs = IntPtr.Zero;
                try
                {
                    cs = NativeMethods.csoundCreate(IntPtr.Zero, null);
                    if (cs == IntPtr.Zero)
                    {
                        Debug.LogError("[CsoundWorker Scan] Failed to create Csound instance.");
                        onComplete?.Invoke(new SortedDictionary<string, CsoundUnityBridge.ChannelInfo>());
                        return;
                    }

                    NativeMethods.csoundCreateMessageBuffer(cs, 0);
                    NativeMethods.csoundSetOption(cs, "-n");
                    NativeMethods.csoundSetOption(cs, "-d");
                    NativeMethods.csoundSetOption(cs, $"--sample-rate={sampleRate}");

                    var ret = NativeMethods.csoundCompileCSD(cs, csdString, 1, 0, null);

                    // Drain compile-time messages regardless of success/failure
                    FlushScanMessageBuffer(cs);

                    if (ret == 0)
                    {
                        NativeMethods.csoundStart(cs);
                        NativeMethods.csoundPerformKsmps(cs);
                        FlushScanMessageBuffer(cs);
                    }
                    else
                    {
                        Debug.LogWarning($"[CsoundWorker Scan] CSD compile returned {ret}. Channel list may be incomplete.");
                    }

                    var channels = CsoundUnityBridge.GetChannelList(cs);
                    Debug.Log($"[CsoundWorker Scan] Done. Found {channels.Count} channel(s).");
                    foreach (var kv in channels)
                        Debug.Log($"[CsoundWorker Scan]   {kv.Key}  type={kv.Value.Type}  dir={kv.Value.Direction}");

                    onComplete?.Invoke(channels);
                }
                catch (Exception ex)
                {
                    // Catches managed exceptions (marshalling errors, threading issues, etc.).
                    // Native crashes inside the Csound library cannot be caught here and will
                    // still terminate the process — that would require an out-of-process scan.
                    Debug.LogError($"[CsoundWorker Scan] Exception during channel scan — the CSD may contain code that crashes Csound.\n{ex}");
                    onComplete?.Invoke(new SortedDictionary<string, CsoundUnityBridge.ChannelInfo>());
                }
                finally
                {
                    // Always clean up the temporary Csound instance, even after an exception.
                    if (cs != IntPtr.Zero)
                    {
                        try { NativeMethods.csoundDestroyMessageBuffer(cs); } catch { }
                        try { NativeMethods.csoundDestroy(cs); } catch { }
                    }
                }
#endif
            });
            thread.IsBackground = true;
            thread.Name = "CsoundWorkerScan";
            thread.Start();
        }

        /// <summary>
        /// Creates a table with the supplied samples.
        /// Can be called during performance.
        /// </summary>
        /// <param name="tableNumber">The table number</param>
        /// <param name="samples"></param>
        /// <returns></returns>
        public int CreateTable(int tableNumber, MYFLT[] samples)
        {
            if (samples.Length < 1) return -1;
            if (_createdTables.Contains(tableNumber))
            {
                TableCopyIn(tableNumber, samples);
                return 0;
            }

            var resTable = CreateTableInstrument(tableNumber, samples.Length);
            if (resTable != 0)
                return -1;

            _createdTables.Add(tableNumber);
            TableCopyIn(tableNumber, samples);

            return resTable;
        }

        /// <summary>
        /// Creates an empty table, to be filled with samples later.
        /// Please note that trying to read the samples from an empty table will produce a crash.
        /// Can be called during performance.
        /// </summary>
        /// <param name="tableNumber">The number of the newly created table</param>
        /// <param name="tableLength">The length of the table in samples</param>
        /// <returns>0 If the table could be created</returns>
        public int CreateTableInstrument(int tableNumber, int tableLength)
        {
            var createTableInstrument = string.Format(@"gisampletable{0} ftgen {0}, 0, {1}, -7, 0, 0", tableNumber, -tableLength);
            Debug.Log($"orc to create table {tableNumber}, length: {tableLength}: \n" + createTableInstrument);
            return CompileOrc(createTableInstrument);
        }

        // uses a table to store the samples and creates an instrument to save data on disk
        public bool SaveAudioFile(string destination, float[] samples, int bitsPerSample = 16)
        {
            var fileType = 10;
            var format = fileType + 4; // WAV 16 BIT, see https://csound.com/docs/manual/ftaudio.html

            var extension = Path.GetExtension(destination);
            switch (extension.ToLower())
            {
                case ".aif":
                case ".aiff":
                    fileType = 20;
                    break;
                case ".wav":
                default:
                    fileType = 10;
                    break;
            }

            switch (bitsPerSample)
            {
                case 16:
                    format = fileType + 4;
                    break;
                case 32:
                    format = fileType + 5;
                    break;
                default:
                    format = fileType + 4;
                    break;
            }

            CreateTable(nOperation, Utilities.AudioSamplesUtils.ConvertToMYFLT(samples));
            if (_createdInstruments.ContainsKey(nOperation))
                return ScheduleInstrument(nOperation, 0, 1) == 0;

            var instr = string.Format(_saveFileInstrTemplate, nOperation, destination, format);
            Debug.Log($"SaveAudioFile instr:\n{instr}");
            var res = CompileOrc(instr);

            if (res == 0)
            {
                // avoid duplication of instruments with the same number
                _createdInstruments.Add(nOperation, "SaveFile");
                nOperation++;
            }
            return res == 0;
        }

        public int ScheduleInstrument(int instrNumber, float start, float duration, params string[] pfields)
        {
            var pp = pfields.Length > 0 ? string.Join(", ", pfields) : string.Empty;
            pp = string.IsNullOrWhiteSpace(pp) || pp.Length == 0 ? pp : pp.Substring(0, pp.Length - 1);
            var msg = pp.Length > 0 ? $"schedule {instrNumber}, {start}, {duration}, {pp}" :
                $"schedule {instrNumber}, {start}, {duration}";
            Debug.Log($"scheduling instrument, msg: {msg}");
            return CompileOrc(msg);
        }

        #endregion Public API

        #region Private Helpers

        /// <summary>
        /// Drains the message buffer of a raw Csound handle and logs each message to the console.
        /// Used exclusively by <see cref="ScanCsdForChannels"/>.
        /// </summary>
        private static void FlushScanMessageBuffer(IntPtr cs)
        {
            while (NativeMethods.csoundGetMessageCnt(cs) > 0)
            {
                var msgPtr = NativeMethods.csoundGetFirstMessage(cs);
                var msg = CsoundUnityBridge.CharPtr2String(msgPtr);
                if (!string.IsNullOrEmpty(msg))
                    Debug.Log($"[CsoundWorker Scan] {msg.TrimEnd()}");
                NativeMethods.csoundPopFirstMessage(cs);
            }
        }

        public void PerformanceThread()
        {
            while (_running)
            {
                try
                {
                    var result = NativeMethods.csoundPerformKsmps(csound);
                    if (result != 0) break; // non-zero means end of score or error
                }
                catch (ThreadAbortException)
                {
                    Debug.Log("Thread aborted");
                    break;
                }
            }
        }

        protected void ReleaseProtectedPointer(IntPtr pgcData)
        {
            if ((pgcData != null) && (pgcData != IntPtr.Zero))
            {
                var gcData = GCHandle.FromIntPtr(pgcData);
                gcData.Free();
            }
        }

        private void RawMessageCallback(IntPtr csound, Int32 attr, string message)
        {
            Debug.Log($"[CSOUNDWORKER] {message} {attr}");
        }

        private void PrintMessages()
        {
            var msg = "[CSOUNDWORKER] ";
            while (GetCsoundMessageCount() > 0)
            {
                msg += GetCsoundMessage();
            }
            Debug.Log(msg);
        }

        internal GCHandle SetMessageCallback(MessageStrCallbackProxy callback)
        {
            var gch = FreezeCallbackInHeap(callback);
            NativeMethods.csoundSetMessageStringCallback(csound, callback);
            return gch;
        }

        internal GCHandle FreezeCallbackInHeap(Delegate callback)
        {
            var name = callback.Method.Name;
            if (!m_callbacks.ContainsKey(name)) m_callbacks.Add(name, GCHandle.Alloc(callback));
            return m_callbacks[name];
        }

        #endregion Private Helpers
    }
}
