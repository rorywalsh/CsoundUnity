## CsoundFileWatcher ##

From version 3.0 CsoundUnity can detect the changes made to the *csd* file while modifying it in an external editor.  
You will see in the Unity Console messages like this:

```
[CsoundFileWatcher] Updating csd: sfload.csd in GameObject: Csound
```

when this happens.
This means that you don't need to drag the Csd Asset in CsoundUnity everytime you make a change on it in an external editor.

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
