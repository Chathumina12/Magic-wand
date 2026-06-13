using UnityEngine;
using UnityEditor;

public class CheckDoorComponents : EditorWindow
{
    [MenuItem("Tools/Magic Wand/📋 Check Door Components")]
    public static void ScanDoorComponents()
    {
        Debug.Log("============ SCROLLING DOOR COMPONENT CHECK ============");
        
        DoorRotator[] doors = Object.FindObjectsOfType<DoorRotator>();
        if (doors.Length == 0)
        {
            Debug.LogWarning("No DoorRotator components found in the scene.");
            EditorUtility.DisplayDialog("Check Door Components", "No doors with DoorRotator found.", "OK");
            return;
        }

        foreach (DoorRotator door in doors)
        {
            GameObject go = door.gameObject;
            Component[] components = go.GetComponents<Component>();
            
            string componentList = $"GameObject '{go.name}' (World Pos: {go.transform.position}):\n";
            foreach (Component comp in components)
            {
                if (comp == null)
                {
                    componentList += "  -> [Missing / Broken Component Reference]\n";
                    continue;
                }
                componentList += $"  -> {comp.GetType().Name} (Enabled: {IsComponentEnabled(comp)})\n";
            }

            // Also check children for rigidbodies or joints that might block parent rotation
            Rigidbody childRb = go.GetComponentInChildren<Rigidbody>();
            if (childRb != null && childRb.gameObject != go)
            {
                componentList += $"  [Child Physics Warning] Found Rigidbody on child '{childRb.gameObject.name}'!\n";
            }
            
            Joint childJoint = go.GetComponentInChildren<Joint>();
            if (childJoint != null && childJoint.gameObject != go)
            {
                componentList += $"  [Child Physics Warning] Found Joint on child '{childJoint.gameObject.name}'!\n";
            }

            Debug.Log(componentList);
        }

        Debug.Log("=======================================================");
        EditorUtility.DisplayDialog("Check Door Components", "Components printed to the Unity Console.", "OK");
    }

    private static string IsComponentEnabled(Component comp)
    {
        if (comp is Behaviour behaviour)
        {
            return behaviour.enabled ? "TRUE" : "FALSE";
        }
        if (comp is Renderer renderer)
        {
            return renderer.enabled ? "TRUE" : "FALSE";
        }
        if (comp is Collider collider)
        {
            return collider.enabled ? "TRUE" : "FALSE";
        }
        return "N/A (Always Active)";
    }
}
