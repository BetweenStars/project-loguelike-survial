using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Weapon), true)]
public class WeaponLevelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.name.EndsWith("ByLevel") && property.isArray)
            {
                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(property.name), EditorStyles.boldLabel);
                if (property.arraySize != Weapon.MaxLevel)
                    EditorGUILayout.HelpBox("Use seven entries for levels 1–7. Missing levels use the last entry.", MessageType.Warning);
                for (int i = 0; i < property.arraySize; i++)
                    EditorGUILayout.PropertyField(property.GetArrayElementAtIndex(i), new GUIContent($"Level {i + 1}"));
            }
            else
                using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                    EditorGUILayout.PropertyField(property, true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}
