#!/bin/bash
# Copyright (C) 2015 Rory Walsh.
#
# This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.

# build_midi_jar.sh
# Compiles CsoundMidiPlugin.java into CsoundUnityMidi.aar and copies it to
# the package's Plugins/Android folder.
#
# An AAR (Android Archive) is used instead of a plain JAR to avoid the
# "duplicate class" Gradle error (AGP 8+) that occurs when a JAR is resolved
# both as a library runtime artifact and as an external dependency.
#
# Requirements:
#   - JDK 8+ (javac, jar)
#   - Android SDK with at least one platform installed (API 23+)
#   - Unity with Android Build Support installed
#   - zip (pre-installed on macOS/Linux)
#
# Usage:
#   ./tools/android/build_midi_jar.sh
# Run from the package root (Packages/CsoundUnity/).

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PACKAGE_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# ── Locate Android SDK ────────────────────────────────────────────────────────

ANDROID_SDK="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
ANDROID_JAR=$(find "$ANDROID_SDK/platforms" -name "android.jar" | sort -V | tail -1)

if [ -z "$ANDROID_JAR" ]; then
    echo "Error: android.jar not found. Set ANDROID_HOME or install Android SDK."
    exit 1
fi
echo "Using Android SDK: $ANDROID_JAR"

# ── Locate Unity classes.jar ──────────────────────────────────────────────────

UNITY_HUB_EDITORS="/Applications/Unity/Hub/Editor"
UNITY_CLASSES_JAR=$(find "$UNITY_HUB_EDITORS" -name "classes.jar" -path "*/il2cpp/Release/*" 2>/dev/null | sort -V | tail -1)

if [ -z "$UNITY_CLASSES_JAR" ]; then
    echo "Error: Unity classes.jar not found under $UNITY_HUB_EDITORS"
    exit 1
fi
echo "Using Unity classes.jar: $UNITY_CLASSES_JAR"

# ── Compile ───────────────────────────────────────────────────────────────────

BUILD_DIR="$(mktemp -d)"
trap "rm -rf $BUILD_DIR" EXIT

javac --release 8 \
    -classpath "$ANDROID_JAR:$UNITY_CLASSES_JAR" \
    "$SCRIPT_DIR/CsoundMidiPlugin.java" \
    -d "$BUILD_DIR"

jar cf "$BUILD_DIR/classes.jar" -C "$BUILD_DIR" com/

# ── Package as AAR ────────────────────────────────────────────────────────────
# An AAR is a ZIP containing at minimum AndroidManifest.xml and classes.jar.
# AGP deduplicates AARs correctly; plain JARs can be merged twice (AGP 8+).

cat > "$BUILD_DIR/AndroidManifest.xml" << 'MANIFEST'
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
    package="com.csound.unity" />
MANIFEST

(cd "$BUILD_DIR" && zip -j CsoundUnityMidi.aar AndroidManifest.xml classes.jar)

# ── Copy to package ───────────────────────────────────────────────────────────

DEST="$PACKAGE_ROOT/Runtime/Plugins/Android"
mkdir -p "$DEST"
# Remove any leftover plain JAR to prevent duplicate class errors
rm -f "$DEST/CsoundUnityMidi.jar"
cp "$BUILD_DIR/CsoundUnityMidi.aar" "$DEST/"

echo "Done: $DEST/CsoundUnityMidi.aar"
