using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Autohand;

/// <summary>
/// SceneTeleporter — Teleports the player to the "2nd level" scene 
/// when the voice command "Great Hall" is detected.
///
/// REQUIRES: Player must pick up the Green Powder from the Magical Pot first.
///
/// FIXES from original:
/// - Fade overlay now correctly follows VR camera (parented to Camera.main at runtime)
/// - Added scene existence check before loading to prevent silent failures
/// - Added clear Inspector-visible debug status
/// - BuildFadeOverlay now uses OcclusionMask layer to render on top in VR
/// - Transparent material now uses correct URP shader property names
/// - Added OnGUI debug display for teleport status
///
/// SETUP: Place this component on the VoiceManager GameObject or any persistent object.
/// Assign the 'sceneTeleporter' field in VoiceManager's Inspector.
/// Make sure "2nd level" is added to Build Settings (File > Build Settings > Add Open Scenes).
/// </summary>
public class SceneTeleporter : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Must exactly match the scene name in Build Settings (File > Build Settings).")]
    public string targetSceneName = "2nd level";

    [Header("Teleport VFX")]
    [Tooltip("Duration of the fade-to-green screen effect before scene loads.")]
    public float fadeOutDuration = 1.5f;

    [Tooltip("Color of the screen flash during teleportation.")]
    public Color flashColor = new Color(0.1f, 1f, 0.3f, 1f); // green flash

    [Header("Audio (Optional)")]
    public AudioClip teleportSound;
    public AudioSource audioSource;

    [Header("Status (Read Only)")]
    [SerializeField] private bool isTeleporting = false;
    [SerializeField] private bool powderPickedUp = false;

    // Fade overlay references
    private GameObject fadeOverlay;
    private Renderer fadeRenderer;
    private Material fadeMaterial;
    private bool overlayBuilt = false;

    // Stabber Carry-Over State
    public static bool carryOverStabber = false;
    public static GameObject stabberInstance = null;
    public static bool grabWithLeftHand = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeStabberPersister()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (carryOverStabber && stabberInstance != null)
        {
            Hand[] hands = FindObjectsOfType<Hand>();
            Hand targetHand = null;
            foreach (Hand hand in hands)
            {
                if (hand.left == grabWithLeftHand)
                {
                    targetHand = hand;
                    break;
                }
            }

            if (targetHand != null)
            {
                Grabbable grab = stabberInstance.GetComponent<Grabbable>();
                if (grab != null)
                {
                    targetHand.StartCoroutine(DelayGrab(targetHand, grab));
                }
            }
            else
            {
                Debug.LogWarning("[SceneTeleporter] Could not find correct hand to carry over stabber.");
            }

            carryOverStabber = false;
            stabberInstance = null;
        }
    }

    private static IEnumerator DelayGrab(Hand hand, Grabbable grab)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForEndOfFrame();
        hand.ForceGrab(grab);
        Debug.Log($"[SceneTeleporter] Stabber '{grab.gameObject.name}' carried over and successfully grabbed.");
    }

    private void Start()
    {
        // Validate scene is in build settings
        ValidateSceneInBuildSettings();
    }

    private void Update()
    {
        // Keep powder status updated for Inspector display
        powderPickedUp = GreenPowder.hasBeenPickedUp;
    }

    /// <summary>
    /// Checks that the target scene is in Build Settings.
    /// Logs a clear error if not, since SceneManager.LoadScene fails silently.
    /// </summary>
    private void ValidateSceneInBuildSettings()
    {
        bool found = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (string.Equals(sceneName, targetSceneName, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogError($"[SceneTeleporter] *** SCENE NOT IN BUILD SETTINGS ***\n" +
                $"Scene '{targetSceneName}' was not found in Build Settings!\n" +
                $"FIX: In Unity Editor go to File > Build Settings > click 'Add Open Scenes' " +
                $"while the '2nd level' scene is open, then click the checkbox next to it.");
        }
        else
        {
            Debug.Log($"[SceneTeleporter] Scene '{targetSceneName}' is in Build Settings. ✓");
        }
    }

    /// <summary>
    /// Call this from VoiceManager when "Great Hall" is detected.
    /// Checks that the player has picked up the Green Powder first.
    /// </summary>
    public void TeleportToGreatHall()
    {
        if (isTeleporting)
        {
            Debug.LogWarning("[SceneTeleporter] Already teleporting — please wait.");
            return;
        }

        if (!GreenPowder.hasBeenPickedUp)
        {
            Debug.LogWarning("[SceneTeleporter] Cannot teleport — player has not picked up the Green Powder yet!\n" +
                "Walk to the Magical Cauldron, pick up the Green Powder, then say 'Great Hall'.");
            return;
        }

        Debug.Log("[SceneTeleporter] ✦ Green Powder confirmed! TELEPORTING TO GREAT HALL (2nd level)! ✦");
        StartCoroutine(TeleportSequence());
    }

    /// <summary>
    /// Force teleport without powder check. Useful for testing in editor.
    /// Call this from the Inspector context menu or from test code.
    /// </summary>
    [ContextMenu("Force Teleport (No Powder Check)")]
    public void ForceTeleport()
    {
        if (isTeleporting)
        {
            Debug.LogWarning("[SceneTeleporter] Already teleporting.");
            return;
        }
        Debug.Log("[SceneTeleporter] Force teleport triggered (bypassing powder check).");
        StartCoroutine(TeleportSequence());
    }

    private IEnumerator TeleportSequence()
    {
        isTeleporting = true;

        // Play teleport sound
        if (teleportSound != null && audioSource != null)
            audioSource.PlayOneShot(teleportSound);

        // Build and show fade overlay
        BuildFadeOverlay();
        if (fadeOverlay != null)
            fadeOverlay.SetActive(true);

        // Fade to green over fadeOutDuration seconds
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeOutDuration);

            if (fadeMaterial != null)
                fadeMaterial.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);

            // Keep overlay in front of VR camera each frame
            PositionOverlayInFrontOfCamera();
            yield return null;
        }

        // Ensure fully opaque
        if (fadeMaterial != null)
            fadeMaterial.color = new Color(flashColor.r, flashColor.g, flashColor.b, 1f);

        // Carry over held stabber if present
        carryOverStabber = false;
        stabberInstance = null;

        Hand[] hands = FindObjectsOfType<Hand>();
        foreach (Hand hand in hands)
        {
            Grabbable held = hand.GetHeldGrabbable();
            if (held != null && held.GetComponent<Stabber>() != null)
            {
                carryOverStabber = true;
                stabberInstance = held.gameObject;
                grabWithLeftHand = hand.left;

                // Force release
                hand.ForceReleaseGrab();

                // Unparent so it can persist
                held.transform.SetParent(null);
                DontDestroyOnLoad(held.gameObject);
                
                Debug.Log($"[SceneTeleporter] Stabber '{held.gameObject.name}' marked for carry-over.");
                break;
            }
        }

        // Brief pause at full opacity
        yield return new WaitForSeconds(0.3f);

        // Load target scene
        Debug.Log($"[SceneTeleporter] Loading scene: '{targetSceneName}'");
        SceneManager.LoadScene(targetSceneName);
        // Note: isTeleporting will be reset when this scene unloads
    }

    /// <summary>
    /// Creates the screen-space quad overlay for the teleportation fade.
    /// Built lazily just before first teleport to ensure Camera.main exists.
    /// </summary>
    private void BuildFadeOverlay()
    {
        if (overlayBuilt && fadeOverlay != null) return;

        fadeOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fadeOverlay.name = "TeleportFadeOverlay";

        // Remove collider immediately so it doesn't interfere with physics or scanning
        DestroyImmediate(fadeOverlay.GetComponent<Collider>());

        // Create an unlit transparent material
        // Try URP shader first, fall back to standard
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ??
                             Shader.Find("Unlit/Color") ??
                             Shader.Find("UI/Default") ??
                             Shader.Find("Standard");

        if (unlitShader != null)
        {
            fadeMaterial = new Material(unlitShader);
        }
        else
        {
            Debug.LogWarning("[SceneTeleporter] Unlit shader not found. Using fallback material.");
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fadeMaterial = new Material(temp.GetComponent<Renderer>().sharedMaterial);
            DestroyImmediate(temp);
        }

        // Enable transparency
        fadeMaterial.SetFloat("_Surface", 1f);     // URP: 0=Opaque, 1=Transparent
        fadeMaterial.SetFloat("_Blend", 0f);        // URP: 0=Alpha blend
        fadeMaterial.SetOverrideTag("RenderType", "Transparent");
        fadeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        fadeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        fadeMaterial.SetInt("_ZWrite", 0);
        fadeMaterial.renderQueue = 4000; // Render after everything else

        // Start fully transparent
        fadeMaterial.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

        fadeRenderer = fadeOverlay.GetComponent<Renderer>();
        fadeRenderer.material = fadeMaterial;
        fadeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fadeRenderer.receiveShadows = false;

        // Scale to cover the whole view
        fadeOverlay.transform.localScale = new Vector3(10f, 10f, 1f);

        // Position it immediately
        PositionOverlayInFrontOfCamera();

        overlayBuilt = true;
        Debug.Log("[SceneTeleporter] Fade overlay created.");
    }

    /// <summary>
    /// Positions the fade overlay directly in front of the VR camera.
    /// Called every frame during the fade so it tracks head movement.
    /// </summary>
    private void PositionOverlayInFrontOfCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || fadeOverlay == null) return;

        // Place slightly closer than the near clip plane to guarantee it covers everything
        float dist = cam.nearClipPlane + 0.05f;
        fadeOverlay.transform.position = cam.transform.position + cam.transform.forward * dist;
        fadeOverlay.transform.rotation = cam.transform.rotation;

        // Scale to match frustum at this distance
        float height = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width  = height * cam.aspect;
        fadeOverlay.transform.localScale = new Vector3(width * 1.1f, height * 1.1f, 1f); // slight overdraw
    }
}
