using UnityEngine;
using UnityEditor;

public class DoorDiagnosticTool : EditorWindow
{
    [MenuItem("Tools/Magic Wand/🔍 Run Door Diagnostics")]
    public static void RunDiagnostics()
    {
        Debug.Log("============ START DOOR DIAGNOSTICS ============");
        
        DoorRotator[] doors = Object.FindObjectsOfType<DoorRotator>();
        Debug.Log($"Found {doors.Length} DoorRotator(s) in the scene.");

        foreach (DoorRotator door in doors)
        {
            Vector3 doorPivot = door.transform.position;
            
            // Try to find the visual center using MeshRenderer in children
            MeshRenderer[] renderers = door.GetComponentsInChildren<MeshRenderer>();
            Vector3 visualCenter = doorPivot;
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                visualCenter = bounds.center;
            }

            Debug.Log($"[Door GameObject] Name: '{door.gameObject.name}'\n" +
                      $"  -> Parent Pivot World Pos: {doorPivot}\n" +
                      $"  -> Visual Mesh World Pos: {visualCenter}\n" +
                      $"  -> Distance Pivot-to-Mesh: {Vector3.Distance(doorPivot, visualCenter):F2} meters");

            // Look for trigger zone
            string triggerName = door.gameObject.name + "_TriggerZone";
            GameObject triggerGO = GameObject.Find(triggerName);
            if (triggerGO != null)
            {
                DoorTriggerZone triggerZone = triggerGO.GetComponent<DoorTriggerZone>();
                BoxCollider col = triggerGO.GetComponent<BoxCollider>();
                
                string targetDoorName = (triggerZone != null && triggerZone.doorRotator != null) 
                    ? triggerZone.doorRotator.gameObject.name 
                    : "NULL";

                Debug.Log($"[Trigger Zone] Name: '{triggerGO.name}'\n" +
                          $"  -> World Position: {triggerGO.transform.position}\n" +
                          $"  -> Collider Center: {col.bounds.center}\n" +
                          $"  -> Target Door Rotator: '{targetDoorName}'");
            }
            else
            {
                Debug.LogWarning($"[Trigger Zone] Missing trigger zone for door '{door.gameObject.name}' (expected name: '{triggerName}')");
            }
        }

        Debug.Log("============ END DOOR DIAGNOSTICS ============");
        EditorUtility.DisplayDialog("Door Diagnostics", "Diagnostics printed to the Unity Console. Please check the Console window.", "OK");
    }
}
