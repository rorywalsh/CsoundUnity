## Getting Started ##

CsoundUnity is a component that can be added to any GameObject in a scene. To do so simple press the **AddComponent** button in Unity's inspector, then search for **CsoundUnity** and select the **CsoundUnity** component. This will add a **Csound Unity** and **Audio Source**  component (if it does not already exist) to the selected GameObject.

<img src="images/addCsoundUnityComponent_v3.gif" alt="Add CsoundUnity"/>

Once a CsoundUnity component has been added to a GameObject, you will need to load a Csound file into its **csd Asset** field. This file can exist anywhere in your Unity Project's asset folder.
 To attach a Csound file to a CsoundUnity component, drag it from your Assets to the **Csd Asset'** field in the CsoundUnity component inspector. Once your game starts, Csound will send audio to the **Audio Source** component. This allows csound to seemlessly integrate into Unity's build in audio system providing access to the spacialition, mixer, and effects systems built into the engine. 

 <img src="images/addCsoundFile_v3.gif" alt="Add Csound file"/>

<!-- The placement of this seems off as mc is a more complex topic- perhaps a seperate section on working with mc is needed -->

<!--
 **CsoundUnity** can input and output any number of channels. See [**CsoundUnity.ProcessBlock()**](https://github.com/rorywalsh/CsoundUnity/blob/7f45fd3bfffa9f3d4760b0437d38de44b04a96e9/Runtime/CsoundUnity.cs#L1423) 
-->
