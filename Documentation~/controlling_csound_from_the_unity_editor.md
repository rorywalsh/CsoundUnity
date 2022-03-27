## Controlling Csound from the Unity Editor

You can control **Csound** channels using the Unity Editor while you are developing your game's sounds.  
To do so, you must provide a short <Cabbage></Cabbage> descriptor at the top of your **Csound** files (.csd) describing the channels that are needed.  
This simple descriptor section uses a single line of code to describe each channel.  
Each line starts with the given channel's controller type and is then followed by combination of other identifiers such as *channel()*, *text()*, and *range()*.  
The following descriptor sets up 3 channel controllers. A slider, a button and a checkbox(toggle).
```csound
;csd file
<Cabbage>
form caption("SimpleFreq")
hslider channel("freq1"), text("Frequency Slider"), range(0, 10000, 0)
button channel("trigger"), text("Push me")
checkbox channel("mute")
</Cabbage>
```

Each control MUST specify a channel.  
The **range()** identifier must be used if the controller type is a slider.  
The **text()** identifier can be used to display unique text beside a control but it is not required. If it is left out, the channel() name will be used as the control's label. The caption() identifier, used with form, is used to display some simple help text to the user.

See [**Cabbage Widgets**](https://cabbageaudio.com/docs/cabbage_syntax/) for more information about the syntax to use.
**CsoundUnity** aims to support most of the (relevant) Widgets available in Cabbage.  
[**Cabbage**](https://cabbageaudio.com/) is a framework for audio software development, with it you can create wonderful VST/AU plugins based on **Csound**.  
And of course it is a great **Csound** IDE!  
You will find a lot of examples inside it: be aware that most of them are not supported at the moment in **CsoundUnity**, since the  Widgets support is still limited. But they're worth a try!   
Feel free to ask on the [**Cabbage Forum**](https://forum.cabbageaudio.com/) for any Cabbage example that you would like to be added to **CsoundUnity** as a sample.

When a **Csound** file which contains a valid ***< Cabbage >*** section is dragged to a **CsoundUnity** component, Unity will generate controls for each channel.  
These controls can be tweaked when your game is running. Each time a control is modified, its value is sent to **Csound** from Unity on the associated channel. In this way it works the same as the method above, only we don't have to code anything in order to test our sound.  
If you change a channel value via C# code, you will see its value updated in the editor.  
For now, **CsoundUnity** supports only four types of controller: *slider*, *checkbox(toggle)*, *button* and *comboboxes*.