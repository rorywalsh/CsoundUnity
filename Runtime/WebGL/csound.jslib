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
        instances: {}
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
        await cs.start();

        CsoundRef.instances[CsoundRef.uniqueIdCounter] = cs;
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
        await cs.cleanup();
        // Csound 7: destroy frees WASM memory; cleanup alone stops the performance
        cs.destroy && (await cs.destroy());
        delete CsoundRef.instances[uniqueId];
        Module['dynCall_vi'](callback, uniqueId);
    },

    csoundReset: async function(uniqueId) {
        CsoundRef.instances[uniqueId] && CsoundRef.instances[uniqueId].reset();
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
