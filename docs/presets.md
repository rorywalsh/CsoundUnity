## Presets

New in **CsoundUnity** 3.4.0, it is now possible to save and load **CsoundUnity** presets.

Each preset refers to a csd file, to help keep things clear.  
It holds a list of **CsoundChannelControllers** with specific values, to be retrieved both at Editor time and at Runtime.  

To cover most of the user cases, there are 3 types of presets:

- **SCRIPTABLE OBJECT preset**: this is the type of preset that you will use while developing your application, and that you can give the user as built-in preset (meaning that the user won't be able to override them). 
It works both on the Editor and at Runtime.

- **JSON preset**: this is meant to be used at runtime, when you want to save the channels of a **CsoundUnity** instance but you cannot save a *ScriptableObject*. After saving the JSON file in a known location, you will be able to reload it in a **CsoundUnity** instance which uses the same csd.

- **GLOBAL JSON preset**: this kind of preset is meant to be used when you want to save the whole state of a **CsoundUnity** instance. It is very similar as saving a Unity preset, but this works also at runtime, since it comes as a JSON file. It is useful when you want to carry over for example the **CsoundUnity** settings like the *Environment Settings*.

### The Preset Inspector

The **CsoundUnity** inspector has a *Preset* section which you can expand.

#### Load

<img src="images/presets_load.jpg" alt="Presets Load"/>

You can select the folder where to look for **CsoundUnityPresets**.
If you click on the *Select Preset Folder* button a dialogue will open asking to choose a folder.  
Otherwise you can quickly select one of the Unity folders: 

- *DataPath*
- *PersistentDataPath*
- *StreamingAssets*

The *Refresh* button will scan again the selected folder searching for presets.

If no folder is selected it will default to the *Assets* folder.

##### Scriptable Object Presets

The presets listed are the ones related with the csd that this **CsoundUnity** instance is using.  
You can increase the number of the presets displayed acting on the slider on the right of the *Assignable Presets:* label.

Pressing on the name of the preset will set that preset on this **CsoundUnity** instance. This works on the editor and at runtime.  
Hovering on the button will show you the path to that preset.  
You cannot have more than one preset with the same name in the same folder.  
CsoundUnityPresets come with the *.asset* extension.

The *To JSON* button next to each preset name lets you convert that preset in the JSON format.  
It will be saved in the same folder of the **CsoundUnityPreset** you're trying to convert.

##### JSON Presets

The JSON presets listed are **ALL** the json files found inside the project, if no LOAD folder is specified.   
There is no guarantee that all the listed JSONs are related with this CsoundUnity instance (because it would mean loading all the found JSON files).  
Pressing on the name of the preset will set that preset on this **CsoundUnity** instance. This works on the editor and at runtime.  
When you try to load a preset some checks are performed: 

- it has to represent a CsoundUnityPreset
- it has to have the same csdName as this **CsoundUnity** instance

Hovering on the button will show you the path to that preset.  
You cannot have more than one preset with the same name in the same folder.  
The *To SO* button next to each preset name lets you convert that preset into a CsoundUnityPreset asset.
It will be saved in the same folder of the JSON you're trying to convert.

##### Global JSON Presets

A global preset represents a **CsoundUnity** instance.  
The available **Global presets** will be listed in the JSON preset list.
If their name contains the word *global* it will be loaded as a **Global preset**, otherwise it will be loaded as a **CsoundUnityPreset**. Be aware of that if you will need to rename the preset after it was created.  
The **Global JSON presets** listed are searched in the entire project, if no LOAD folder is specified.   

#### Save

<img src="images/presets_save.jpg" alt="Presets Save"/>

To save a preset, first select your destination folder. If the folder is not set, the *Assets* folder (aka *DataPath*) will be used.  
Type a preset name: if name is empty a default one (*CsoundUnityPreset*) will be used.  
You can choose to save the preset in the 3 different formats (*ScriptableObject*, *JSON* or *Global*) pressing the related buttons.  
If a file with the same name is found in the same destination folder, it will ask if overwrite it or rename it (if saving as a *ScriptableObject*). If rename is chosen it will add a suffix to the name.   
If saving as a JSON and the JSON file exists at that location, it will simply add a suffix to the name.

#### Import Cabbage Snaps

You can import [Cabbage snaps](https://cabbageaudio.com/docs/presets/) and create CsoundUnityPresets from them.
Simply specify the folder where the *.snaps* files are contained, specify the destination folder where you want to save them and press *IMPORT*.  
A new **CsoundUnityPreset** will be created for each preset contained in each *.snaps* file found in the folder specified. 

<img src="images/presets_import.jpg" alt="Presets Import"/>

### Saving / Loading presets using C#

There are lots of methods that can help you save and load presets at runtime.
Consider that saving a **CsoundUnityPreset** as a *ScriptableObject* won't work at runtime (it is meant to be used in the Editor), so you will have to save it as a JSON.  
But you can of course load the *ScriptableObjects* presets at runtime. You can assign the **CsoundUnityPresets** as fields in your scripts like you would do with any other **Unity** asset.  

Be sure to check the [CsoundUnity API](http://rorywalsh.github.io/CsoundUnity/html/index.html) for the description of each method.