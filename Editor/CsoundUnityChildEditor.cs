using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CsoundUnityChild))]
[System.Serializable]
public class CsoundUnityChildEditor : Editor
{
    SerializedProperty m_selectedAudioChannelIndexByChan;
    SerializedProperty m_csoundUnityGO;
    SerializedProperty m_channels;
    SerializedProperty m_availableAudioChannels;
    SerializedProperty m_bufferSize;

    private void OnEnable()
    {
        m_selectedAudioChannelIndexByChan = serializedObject.FindProperty("selectedAudioChannelIndexByChannel");
        m_csoundUnityGO = serializedObject.FindProperty("csoundUnityGameObject");
        m_channels = serializedObject.FindProperty("AudioChannelsSetting");
        m_availableAudioChannels = serializedObject.FindProperty("availableAudioChannels");
        m_bufferSize = serializedObject.FindProperty("bufferSize");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        if (m_csoundUnityGO.objectReferenceValue != null)
        {
            var csdORV = (GameObject)m_csoundUnityGO.objectReferenceValue;
            var csd = csdORV.GetComponent<CsoundUnity>();

            // reset if the Csound available channels differ from the ones saved here
            if (!CheckListEquality(m_availableAudioChannels, csd.availableAudioChannels))
            {
                Debug.Log("Csound Unity Child channel updated!");
                m_selectedAudioChannelIndexByChan.ClearArray();
                m_availableAudioChannels.ClearArray();
                var count = 0;
                foreach (var ac in csd.availableAudioChannels)
                {
                    m_availableAudioChannels.InsertArrayElementAtIndex(count);
                    m_availableAudioChannels.GetArrayElementAtIndex(count).stringValue = ac;
                    count++;
                }

                Debug.Log($"{csd.availableAudioChannels.Count} channels found in {csd.csoundFileName}!");
                m_selectedAudioChannelIndexByChan.arraySize = csd.availableAudioChannels.Count;
                for (var i = 0; i < csd.availableAudioChannels.Count; i++)
                {
                    SerializedProperty chanName = m_availableAudioChannels.GetArrayElementAtIndex(i);
                    chanName.stringValue = csd.availableAudioChannels[i];
                    Debug.Log($"added serialized property chanName {chanName.stringValue} at pos {i}");
                    m_selectedAudioChannelIndexByChan.InsertArrayElementAtIndex(i);
                    SerializedProperty chanIndx = m_selectedAudioChannelIndexByChan.GetArrayElementAtIndex(i);
                    chanIndx.intValue = 0;
                    Debug.Log($"added serialized property chanIndx {chanIndx.intValue} at pos {i}");
                }
            }

            if (m_selectedAudioChannelIndexByChan.arraySize > 0 && m_availableAudioChannels.arraySize > 0)
            {
                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Buffer Size: " + m_bufferSize.intValue + "");
                }

                var options = new string[m_availableAudioChannels.arraySize];
                for (var o = 0; o < m_availableAudioChannels.arraySize; o++)
                {
                    options[o] = m_availableAudioChannels.GetArrayElementAtIndex(o).stringValue;
                }
                for (var c = 0; c < m_channels.intValue; c++)
                {
                    EditorGUILayout.LabelField($"CHANNEL {c}");

                    m_selectedAudioChannelIndexByChan.GetArrayElementAtIndex(c).intValue = (int)EditorGUILayout.Popup(m_selectedAudioChannelIndexByChan.GetArrayElementAtIndex(c).intValue, options);
                    // Debug.Log($"chan {c} selected: " + m_selectedAudioChannelIndexByChan.GetArrayElementAtIndex(c).intValue);
                }
            }
            else
            {
                GUIStyle s = new GUIStyle
                {
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                };
                s.normal.textColor = Color.red;
                EditorGUILayout.LabelField($"No audioChannels available, use the chnset opcode in {csd.csoundFileName}", s);
            }
        }
        serializedObject.ApplyModifiedProperties();
    }

    //assumes list are ordered
    private bool CheckListEquality(SerializedProperty first, List<string> second)
    {
        if (first == null && second == null) return true;
        if ((first != null && second == null) || (first == null && second != null)) return false;
        if (first.arraySize != second.Count) return false;
        for (var i = 0; i < first.arraySize; i++)
        {
            if (first.GetArrayElementAtIndex(i).stringValue.Equals(second[i])) continue;
            else return false;
        }
        return true;
    }
}
