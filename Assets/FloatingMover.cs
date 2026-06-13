using UnityEngine;

/// <summary>
/// FloatingMover — Makes an object smoothly float upward (Wingardium Leviosa) 
/// and return to its original position (Descendo / Finite Incantatem).
///
/// FIXES from original:
/// - Removed duplicate KeyCode.F check (was conflicting with VoiceManager)
/// - Fixed Down(): wobbleTime reset was inside a redundant if-check (always true)
/// - Added IsFloating property for external state queries
/// - Added XML documentation
/// </summary>
public class FloatingMover : MonoBehaviour
{
    [Header("Float Settings")]
    [Tooltip("How high above the start position the object floats (in Unity units).")]
    public float floatHeight = 2f;

    [Tooltip("Speed of floating upward.")]
    public float floatSpeed = 1.5f;

    [Tooltip("Speed of dropping back down.")]
    public float dropSpeed = 3f;

    [Tooltip("Amplitude of the gentle up-down wobble while floating.")]
    public float floatWobbleAmount = 0.2f;

    // ── Internal state ──
    private Vector3 startPos;
    private Vector3 targetPos;
    private bool isFloating = false;
    private float wobbleTime = 0f;

    private void Start()
    {
        startPos = transform.position;
        targetPos = startPos + Vector3.up * floatHeight;
    }

    private void Update()
    {
        if (isFloating)
        {
            // Smoothly move up with gentle sinusoidal wobble
            wobbleTime += Time.deltaTime * 2f;
            Vector3 wobble = new Vector3(0f, Mathf.Sin(wobbleTime) * floatWobbleAmount, 0f);
            transform.position = Vector3.Lerp(transform.position, targetPos + wobble, Time.deltaTime * floatSpeed);
        }
        else
        {
            // Smoothly drop back to starting position
            transform.position = Vector3.Lerp(transform.position, startPos, Time.deltaTime * dropSpeed);
        }
    }

    /// <summary>
    /// Starts floating the object upward. Called by Wingardium Leviosa.
    /// Safe to call multiple times — no effect if already floating.
    /// </summary>
    public void Float()
    {
        isFloating = true;
        wobbleTime = 0f;
        Debug.Log($"[FloatingMover] {gameObject.name} is floating.");
    }

    /// <summary>
    /// Returns the object to its starting position. Called by Descendo and Finite Incantatem.
    /// Safe to call multiple times — no effect if already on the ground.
    /// </summary>
    public void Down()
    {
        isFloating = false;
        wobbleTime = 0f; // Always reset wobble when coming down
        Debug.Log($"[FloatingMover] {gameObject.name} is coming down.");
    }

    /// <summary>
    /// Toggles between floating and grounded states.
    /// </summary>
    public void ToggleFloat()
    {
        if (isFloating) Down();
        else Float();
    }

    /// <summary>
    /// Returns whether this object is currently in the floating state.
    /// </summary>
    public bool IsFloating => isFloating;
}
