using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom inspector for DoorRotator to add test buttons and help debug rotation settings.
/// </summary>
[CustomEditor(typeof(DoorRotator))]
[CanEditMultipleObjects]
public class DoorRotatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default fields (Rotation Angle, Speed, Axis, Is Open)
        DrawDefaultInspector();

        DoorRotator door = (DoorRotator)target;

        GUILayout.Space(15);
        GUILayout.Label("🔧 Debug Controls", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Trigger Open"))
            {
                foreach (var t in targets)
                {
                    DoorRotator d = (DoorRotator)t;
                    d.Open();
                    EditorUtility.SetDirty(d);
                }
            }

            if (GUILayout.Button("Trigger Close"))
            {
                foreach (var t in targets)
                {
                    DoorRotator d = (DoorRotator)t;
                    d.Close();
                    EditorUtility.SetDirty(d);
                }
            }
        }

        GUILayout.Space(5);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "In Edit Mode, clicking the buttons toggles the 'Is Open' flag.\n" +
                "Enter Play Mode to see the smooth rotation animation.", 
                MessageType.Info);
        }
        else
        {
            GUILayout.Label($"Current State: {(door.IsOpen ? "OPEN" : "CLOSED")}", EditorStyles.boldLabel);
        }
    }
}
