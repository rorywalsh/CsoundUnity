/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

package com.csound.unity;

import android.content.Context;
import android.media.midi.MidiDevice;
import android.media.midi.MidiDeviceInfo;
import android.media.midi.MidiManager;
import android.media.midi.MidiOutputPort;
import android.media.midi.MidiReceiver;
import android.os.Handler;
import android.os.Looper;
import com.unity3d.player.UnityPlayer;

import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

/**
 * CsoundMidiPlugin — Android MIDI input bridge for CsoundUnity.
 *
 * Opens all connected MIDI output ports (USB and BLE devices appear here
 * transparently once paired via Android's MIDI service) and forwards raw
 * MIDI bytes to a Unity GameObject via UnitySendMessage.
 *
 * Requires android.permission.MIDI and minSdkVersion 23 (Android 6.0+).
 */
public class CsoundMidiPlugin {

    private final MidiManager midiManager;
    private final String gameObjectName;
    private final List<MidiDevice> openDevices = new ArrayList<>();
    private final List<MidiOutputPort> openPorts = new ArrayList<>();

    public CsoundMidiPlugin(String gameObjectName) {
        this.gameObjectName = gameObjectName;
        Context context = UnityPlayer.currentActivity;
        this.midiManager = (MidiManager) context.getSystemService(Context.MIDI_SERVICE);
    }

    /** Opens all currently connected MIDI devices and starts receiving. */
    public void open() {
        if (midiManager == null) return;
        MidiDeviceInfo[] devices = midiManager.getDevices();
        for (MidiDeviceInfo info : devices) {
            openDevice(info);
        }
    }

    private void openDevice(final MidiDeviceInfo info) {
        midiManager.openDevice(info, new MidiManager.OnDeviceOpenedListener() {
            @Override
            public void onDeviceOpened(MidiDevice device) {
                if (device == null) return;
                openDevices.add(device);
                for (int i = 0; i < info.getOutputPortCount(); i++) {
                    MidiOutputPort port = device.openOutputPort(i);
                    if (port != null) {
                        openPorts.add(port);
                        port.connect(new MidiReceiver() {
                            @Override
                            public void onSend(byte[] data, int offset, int count, long timestamp)
                                    throws IOException {
                                // Encode bytes as comma-separated string for UnitySendMessage.
                                StringBuilder sb = new StringBuilder();
                                for (int j = offset; j < offset + count; j++) {
                                    if (j > offset) sb.append(',');
                                    sb.append(data[j] & 0xFF);
                                }
                                UnityPlayer.UnitySendMessage(
                                        gameObjectName, "OnAndroidMidiMessage", sb.toString());
                            }
                        });
                    }
                }
            }
        }, new Handler(Looper.getMainLooper()));
    }

    /** Closes all open ports and devices. */
    public void close() {
        for (MidiOutputPort port : openPorts) {
            try { port.close(); } catch (Exception ignored) {}
        }
        openPorts.clear();
        for (MidiDevice device : openDevices) {
            try { device.close(); } catch (Exception ignored) {}
        }
        openDevices.clear();
    }
}
