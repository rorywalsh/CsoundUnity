## CsoundFileWatcher ##

From version 3.0 CsoundUnity can detect the changes made to the *csd* file while modifying it in an external editor.  
You will see in the Unity Console messages like this:

```
[CsoundFileWatcher] Updating csd: sfload.csd in GameObject: Csound
```

when this happens.
This means that you don't need to drag the Csd Asset in CsoundUnity everytime you make a change on it in an external editor.

---

### What a save costs, and the switch for it ###

Reading a csd's channels means **running it**: the channels exist once the instruments have
initialised, not once the orchestra has compiled, so the editor compiles the csd into a throwaway
Csound instance and performs one `ksmps` of it. That is how the inspector knows a channel you have
just added.

It also means that a csd Csound cannot survive takes the Unity editor with it. Csound crashes on
some orchestras that compile perfectly well — a variable named after an opcode is enough — and a
crash inside a native library cannot be caught: the process goes, and an unsaved scene with it.

So when the watcher sees a save, the scan that follows is work the editor does on its own, after
a keystroke in another window. **Settings ▸ Scan On External Save** turns that off while you are
writing a csd elsewhere, and leaves the two deliberate paths alone: assigning a csd and the **↺**
button always scan. It is an editor preference, which Unity keeps per user rather than per project:
it holds for every CsoundUnity, in every project opened with that editor.

Turning it off does not freeze the component: the save still updates the csd, its widgets and the
audio channels the text declares, because reading those takes no Csound. What waits for the **↺**
button is what only a running orchestra can tell us — a channel an instrument creates when it
initialises, and the cross-check between the Cabbage block and Csound's own channel list.

Two scans never run at once, whichever instance asked for them, and the console numbers them
(`[CsoundWorker Scan #3]`). Saving before a refresh is still worth the habit.

---

### When the watcher does not fire: the refresh button ###

The widget data — channel names, ranges, combobox options, labels — is **parsed once and then
serialized on the component**. That is what lets the inspector show your controls without Csound
running, and what lets your channel values survive a scene reload.

The consequence is that the inspector reflects the csd *as it was parsed*, not as it is on disk
right now. The file watcher covers the usual case, but it cannot see a change that happened while
Unity was not running — a `git pull`, a branch switch, a file restored from backup.

When the inspector and the csd disagree, press the **↺** button next to the Csd Asset field. It
re-parses the file and rebuilds the channel list.

The symptom is easy to misread, because nothing errors: a widget you added does not appear, a
range you widened still clamps to the old one, or a combobox you extended still offers the old
options. If a control looks stale, refresh before looking for a bug.
