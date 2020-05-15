using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CsoundUnityChild))]
[System.Serializable]
public class CsoundUnityChildEditor : Editor
{
    SerializedProperty m_selectedAudioChannelIndexByChan;
    SerializedProperty m_namedAudioChannelNames;
    SerializedProperty m_csoundUnityGO;
    SerializedProperty m_channels;

    private void OnEnable()
    {
        m_selectedAudioChannelIndexByChan = serializedObject.FindProperty("selectedAudioChannelIndexByChannel");
        m_namedAudioChannelNames = serializedObject.FindProperty("namedAudioChannelNames");
        m_csoundUnityGO = serializedObject.FindProperty("csoundUnityGameObject");
        m_channels = serializedObject.FindProperty("AudioChannelsSetting");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        if (m_csoundUnityGO.objectReferenceValue == null) return;

        var csdORV = (GameObject)m_csoundUnityGO.objectReferenceValue;
        var csd = csdORV.GetComponent<CsoundUnity>();
        if (csd.availableAudioChannels == null || csd.availableAudioChannels.Count < 1)
        {
            m_namedAudioChannelNames.ClearArray();
            m_namedAudioChannelNames.arraySize = 0;
            GUIStyle s = EditorStyles.boldLabel;
            s.normal.textColor = Color.red;
            s.wordWrap = true;
            EditorGUILayout.LabelField($"No audioChannels available, use the chnset opcode in {csd.csoundFile}", s);
            return;
        }

        var options = new string[csd.availableAudioChannels.Count];

        m_namedAudioChannelNames.ClearArray();
        m_namedAudioChannelNames.arraySize = options.Length;
        m_selectedAudioChannelIndexByChan.arraySize = m_channels.intValue;

        for (var c = 0; c < m_channels.intValue; c++)
        {
            for (var i = 0; i < options.Length; i++)
            {
                SerializedProperty el = m_namedAudioChannelNames.GetArrayElementAtIndex(i);
                el.stringValue = options[i];
                options[i] = csd.availableAudioChannels[i];
                //Debug.Log($"options[{i}]: {options[i]}");
            }
            EditorGUILayout.LabelField($"CHANNEL {c}");
            m_selectedAudioChannelIndexByChan.GetArrayElementAtIndex(c).intValue = EditorGUILayout.Popup(m_selectedAudioChannelIndexByChan.GetArrayElementAtIndex(c).intValue, options);
        }
        serializedObject.ApplyModifiedProperties();

    }
}
