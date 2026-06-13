using UnityEngine;

/// <summary>
/// GreenPowder — A glowing green powder pickup that the player can grab.
/// Builds its own visuals (sphere + sparkle particles) at runtime.
/// Uses AutoHand's Grabbable component for VR hand interaction.
/// 
/// Usage: Add this component to an empty GameObject, or let MagicalPot spawn it.
/// </summary>
public class GreenPowder : MonoBehaviour
{
    [Header("Settings")]
    public float powderScale = 0.12f;
    public Color powderColor = new Color(0.15f, 1f, 0.3f, 1f);

    /// <summary>
    /// Static flag — any script can check GreenPowder.hasBeenPickedUp 
    /// to know if the player grabbed the powder.
    /// </summary>
    public static bool hasBeenPickedUp = false;

    private ParticleSystem sparkles;
    private Light powderGlow;
    private bool wasGrabbed = false;

    /// <summary>
    /// Resets the pickup flag at the start of a new game session (not on every Awake).
    /// This prevents the flag from being cleared if the powder object is recreated mid-game.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnGameStart()
    {
        hasBeenPickedUp = false;
    }

    private void Awake()
    {
        // Note: hasBeenPickedUp is reset via ResetOnGameStart() at game start,
        // not here — so recreating the powder object doesn't clear a pickup.
        BuildVisual();
        BuildSparkles();
        BuildGlow();
        SetupGrabbable();
    }

    // ── Glowing green sphere ──
    private void BuildVisual()
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "PowderMesh";
        sphere.transform.SetParent(transform);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * powderScale;

        Renderer rend = sphere.GetComponent<Renderer>();
        // Use the default material already instantiated on the primitive by Unity
        Material mat = rend.material;
        if (mat != null)
        {
            mat.color = powderColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", powderColor * 3f);
            mat.SetFloat("_Smoothness", 0.9f);
            rend.material = mat;
        }

        // Remove the sphere's own collider immediately so AutoHand doesn't scan it
        DestroyImmediate(sphere.GetComponent<Collider>());
    }

    // ── Green sparkle particles ──
    private void BuildSparkles()
    {
        GameObject psObj = new GameObject("Sparkles");
        psObj.transform.SetParent(transform);
        psObj.transform.localPosition = Vector3.zero;

        sparkles = psObj.AddComponent<ParticleSystem>();

        var main = sparkles.main;
        main.startLifetime = 1.2f;
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new Color(0.3f, 1f, 0.4f, 0.8f);
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.02f;

        var emission = sparkles.emission;
        emission.rateOverTime = 15f;

        var shape = sparkles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = powderScale * 0.8f;

        var colorOverLifetime = sparkles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.3f, 1f, 0.4f), 0f),
                new GradientColorKey(new Color(0.1f, 0.5f, 0.1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var psRenderer = psObj.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                                Shader.Find("Particles/Standard Unlit") ??
                                Shader.Find("Mobile/Particles/Additive");
        if (particleShader != null)
        {
            psRenderer.material = new Material(particleShader);
        }
        else
        {
            Debug.LogWarning("[GreenPowder] Particle shader not found. Using fallback material.");
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            psRenderer.material = new Material(temp.GetComponent<Renderer>().sharedMaterial);
            DestroyImmediate(temp);
        }
        psRenderer.material.color = new Color(0.3f, 1f, 0.4f, 0.8f);
    }

    // ── Small glow light ──
    private void BuildGlow()
    {
        GameObject lightObj = new GameObject("PowderGlow");
        lightObj.transform.SetParent(transform);
        lightObj.transform.localPosition = Vector3.zero;

        powderGlow = lightObj.AddComponent<Light>();
        powderGlow.type = LightType.Point;
        powderGlow.color = new Color(0.2f, 1f, 0.3f);
        powderGlow.intensity = 1.5f;
        powderGlow.range = 1.5f;
    }

    // ── Make it grabbable with AutoHand ──
    private void SetupGrabbable()
    {
        // Add a collider for grabbing
        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.radius = powderScale * 0.6f;

        // Add a Rigidbody (required by AutoHand Grabbable)
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        rb.useGravity = false;
        rb.isKinematic = true; // Start kinematic, AutoHand will manage it

        // Try to add AutoHand Grabbable component
        try
        {
            var grabbable = gameObject.AddComponent<Autohand.Grabbable>();
            Debug.Log("[GreenPowder] AutoHand Grabbable component added successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GreenPowder] Could not add AutoHand Grabbable: {e.Message}. " +
                "Player can still interact via proximity trigger.");
        }
    }

    private void Update()
    {
        // Pulse the glow
        if (powderGlow != null)
        {
            powderGlow.intensity = 1.2f + Mathf.Sin(Time.time * 3f) * 0.5f;
        }

        // Gentle hover/rotate animation when not grabbed
        if (!wasGrabbed)
        {
            transform.position += new Vector3(0f, Mathf.Sin(Time.time * 2f) * 0.001f, 0f);
            transform.Rotate(Vector3.up, 30f * Time.deltaTime);
        }

        // Detect if the powder has been grabbed (Rigidbody becomes non-kinematic or parent changes)
        if (!wasGrabbed)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                OnPickedUp();
            }

            // Also check if AutoHand Grabbable reports being held
            var grabbable = GetComponent<Autohand.Grabbable>();
            if (grabbable != null && grabbable.IsHeld())
            {
                OnPickedUp();
            }
        }
    }

    private void OnPickedUp()
    {
        if (wasGrabbed) return;
        wasGrabbed = true;
        hasBeenPickedUp = true;
        Debug.Log("[GreenPowder] ✦ Player picked up the Green Powder! hasBeenPickedUp = true ✦");
        Debug.Log("[GreenPowder] ✦ Now say 'Great Hall' to teleport to the Great Hall (2nd level)! ✦");

        // Stop animations after pickup
        if (sparkles != null) sparkles.Stop();
        if (powderGlow != null) powderGlow.intensity = 0.5f; // dim the glow
    }

    /// <summary>
    /// Call this from external scripts if you need to force-mark as picked up
    /// (e.g., on collision with hand if Grabbable doesn't work).
    /// </summary>
    public void ForcePickUp()
    {
        OnPickedUp();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Fallback: if anything tagged "Hand" or containing "Hand" in name touches this
        if (collision.gameObject.name.Contains("Hand") ||
            collision.gameObject.CompareTag("Player"))
        {
            OnPickedUp();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Fallback trigger detection
        if (other.gameObject.name.Contains("Hand") ||
            other.CompareTag("Player"))
        {
            OnPickedUp();
        }
    }

    private void OnDestroy()
    {
        // Don't reset hasBeenPickedUp on destroy — it persists until new powder spawns
    }
}
