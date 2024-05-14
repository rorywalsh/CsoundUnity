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
        bool compiledOk = false;
        Thread performance;

        private static string _csdEmptyTemplate =
            "<CsoundSynthesizer>\n" +
            // "<CsOptions>\n" +
            // "- n - d\n" +
            // "</CsOptions>\n" +
            "<CsInstruments>\n" +
            "0dbfs = 1\n" +
            "ksmps = 1\n\n" +
            "instr 9999\n" +
            //"printks \"alive\", .1\n" +
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

        public CsoundWorker() : base()
        {
            Debug.Log("CsoundWorker construction");
            IsInitialized = false;

            if (Application.platform == RuntimePlatform.WindowsPlayer)
                NativeMethods.csoundSetOpcodedir(".");

            NativeMethods.csoundInitialize(0);
            csound = NativeMethods.csoundCreate(System.IntPtr.Zero);
            if (csound == null)
            {
                Debug.LogError("Couldn't create Csound!");
                return;
            }

            NativeMethods.csoundSetHostImplementedAudioIO(csound, 1, 0);
            NativeMethods.csoundCreateMessageBuffer(csound, 0);
            SetMessageCallback(RawMessageCallback);
            NativeMethods.csoundSetOption(csound, "-n");
            NativeMethods.csoundSetOption(csound, "-d");

            var parms = GetParams();
            parms.control_rate_override = AudioSettings.outputSampleRate;
            parms.sample_rate_override = AudioSettings.outputSampleRate;
            SetParams(parms);

            int ret = NativeMethods.csoundCompileCsdText(csound, _csdEmptyTemplate);
            NativeMethods.csoundStart(csound);
            var compiledOk = ret == 0;
            Debug.Log($"CsoundWorker created and started. CsoundCompile: {compiledOk}\n" +
                $"AudioSettings.outputSampleRate: {AudioSettings.outputSampleRate}\n" +
                $"GetSr: {GetSr()}\n" +
                $"GetKr: {GetKr()}\n" +
                $"Get0dbfs: {Get0dbfs()}\n" +
                $"GetKsmps: {GetKsmps()}");

            // Create a new thread and start it
            performance = new Thread(PerformanceThread);
            performance.Start();
            _running = true;

            IsInitialized = true;
        }

        //~CsoundWorker()
        //{
        //    base.OnApplicationQuit();
        //    Dispose();
        //}

        protected bool m_disposed = false;

        protected virtual void Dispose()
        {
            if (!m_disposed)
            {
                Debug.Log("Disposing");
                
                //ReleaseProtectedPointer(NativeMethods.csoundGetHostData(csound));

                //dispose of unmanaged resources
                if (m_callbacks != null)
                {
                    foreach (GCHandle gch in m_callbacks.Values) gch.Free();
                    m_callbacks.Clear();
                    m_callbacks = null;
                }

                Debug.Log("Joining CsoundWorker thread");
                performance.Join();
                performance = null;

                m_disposed = true;
                Debug.Log("CsoundWorker Disposed");
                //GC.SuppressFinalize(this);
            }
        }

        protected void ReleaseProtectedPointer(IntPtr pgcData)
        {
            if ((pgcData != null) && (pgcData != IntPtr.Zero))
            {
                GCHandle gcData = GCHandle.FromIntPtr(pgcData);
                gcData.Free();
            }
        }

        public void PerformanceThread()
        {
            while (_running)
            {
                try
                {
                    NativeMethods.csoundPerform(csound);
                }
                catch (ThreadAbortException)
                {
                    Debug.Log("Thread aborted");
                }
            }
            // Delay the thread for a certain amount of time
            //Thread.Sleep((int)(1f / GetKr() * 1000f)); 
            //}
        }

        public static CsoundWorker Create()
        {
            return new CsoundWorker();
        }

        int nOperation = 1;

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
            {
                //SendScoreEvent("i100, 0, 1");
                //return true;
                return ScheduleInstrument(nOperation, 0, 1) == 0;
            }

            var instr = string.Format(_saveFileInstrTemplate, nOperation, destination, format);
            Debug.Log($"SaveAudioFile instr:\n{instr}");
            var res = CompileOrc(instr);

            if (res == 0)
            {
                // avoid duplication of instruments with the same number
                _createdInstruments.Add(nOperation, "SaveFile");
                nOperation++;
            }
            //SendScoreEvent("i100 0 1");
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
                // copy samples to the existing table
                TableCopyIn(tableNumber, samples);
                return 0;
            }

            var resTable = CreateTableInstrument(tableNumber, samples.Length);
            if (resTable != 0)
                return -1;

            _createdTables.Add(tableNumber);

            // copy samples to the newly created table
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
            string createTableInstrument = string.Format(@"gisampletable{0} ftgen {0}, 0, {1}, -7, 0, 0", tableNumber, -tableLength);
            Debug.Log($"orc to create table {tableNumber}, length: {tableLength}: \n" + createTableInstrument);
            var res = CompileOrc(createTableInstrument);
            return res;
        }

        private void RawMessageCallback(IntPtr csound, Int32 attr, string message)
        {
            Debug.Log($"[CSOUNDWORKER] {message} {attr}");

           // PrintMessages();
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
            GCHandle gch = FreezeCallbackInHeap(callback);
            NativeMethods.csoundSetMessageStringCallback(csound, callback);
            return gch;
        }

        private IDictionary<string, GCHandle> m_callbacks = new Dictionary<string, GCHandle>();

        internal GCHandle FreezeCallbackInHeap(Delegate callback)
        {
            string name = callback.Method.Name;
            if (!m_callbacks.ContainsKey(name)) m_callbacks.Add(name, GCHandle.Alloc(callback));
            return m_callbacks[name];
        }

        public void Destroy()
        {
            OnApplicationQuit();
        }

        public override void OnApplicationQuit()
        {
            _running = false;
            Debug.Log("Worker OnApplicationQuit");
            base.OnApplicationQuit();
            Dispose();
        }
    }
}