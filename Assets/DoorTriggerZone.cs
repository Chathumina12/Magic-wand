using UnityEngine;

/// <summary>
/// DoorTriggerZone — Activates a DoorRotator when a Player enters the trigger collider
/// and closes the door when the Player exits.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorTriggerZone : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The DoorRotator component of the door that this trigger zone should control.")]
    public DoorRotator doorRotator;

    [Header("Trigger Settings")]
    [Tooltip("The tag assigned to the Player GameObject or VR Simulator.")]
    public string playerTag = "Player";

    private void Start()
    {
        // Ensure the collider is set as a trigger
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.Log($"[DoorTriggerZone] Automatically set collider on '{gameObject.name}' to be a Trigger.");
        }

        if (doorRotator == null)
        {
            Debug.LogWarning($"[DoorTriggerZone] DoorRotator is not assigned on '{gameObject.name}'! Please assign it in the Inspector.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            if (doorRotator != null)
            {
                doorRotator.Open();
                Debug.Log($"[DoorTriggerZone] Player entered. Opening door: {doorRotator.gameObject.name}");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            if (doorRotator != null)
            {
                doorRotator.Close();
                Debug.Log($"[DoorTriggerZone] Player exited. Closing door: {doorRotator.gameObject.name}");
            }
        }
    }
}
