using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor utility to automatically generate and configure trigger zones for all doors
/// and automatically repair door references in the VoiceManager.
/// </summary>
public class DoorTriggerSetupTool : EditorWindow
{
    [MenuItem("Tools/Magic Wand/🚪 Setup Door Trigger Zones")]
    public static void SetupTriggerZones()
    {
        DoorRotator[] doors = Object.FindObjectsOfType<DoorRotator>();
        if (doors.Length == 0)
        {
            EditorUtility.DisplayDialog("Setup Door Trigger Zones", 
                "No DoorRotator components found in the active scene. Please make sure your doors have the DoorRotator script attached.", 
                "OK");
            return;
        }

        int createdCount = 0;
        int updatedCount = 0;

        foreach (DoorRotator door in doors)
        {
            // ─── Step 1: Setup/Align Trigger Zone ───
            string triggerName = door.gameObject.name + "_TriggerZone";
            
            // Calculate the actual visual center of the door using MeshRenderer bounds
            MeshRenderer[] renderers = door.GetComponentsInChildren<MeshRenderer>();
            Vector3 visualCenter = door.transform.position;
            
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                visualCenter = bounds.center;
                
                // Align visual center vertically to the door's pivot height
                visualCenter.y = door.transform.position.y;
            }

            // Find or create the trigger zone GameObject
            GameObject triggerGO = GameObject.Find(triggerName);
            bool isNew = false;
            
            if (triggerGO == null)
            {
                triggerGO = new GameObject(triggerName);
                Undo.RegisterCreatedObjectUndo(triggerGO, $"Create Trigger Zone for {door.gameObject.name}");
                isNew = true;
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(triggerGO, $"Update Trigger Zone for {door.gameObject.name}");
            }

            // Position and rotate it to match the door's visual center and rotation
            triggerGO.transform.position = visualCenter;
            triggerGO.transform.rotation = door.transform.rotation;

            // Ensure BoxCollider is present and configured as a trigger
            BoxCollider col = triggerGO.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = triggerGO.AddComponent<BoxCollider>();
            }
            col.isTrigger = true;
            
            // Default size of 4m x 3m x 4m is ideal for a player walking up to the door
            col.size = new Vector3(4f, 3f, 4f);

            // Ensure DoorTriggerZone script is present and linked to the correct DoorRotator
            DoorTriggerZone zone = triggerGO.GetComponent<DoorTriggerZone>();
            if (zone == null)
            {
                zone = triggerGO.AddComponent<DoorTriggerZone>();
            }
            zone.doorRotator = door;

            // Set trigger as sibling under the same parent to avoid rotation inheritance issues
            if (door.transform.parent != null && triggerGO.transform.parent != door.transform.parent)
            {
                triggerGO.transform.SetParent(door.transform.parent, true);
            }

            if (isNew)
                createdCount++;
            else
                updatedCount++;
        }

        // ─── Step 2: Auto-Repair VoiceManager Door References ───
        int repairedReferencesCount = 0;
        VoiceManager vm = Object.FindObjectOfType<VoiceManager>();
        if (vm != null)
        {
            SerializedObject so = new SerializedObject(vm);
            SerializedProperty prop1 = so.FindProperty("doorRotator");
            SerializedProperty prop2 = so.FindProperty("doorRotator1");

            bool modified = false;

            if (doors.Length > 0)
            {
                if (prop1.objectReferenceValue == null)
                {
                    prop1.objectReferenceValue = doors[0];
                    modified = true;
                    repairedReferencesCount++;
                    Debug.Log($"[DoorTriggerSetupTool] Automatically assigned '{doors[0].gameObject.name}' to VoiceManager.doorRotator");
                }
            }

            if (doors.Length > 1)
            {
                if (prop2.objectReferenceValue == null)
                {
                    prop2.objectReferenceValue = doors[1];
                    modified = true;
                    repairedReferencesCount++;
                    Debug.Log($"[DoorTriggerSetupTool] Automatically assigned '{doors[1].gameObject.name}' to VoiceManager.doorRotator1");
                }

                // If both are assigned but point to the exact same door, fix the duplication
                if (prop1.objectReferenceValue == prop2.objectReferenceValue)
                {
                    if (prop1.objectReferenceValue == doors[0])
                    {
                        prop2.objectReferenceValue = doors[1];
                    }
                    else
                    {
                        prop2.objectReferenceValue = doors[0];
                    }
                    modified = true;
                    repairedReferencesCount++;
                    Debug.Log("[DoorTriggerSetupTool] Fixed duplicate door assignments in VoiceManager. Assigned doors uniquely.");
                }
            }

            if (modified)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(vm);
            }
        }

        // Mark the active scene as dirty so the editor knows it has changes to save
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string message = $"Setup completed successfully!\n\n" +
                         $"* Trigger Zones Created: {createdCount}\n" +
                         $"* Trigger Zones Updated/Aligned: {updatedCount}\n" +
                         $"* VoiceManager references repaired: {repairedReferencesCount}";
                         
        EditorUtility.DisplayDialog("Setup Door Trigger Zones", message, "OK");
        Debug.Log($"[DoorTriggerSetupTool] {message}");
    }
}
