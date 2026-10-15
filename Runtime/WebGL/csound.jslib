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
        nodes: {},       // id -> the instance's AudioNode: cs.start() returns it only in the
                         //       single-threaded variation, elsewhere it is fetched on first use
        contexts: {},    // id -> the instance's AudioContext, in every variation
        rtInput: {},     // id -> true when Csound opens the microphone itself at start()
        awaitingGesture: {}, // id -> true while start() waits for the page's first gesture
        started: {},     // id -> true once cs.start() has resolved
        inputStreams: {} // id -> { stream: MediaStream, source: MediaStreamAudioSourceNode }
    },

    csoundInitialize: async function (id, flags, csdTextPtr, filesToLoadTextPtr, initialChannelsPtr, callback) {

        // copy URL to local file in Csound's virtual filesystem
        async function copyUrlToLocal(csound, src, dest) {
            console.log("[CsoundUnity] fetching " + src);
            let srcfile = await fetch(src, {cache: "no-store"});

            // A 404 answers with a page, and writing that page under the name Csound expects is
            // worse than not writing it: the opcode that reads it gets a few hundred bytes of HTML
            // instead of data, and says something that has nothing to do with the missing file.
            if (!srcfile.ok) {
                console.warn("[CsoundUnity] " + src + " not found (HTTP " + srcfile.status +
                             "), skipped. Csound will report it missing if the csd asks for it.");
                return;
            }

            let dat = await srcfile.arrayBuffer();
            console.log("[CsoundUnity] fetched src: " + src + " dat length: " + dat.byteLength);
            await csound.fs.writeFile(dest, new Uint8Array(dat));
            console.log("[CsoundUnity] finished writing file to " + dest);
        };

        // Resolves once the context is running: at once if it already is, otherwise on the page's
        // first real gesture. A browser starts no audio before one, and a context created earlier is
        // born suspended. resume() has to be called from inside the gesture handler, and it can
        // still be refused, so the listeners stay on until the state actually changes — which is
        // also watched directly, for the case where something else resumes the context for us.
        function contextRunning(ctx) {
            if (!ctx || ctx.state === "running") return Promise.resolve();

            console.log("[CsoundUnity] the browser allows no audio yet: Csound will start on the " +
                        "page's first click, tap or key press.");

            return new Promise(function (resolve) {
                const gestures = ["pointerdown", "touchend", "keydown"];
                const done = function () {
                    gestures.forEach(g => document.removeEventListener(g, onGesture, true));
                    ctx.removeEventListener("statechange", onState);
                    console.log("[CsoundUnity] audio context running, starting Csound");
                    resolve();
                };
                const onState = function () { if (ctx.state === "running") done(); };
                const onGesture = function () {
                    ctx.resume().then(
                        () => { if (ctx.state === "running") done(); },
                        e  => console.warn("[CsoundUnity] could not resume the audio context: " + e));
                };
                ctx.addEventListener("statechange", onState);
                gestures.forEach(g => document.addEventListener(g, onGesture, true));
            });
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
        const initialChannels = UTF8ToString(initialChannelsPtr);

        console.log(`[CsoundUnity] starting Csound 7 id=${id} variation="${variation.name}"`);
        const cs = await Csound(variation);

        for (const element of filesArray) {
            if (!element) continue;
            const name = element.substring(element.lastIndexOf('/') + 1);
            await copyUrlToLocal(cs, element, "./" + name);
        }

        console.log(`[CsoundUnity] Csound version: ${cs.name}`);

        // A csd that names no output renders to Csound's dummy interface and a virtual test.wav
        // that nobody can hear, so -odac is the default here. Only a default: an option set
        // before compileCSD takes precedence over <CsOptions>, so a csd that deliberately writes
        // to a file keeps doing it. Comments are stripped first, or a commented-out -o would
        // count as a declaration.
        const optionsBlock = /<CsOptions>([\s\S]*?)<\/CsOptions>/i.exec(csdText);
        const csOptions = optionsBlock ? optionsBlock[1].replace(/;[^\n]*/g, "") : "";
        if (!/(^|\s)(-o\S*|--output\b)/.test(csOptions)) {
            console.log("[CsoundUnity] the csd declares no output, defaulting to -odac");
            await cs.setOption("-odac");
        }

        // The same for the input, so that a csd reading adc or inch gets a device to read from.
        const inputChannels = /^\s*nchnls_i\s*=\s*(\d+)/m.exec(csdText);
        const wantsInput = inputChannels && parseInt(inputChannels[1], 10) > 0;
        if (wantsInput && !/(^|\s)(-i\S*|--input\b)/.test(csOptions)) {
            console.log(`[CsoundUnity] the csd reads ${inputChannels[1]} input channels, defaulting to -iadc`);
            await cs.setOption("-iadc");
        }

        // -n means "open no audio device", which is what a csd written for the native input path
        // wants: there the host drives performKsmps and fills spin itself. In the browser Csound
        // owns the device, and under -n it renders to the dummy interface and its worklet is built
        // with no inputs at all — a microphone stream connected to that crashes it on the first
        // block. So the flag is dropped from the copy that gets compiled, and the csd on disk,
        // which still has to serve the native path, is left alone.
        let compiledCsd = csdText;
        if (optionsBlock && /(^|\s)(-n|--nosound)(\s|$)/.test(csOptions)) {
            const inner = optionsBlock[1];
            const start = optionsBlock.index + optionsBlock[0].indexOf(inner);
            compiledCsd = csdText.slice(0, start) +
                          inner.replace(/(^|\s)(-n|--nosound)(?=\s|$)/g, "$1") +
                          csdText.slice(start + inner.length);
            console.log("[CsoundUnity] dropped -n: in the browser Csound opens the device itself");
        }

        await cs.compileCSD(compiledCsd);

        // The context is asked of the instance, not taken from the node: the worker variations
        // return no node, and it is read once and kept because getAudioContext is async while the
        // state check called from C# is a plain function.
        const csContext = await cs.getAudioContext();

        // The gesture is a precondition of start(), not a race against it. Started on a suspended
        // context, the library's start promise times out and then resolves with no node: Csound
        // stays up for the whole session with nothing connected and, for a csd that reads input, no
        // microphone — and nobody retries. Waiting here means initialization completes when audio
        // can really run, so a scene needs no click-to-start gate of its own, and the permission
        // prompt appears after the click instead of before it.
        // The control channels the csd declares, with the values the inspector holds, set before the
        // first performance pass rather than after it. Csound starts performing the moment start()
        // returns, and an instrument that reads a channel at init time, which is what i-rate chnget
        // is, has already read it by the time C# gets its callback. Natively the question does not
        // arise: there the host drives performKsmps and nothing runs until it does.
        if (initialChannels) {
            for (const pair of initialChannels.split("\n")) {
                const eq = pair.lastIndexOf("=");
                if (eq <= 0) continue;
                const value = parseFloat(pair.slice(eq + 1));
                if (!isFinite(value)) continue;
                await cs.setControlChannel(pair.slice(0, eq), value);
            }
        }

        // The only answer C# has during the wait: until start() returns there is no registered
        // instance to ask, so the flag is keyed by the id C# passed in. Without it a scene cannot
        // tell "still loading", which lasts a few frames everywhere, from "the browser is waiting
        // for a click", which lasts until there is one.
        if (csContext && csContext.state !== "running") CsoundRef.awaitingGesture[id] = true;
        try {
            await contextRunning(csContext);
        } finally {
            delete CsoundRef.awaitingGesture[id];
        }

        const csNode = await cs.start();

        CsoundRef.instances[CsoundRef.uniqueIdCounter] = cs;
        CsoundRef.nodes[CsoundRef.uniqueIdCounter] = csNode;
        CsoundRef.contexts[CsoundRef.uniqueIdCounter] = csContext;
        CsoundRef.rtInput[CsoundRef.uniqueIdCounter] = !!wantsInput;
        CsoundRef.started[CsoundRef.uniqueIdCounter] = true;
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

        // Guarded like destroy below: @csound/browser does not ship cleanup, so an unguarded
        // call throws and leaves destroy and the bookkeeping underneath it unreached.
        cs.cleanup && (await cs.cleanup());
        // Csound 7: destroy frees the WASM memory
        cs.destroy && (await cs.destroy());
        delete CsoundRef.instances[uniqueId];
        delete CsoundRef.nodes[uniqueId];
        delete CsoundRef.contexts[uniqueId];
        delete CsoundRef.rtInput[uniqueId];
        delete CsoundRef.started[uniqueId];
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
        const cs = CsoundRef.instances[uniqueId];
        if (!cs) {
            console.warn("[CsoundUnity] csoundAudioInputEnable: instance " + uniqueId + " not ready.");
            Module['dynCall_vii'](callbackPtr, uniqueId, 0);
            return;
        }

        // An orchestra that reads input already has a microphone: @csound/browser opens one inside
        // start(), and in the worker variations it refuses to do it any later. Opening a second
        // stream here and wiring it to the same node by hand is what crashes the worklet, so when
        // Csound owns the microphone this call only reports that it is already on.
        if (CsoundRef.rtInput[uniqueId]) {
            console.log("[CsoundUnity] Csound opened the microphone itself for instance " + uniqueId + ".");
            Module['dynCall_vii'](callbackPtr, uniqueId, 1);
            return;
        }

        console.warn("[CsoundUnity] csoundAudioInputEnable: the csd of instance " + uniqueId +
                     " declares no input channels. Add nchnls_i and read adc or inch, and Csound " +
                     "opens the microphone on its own.");

        // The node the microphone connects to. cs.start() returns it only in the single-threaded
        // variation; the worker ones create it afterwards and hand it over through getNode.
        let node = CsoundRef.nodes[uniqueId];
        if (!node) {
            try { node = await cs.getNode(); } catch (e) { node = null; }
            CsoundRef.nodes[uniqueId] = node;
        }
        if (!node) {
            console.warn("[CsoundUnity] csoundAudioInputEnable: instance " + uniqueId +
                         " has no audio node to connect the microphone to.");
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
        // The channel count is a preference, not a requirement, but some browsers still refuse the
        // whole request over it. A refusal that names a constraint is therefore retried with nothing
        // asked for but audio, which is the stereo-or-mono default every browser can give.
        let stream = null;
        try {
            stream = await navigator.mediaDevices.getUserMedia(constraints);
        } catch (e) {
            console.warn("[CsoundUnity] getUserMedia refused " + e.name +
                         (e.constraint ? " over the '" + e.constraint + "' constraint" : "") +
                         "; asking for the default input instead.");
            try {
                stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            } catch (e2) {
                console.error("[CsoundUnity] getUserMedia failed: " + e2.name + " — " + e2.message);
                Module['dynCall_vii'](callbackPtr, uniqueId, 0);
                return;
            }
        }

        try {
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
            console.error("[CsoundUnity] could not connect the microphone to Csound: " + e);
            try { stream.getTracks().forEach(t => t.stop()); } catch (stopError) {}
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
    },

    csoundCompileOrc: async function(uniqueId, orchestra) {
        var orc = UTF8ToString(orchestra);
        var res = await CsoundRef.instances[uniqueId].compileOrc(orc);
        return res;
    },

    csoundSetStringChannel: async function(uniqueId, channel, value) {
        var name = UTF8ToString(channel);
        var val  = UTF8ToString(value);
        await CsoundRef.instances[uniqueId].setStringChannel(name, val);
    },

    /// Whether this instance's audio is actually running. A page that has had no user gesture
    /// keeps its AudioContext suspended, which is the usual reason a Csound that started, compiled
    /// and allocated its instruments makes no sound at all.
    csoundIsAudioContextRunning: function(uniqueId) {
        // The context, never the node: the worker variations return no node from cs.start(), so a
        // missing node there says nothing about whether audio is running.
        var ctx = CsoundRef.contexts[uniqueId];
        if (!ctx) return 0;
        return ctx.state === "running" ? 1 : 0;
    },

    /// Whether this instance is holding its start() until the page's first click, tap or key press.
    /// False both before Csound has been created and once it is running, so it answers exactly one
    /// question: is the browser the thing we are waiting for.
    csoundIsWaitingForUserGesture: function(instanceId) {
        return CsoundRef.awaitingGesture[instanceId] ? 1 : 0;
    }
}

autoAddDeps(csoundModule, '$CsoundRef');
mergeInto(LibraryManager.library, csoundModule);
