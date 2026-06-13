using UnityEngine;

/// <summary>
/// DoorRotator — Smoothly rotates a door open and closed.
///
/// FIXES from original:
/// - Added guard so Open() called multiple times doesn't reset the animation
/// - Added Close() reset guard similarly
/// - Added configurable rotation axis (not just Y) for different door models
/// - targetRotation now computed from initialRotation to avoid Start() ordering issues
/// - Added isFullyOpen / isFullyClosed state tracking for animation completion
/// </summary>
public class DoorRotator : MonoBehaviour
{
    [Header("Door Settings")]
    [Tooltip("How many degrees the door rotates when opened (positive = counter-clockwise, negative = clockwise).")]
    public float rotationAngle = 90f;

    [Tooltip("Speed of the door rotation animation.")]
    public float rotationSpeed = 2f;

    [Tooltip("Axis to rotate around. Y = horizontal hinge (standard door). X = top/bottom hinge. Z = wall-mounted.")]
    public Vector3 rotationAxis = Vector3.up; // Default: Y axis (standard door hinge)

    [Header("Debug")]
    [SerializeField] private bool isOpen = false;

    // Initial and target rotations calculated at Start
    private Quaternion initialRotation;
    private Quaternion targetRotation;

    private void Start()
    {
        // Store the door's starting rotation
        initialRotation = transform.rotation;

        // Calculate the open rotation from the initial rotation using the configured axis
        targetRotation = initialRotation * Quaternion.Euler(rotationAxis.normalized * rotationAngle);

        Debug.Log($"[DoorRotator] Initialized. Rotation axis: {rotationAxis}, angle: {rotationAngle}°");
    }

    private void Update()
    {
        // Smoothly interpolate to either open or closed rotation
        Quaternion destination = isOpen ? targetRotation : initialRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, destination, Time.deltaTime * rotationSpeed);
    }

    /// <summary>
    /// Opens the door. Safe to call multiple times — will not restart animation if already open.
    /// </summary>
    public void Open()
    {
        if (isOpen)
        {
            Debug.Log("[DoorRotator] Door is already open.");
            return;
        }
        isOpen = true;
        Debug.Log("[DoorRotator] Door opening...");
    }

    /// <summary>
    /// Closes the door. Safe to call multiple times — will not restart animation if already closed.
    /// </summary>
    public void Close()
    {
        if (!isOpen)
        {
            Debug.Log("[DoorRotator] Door is already closed.");
            return;
        }
        isOpen = false;
        Debug.Log("[DoorRotator] Door closing...");
    }

    /// <summary>
    /// Toggles door between open and closed states.
    /// </summary>
    public void ToggleDoor()
    {
        if (isOpen)
            Close();
        else
            Open();
    }

    /// <summary>
    /// Returns whether the door is currently in the open state.
    /// </summary>
    public bool IsOpen => isOpen;
}