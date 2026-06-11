/*

C S O U N D WebGL module (csound.jslib)

Javascript wrapper for Csound 7 via the Csound API
and is licensed under the same terms and disclaimers as Csound described below.

Copyright (C) 2024 Rory Walsh, Giovanni Bedetti

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Written by Giovanni Bedetti, July 2024
Updated for Csound 7 (@csound/browser 7.0.0), June 2026

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

var csoundModule = {

    $CsoundRef: {
        uniqueIdCounter: 0,
        instances: {},   // id -> CsoundObj
        nodes: {},       // id -> AudioNode returned by cs.start()
        inputStreams: {} // id -> { stream: MediaStream, source: MediaStreamAudioSourceNode }
    },

    csoundInitialize: async function (id, flags, csdTextPtr, filesToLoadTextPtr, callback) {

        // copy URL to local file in Csound's virtual filesystem
        async function copyUrlToLocal(csound, src, dest) {
            console.log("[CsoundUnity] fetching " + src);
            let srcfile = await fetch(src, {cache: "no-store"});
            let dat = await srcfile.arrayBuffer();
            console.log("[CsoundUnity] fetched src: " + src + " dat length: " + dat.byteLength);
            await csound.fs.writeFile(dest, new Uint8Array(dat));
            console.log("[CsoundUnity] finished writing file to " + dest);
        };

        // Csound 7 variation options (useSPN removed — ScriptProcessorNode dropped in Csound 7)
        const csoundVariations = [
            { useWorker: false,               name: "SINGLE THREAD, AudioWorklet" },
            { useWorker: true,  useSAB: true,  name: "WORKER, AudioWorklet, SAB" },
            { useWorker: true,  useSAB: false, name: "WORKER, AudioWorklet, MessagePort" },
        ];

        if (CsoundRef.instances[id] !== undefined) {
            console.log("[CsoundUnity] id already exists! aborting Csound creation with id " + id);
            return;
        }

        const variation = csoundVariations[Math.min(flags, csoundVariations.length - 1)];
        const csdText = UTF8ToString(csdTextPtr);
        const filesToLoad = UTF8ToString(filesToLoadTextPtr);
        const filesArray = filesToLoad.split(":");

        console.log(`[CsoundUnity] starting Csound 7 id=${id} variation="${variation.name}"`);
        const cs = await Csound(variation);

        for (const element of filesArray) {
            if (!element) continue;
            const name = element.substring(element.lastIndexOf('/') + 1);
            await copyUrlToLocal(cs, element, "./" + name);
        }

        console.log(`[CsoundUnity] Csound version: ${cs.name}`);
        await cs.compileCSD(csdText);
        const csNode = await cs.start();

        CsoundRef.instances[CsoundRef.uniqueIdCounter] = cs;
        CsoundRef.nodes[CsoundRef.uniqueIdCounter] = csNode;
        const uniqueId = CsoundRef.uniqueIdCounter;
        CsoundRef.uniqueIdCounter++;
        console.log(`[CsoundUnity] created Csound with uniqueId: ${uniqueId}`);
        Module['dynCall_vi'](callback, uniqueId);
    },

    csoundGetChannel: async function (uniqueId, channelPtr, callback) {
        var channel = UTF8ToString(channelPtr);
        if (CsoundRef.instances[uniqueId] === undefined) return;
        try {
            var value = await CsoundRef.instances[uniqueId].getControlChannel(channel);
            var strBufferSize = lengthBytesUTF8(channel) + 1;
            var strBuffer = _malloc(strBufferSize);
            stringToUTF8(channel, strBuffer, strBufferSize);
            Module['dynCall_viif'](callback, uniqueId, [strBuffer], value);
        } catch (error) {
            console.error(`[CsoundUnity] [id: ${+uniqueId}] Error retrieving channel ${channel}, error: ${error}`);
        }
    },

    csoundSetChannel: async function (uniqueId, channelPtr, value) {
        CsoundRef.instances[uniqueId].setControlChannel(UTF8ToString(channelPtr), value);
    },

    csoundStop: async function(uniqueId, callback) {
        const cs = CsoundRef.instances[uniqueId];
        if (!cs) return;

        // Disconnect microphone if open
        const inEntry = CsoundRef.inputStreams[uniqueId];
        if (inEntry) {
            try { inEntry.source.disconnect(); } catch (e) {}
            try { inEntry.stream.getTracks().forEach(t => t.stop()); } catch (e) {}
            delete CsoundRef.inputStreams[uniqueId];
        }

        await cs.cleanup();
        // Csound 7: destroy frees WASM memory; cleanup alone stops the performance
        cs.destroy && (await cs.destroy());
        delete CsoundRef.instances[uniqueId];
        delete CsoundRef.nodes[uniqueId];
        Module['dynCall_vi'](callback, uniqueId);
    },

    csoundReset: async function(uniqueId) {
        CsoundRef.instances[uniqueId] && CsoundRef.instances[uniqueId].reset();
    },

    // ── MIDI ──────────────────────────────────────────────────────────────────

    /// Sends a raw MIDI message (3 bytes) to the Csound WASM instance.
    /// Can be called from C# at any time after initialization.
    csoundSendMidiMessage: function(uniqueId, b0, b1, b2) {
        const cs = CsoundRef.instances[uniqueId];
        if (!cs) return;
        cs.midiMessage(b0, b1, b2);
    },

    /// Connects all available Web MIDI inputs to Csound and optionally notifies a
    /// Unity GameObject via UnitySendMessage so C# code can react to incoming MIDI.
    /// gameObjectNamePtr / methodNamePtr: pass empty string to skip C# notification
    /// (raw MIDI-to-Csound routing still happens).
    csoundMidiEnable: async function(uniqueId, gameObjectNamePtr, methodNamePtr) {
        const cs = CsoundRef.instances[uniqueId];
        if (!cs) {
            console.warn("[CsoundUnity] csoundMidiEnable: instance " + uniqueId + " not found.");
            return;
        }
        if (!navigator.requestMIDIAccess) {
            console.warn("[CsoundUnity] Web MIDI API not supported in this browser (Chrome/Edge required).");
            return;
        }
        const gameObjectName = UTF8ToString(gameObjectNamePtr);
        const methodName     = UTF8ToString(methodNamePtr);
        try {
            const access = await navigator.requestMIDIAccess();
            function connectInput(input) {
                input.onmidimessage = function(evt) {
                    const d  = evt.data;
                    const b0 = d[0] || 0;
                    const b1 = d.length > 1 ? d[1] : 0;
                    const b2 = d.length > 2 ? d[2] : 0;
                    // Route raw MIDI bytes directly into Csound WASM
                    cs.midiMessage(b0, b1, b2);
                    // Forward to C# if a Unity GameObject / method was given
                    if (gameObjectName && methodName) {
                        SendMessage(gameObjectName, methodName, b0 + "," + b1 + "," + b2);
                    }
                };
            }
            access.inputs.forEach(connectInput);
            // Auto-connect newly plugged-in devices
            access.onstatechange = function(e) {
                if (e.port.type === "input" && e.port.state === "connected")
                    connectInput(e.port);
            };
            console.log("[CsoundUnity] Web MIDI enabled, " + access.inputs.size + " input(s) connected.");
        } catch (e) {
            console.error("[CsoundUnity] MIDI access denied or unavailable:", e);
        }
    },

    // ── AUDIO INPUT ───────────────────────────────────────────────────────────

    /// Opens microphone access via getUserMedia and connects the stream to the
    /// Csound AudioWorklet node. The CSD must declare nchnls_i >= 1 and use adc.
    /// deviceIdPtr  : browser deviceId string from enumerateDevices, or empty for default.
    /// channelCount : number of input channels to request (ideal hint — browsers cap at 2 in practice).
    /// callbackPtr  : void(int instanceId, int success) — called with success=1 on success.
    ///
    /// BROWSER LIMITATION: getUserMedia is capped at stereo (2 channels) on all current
    /// browsers, regardless of the connected hardware. Unlike CoreAudio / AAudio / WASAPI,
    /// the Web Audio API does not expose multi-channel audio interfaces. CSD opcodes that
    /// read nchnls_i > 2 will receive silence on the extra channels.
    csoundAudioInputEnable: async function(uniqueId, deviceIdPtr, channelCount, callbackPtr) {
        const cs   = CsoundRef.instances[uniqueId];
        const node = CsoundRef.nodes[uniqueId];
        if (!cs || !node) {
            console.warn("[CsoundUnity] csoundAudioInputEnable: instance " + uniqueId + " not ready.");
            Module['dynCall_vii'](callbackPtr, uniqueId, 0);
            return;
        }
        // Disconnect any existing stream first
        const prev = CsoundRef.inputStreams[uniqueId];
        if (prev) {
            try { prev.source.disconnect(); } catch (e) {}
            try { prev.stream.getTracks().forEach(t => t.stop()); } catch (e) {}
            delete CsoundRef.inputStreams[uniqueId];
        }
        const deviceId = UTF8ToString(deviceIdPtr);
        // channelCount is an ideal hint: browsers silently cap it at 2 (stereo).
        const audioConstraints = { channelCount: { ideal: channelCount } };
        if (deviceId) audioConstraints.deviceId = { exact: deviceId };
        const constraints = { audio: audioConstraints };
        if (channelCount > 2) {
            console.warn("[CsoundUnity] getUserMedia: requested " + channelCount + " channels, " +
                         "but browsers cap audio input at 2 (stereo). Extra channels will be silent.");
        }
        try {
            const stream   = await navigator.mediaDevices.getUserMedia(constraints);
            const audioCtx = await cs.getAudioContext();
            const source   = audioCtx.createMediaStreamSource(stream);
            // Upmix / downmix happens automatically inside the Web Audio graph.
            source.connect(node);
            const actualChannels = stream.getAudioTracks()[0]
                ? stream.getAudioTracks()[0].getSettings().channelCount || "unknown"
                : "unknown";
            CsoundRef.inputStreams[uniqueId] = { stream: stream, source: source };
            console.log("[CsoundUnity] Microphone connected to Csound AudioWorklet (id=" + uniqueId +
                        ", actual channels=" + actualChannels + ").");
            Module['dynCall_vii'](callbackPtr, uniqueId, 1);
        } catch (e) {
            console.error("[CsoundUnity] getUserMedia failed:", e);
            Module['dynCall_vii'](callbackPtr, uniqueId, 0);
        }
    },

    /// Disconnects the microphone stream from the Csound AudioWorklet and stops
    /// all audio tracks. Safe to call when no stream is open.
    csoundAudioInputDisable: function(uniqueId) {
        const entry = CsoundRef.inputStreams[uniqueId];
        if (!entry) return;
        try { entry.source.disconnect(); } catch (e) {}
        try { entry.stream.getTracks().forEach(t => t.stop()); } catch (e) {}
        delete CsoundRef.inputStreams[uniqueId];
        console.log("[CsoundUnity] Microphone disconnected (id=" + uniqueId + ").");
    },

    csoundGetTable: async function(uniqueId, tableId, callback) {
        var table = await CsoundRef.instances[uniqueId].getTable(tableId);
        console.log("table len: "+ table.length + ": " + table + "\nBYTES_PER_ELEMENT: " + table.BYTES_PER_ELEMENT);
        var buf = _malloc(table.length * table.BYTES_PER_ELEMENT);
        Module.HEAPF64.set(table, buf >> 3);
        Module['dynCall_viii'](callback, uniqueId, table.length, buf);
    },

    csoundSetOption: async function(uniqueId, option, callback) {
        var opt = UTF8ToString(option);
        var res = await CsoundRef.instances[uniqueId].setOption(opt);
        Module['dynCall_vii'](callback, uniqueId, res);
    },

    csoundInputMessage: async function(uniqueId, scoreEvent) {
        var event = UTF8ToString(scoreEvent);
        var res = await CsoundRef.instances[uniqueId].inputMessage(event);
        return res;
    }
}

autoAddDeps(csoundModule, '$CsoundRef');
mergeInto(LibraryManager.library, csoundModule);
