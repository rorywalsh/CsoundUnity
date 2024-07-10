/*

C S O U N D WebGL module (csound.jslib)

Javascript wrapper for Csound 6 via the Csound API
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

var csoundModule = {

    $CsoundRef: {
        uniqueIdCounter: 0,
        instances: {}
    },

    csoundInitialize: async function (id, flags, csdTextPtr, filesToLoadTextPtr, callback) {
        //window.alert("csoundInitialize");

        // this is called by the open button
        // async function openf(sf) {
        //     // create an anchor element
        //     let a = document.createElement('a');
        //     // append it to html body
        //     document.body.appendChild(a);
        //     // set the anchor URL
        //     a.href = sf;
        //     // open in a different tab
        //     a.target = "_blank";
        //     // click on the element
        //     a.click();
        // }
        //
        // async function download(sf) {
        //     // create an anchor element
        //     let a = document.createElement('a');
        //     // append it to html body
        //     document.body.appendChild(a);
        //     // set the anchor URL
        //     a.href = sf;
        //     // set the download name
        //     a.download = "test.dat";
        //     // click on the element
        //     a.click();
        // }

        // copy file from local and return a URL for it
        async function copyUrlFromLocal(cs, src,t) {
            // get the file as a Uint8Array
            let data = await cs.fs.readFile(src);
            // create a data blob
            let destfile = new Blob([data.buffer], { type: t});
            // create a URL for it
            return window.URL.createObjectURL(destfile);
        }

        // copy URL to local file
        async function copyUrlToLocal(csound, src, dest) {
            console.log("fetching " + src)
            // fetch the file
            let srcfile = await fetch(src, {cache: "no-store"}); //, mode: "no-cors", crossorigin: "anonymous"})

            // get the file data as an array
            let dat = await srcfile.arrayBuffer();
            console.log("fetched src: " + src + " dat length: " + dat.byteLength)
            // write the data as a new file in the filesystem
            await csound.fs.writeFile(dest, new Uint8Array(dat));
            console.log("finished writing file to " + dest);
        };

        const csoundVariations = [
            { useWorker: false, useSPN: false, name: "SINGLE THREAD, AW" },
            { useWorker: false, useSPN: true, name: "SINGLE THREAD, SPN" },
            { useWorker: true, useSAB: true, name: "WORKER, AW, SAB" },
            { useWorker: true, useSAB: false, name: "WORKER, AW, Messageport" },
            { useWorker: true, useSAB: false, useSPN: true, name: "WORKER, SPN, MessagePort" },
        ];

        if (CsoundRef.instances[id] !== undefined)
        {
            // in case the id exists already, we could kill the existing instance and replace it with a new one
            // for now let's reject the operation
            console.log("id already exists! aborting Csound creation with id " + id);
            reject("existing id");
            return;
        }
        variation = csoundVariations[flags]
        csdText = UTF8ToString(csdTextPtr)
        //options = UTF8ToString(optionsPtr);
        var filesToLoad = UTF8ToString(filesToLoadTextPtr);
        //var filesToLoad = "./StreamingAssets/samples/hrtf-44100-left.dat:./StreamingAssets/samples/hrtf-44100-right.dat"
        var filesArray = filesToLoad.split(":");

        console.log(`starting to await for Csound id ${id} with flag: ${flags}`);// + " options: " + options)
        const cs = await Csound(variation);

        for (const element of filesArray) {
            var name = element.substring(element.lastIndexOf('/') + 1);
            await copyUrlToLocal(cs, element, "./" + name);
        }

        console.log(`Csound version: ${cs.name}`);
        const compileReturn = await cs.compileCsdText(csdText);
        const startReturn = await cs.start();
        //console.log(startReturn);
        CsoundRef.instances[CsoundRef.uniqueIdCounter] = cs;
        var uniqueId = CsoundRef.uniqueIdCounter;
        CsoundRef.uniqueIdCounter++;
        console.log(`uniqueId: ${uniqueId}, CsoundRef.uniqueIdCounter: ${CsoundRef.uniqueIdCounter}`);
        Module['dynCall_vi'](callback, uniqueId);
        //cs.terminateInstance && (await cs.terminateInstance());
    },
    
    csoundGetChannel: async function (uniqueId, channelPtr) {
        return CsoundRef.instances[uniqueId].getControlChannel(UTF8ToString(channelPtr));
    },

    csoundSetChannel: async function (uniqueId, channelPtr, value) {
        CsoundRef.instances[uniqueId].setControlChannel(UTF8ToString(channelPtr), value);
    },

    csoundStop: async function(uniqueId, callback) {

        //await CsoundRef.instances[uniqueId].stop();
        await CsoundRef.instances[uniqueId].cleanup();
        Module['dynCall_vi'](callback, uniqueId);
    },

    csoundReset: async function(uniqueId) {
        CsoundRef.instances[uniqueId].reset();
    },

    csoundGetTable: async function(uniqueId, tableId, callback) {
        var table = await CsoundRef.instances[uniqueId].getTable(tableId);
        console.log("table len: "+ table.length + ": " + table + "\nBYTES_PER_ELEMENT: " + table.BYTES_PER_ELEMENT)
        var buf = _malloc(table.length * table.BYTES_PER_ELEMENT);
        Module.HEAPF64.set(table, buf >> 3);
        Module['dynCall_viii'](callback, uniqueId, table.length, buf);
    },

    csoundSetOption: async function(uniqueId, option, callback) {
        var opt = UTF8ToString(option);
        console.log("csoundSetOption for id: " + uniqueId + " option: " + opt)
        var res = await CsoundRef.instances[uniqueId].setOption(opt);
        console.log("csoundSetOption res: " + res + " option: " + opt);
        Module['dynCall_vii'](callback, uniqueId, res);
        //return res;
    },
    
    csoundInputMessage: async function(uniqueId, scoreEvent) {

        var event = UTF8ToString(scoreEvent);
        console.log("csoundInputMessage for id: " + uniqueId + " scoreEvent: " + event + " Csound: " + CsoundRef.instances[uniqueId])
        var res = await CsoundRef.instances[uniqueId].inputMessage(event);
        console.log("csoundInputMessage res: " + res + " scoreEvent: " + event)
        return res;
    }    
}

autoAddDeps(csoundModule, '$CsoundRef');
mergeInto(LibraryManager.library, csoundModule);
