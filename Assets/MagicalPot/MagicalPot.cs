using System.Collections;
using UnityEngine;

/// <summary>
/// MagicalPot — Place this on an empty GameObject in your scene.
/// It builds a cauldron mesh, green smoke particles, glowing light,
/// and a proximity trigger at runtime.  When the player walks close,
/// a GreenPowder pickup appears above the pot.
/// </summary>
public class MagicalPot : MonoBehaviour
{
    [Header("Pot Settings")]
    public float potRadius = 0.4f;
    public float potHeight = 0.6f;
    public Color potColor = new Color(0.15f, 0.15f, 0.15f, 1f); // dark iron

    [Header("Particle Settings")]
    public Color smokeColor = new Color(0.1f, 0.9f, 0.2f, 0.6f); // green glow
    public float smokeRate = 30f;
    [Tooltip("Minimum size of individual smoke particles")]
    public float minParticleSize = 0.05f;
    [Tooltip("Maximum size of individual smoke particles")]
    public float maxParticleSize = 0.15f;

    [Header("Glow Light Settings")]
    [Tooltip("Base light intensity for the cauldron glow")]
    public float glowIntensity = 1.0f;
    [Tooltip("How much the light intensity pulses/flickers")]
    public float glowPulseIntensity = 0.3f;
    [Tooltip("The range of the cauldron glow light")]
    public float glowRange = 2.0f;

    [Header("Proximity")]
    public float triggerRadius = 2.0f;

    [Header("Green Powder")]
    [Tooltip("If empty, a default powder object is created automatically.")]
    public GameObject greenPowderPrefab;

    // Runtime references
    private ParticleSystem smokeParticles;
    private Light potGlow;
    private GameObject powderInstance;
    private bool powderSpawned = false;
    private bool playerInRange = false;
    private GameObject promptUI;

    // ─────────────────────── SETUP ───────────────────────

    private void Awake()
    {
        BuildPotVisual();
        BuildParticleSystem();
        BuildGlowLight();
        BuildTriggerZone();
        BuildPromptUI();
    }

    // ── Cauldron body (cylinder + rim torus approximated by a flattened cylinder) ──
    private void BuildPotVisual()
    {
        // Main pot body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "PotBody";
        body.transform.SetParent(transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(potRadius * 2f, potHeight * 0.5f, potRadius * 2f);

        Renderer bodyRend = body.GetComponent<Renderer>();
        // Use the default material already instantiated on the primitive by Unity
        Material potMat = bodyRend.material;
        if (potMat != null)
        {
            potMat.color = potColor;
            potMat.SetFloat("_Metallic", 0.7f);
            potMat.SetFloat("_Smoothness", 0.5f);
            bodyRend.material = potMat;
        }

        // Destroy the default collider immediately — we use a trigger instead
        DestroyImmediate(body.GetComponent<Collider>());

        // Rim (slightly wider, thin cylinder on top)
        GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "PotRim";
        rim.transform.SetParent(transform);
        rim.transform.localPosition = new Vector3(0f, potHeight * 0.48f, 0f);
        rim.transform.localScale = new Vector3(potRadius * 2.3f, potHeight * 0.06f, potRadius * 2.3f);

        Renderer rimRend = rim.GetComponent<Renderer>();
        Material rimMat = potMat != null ? new Material(potMat) : rimRend.material;
        if (rimMat != null)
        {
            rimMat.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            rimRend.material = rimMat;
        }
        DestroyImmediate(rim.GetComponent<Collider>());

        // Liquid surface (green disc inside the pot)
        GameObject liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        liquid.name = "PotLiquid";
        liquid.transform.SetParent(transform);
        liquid.transform.localPosition = new Vector3(0f, potHeight * 0.35f, 0f);
        liquid.transform.localScale = new Vector3(potRadius * 1.8f, 0.01f, potRadius * 1.8f);

        Renderer liqRend = liquid.GetComponent<Renderer>();
        Material liqMat = liqRend.material;
        if (liqMat != null)
        {
            liqMat.color = new Color(0.05f, 0.85f, 0.15f, 0.9f);
            liqMat.SetFloat("_Smoothness", 0.95f);
            // Enable emission for glow
            liqMat.EnableKeyword("_EMISSION");
            liqMat.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 0.1f) * 2f);
            liqRend.material = liqMat;
        }
        DestroyImmediate(liquid.GetComponent<Collider>());

        // Add a solid collider so the pot has physics presence
        BoxCollider solidCollider = gameObject.AddComponent<BoxCollider>();
        solidCollider.center = new Vector3(0f, potHeight * 0.25f, 0f);
        solidCollider.size = new Vector3(potRadius * 2.2f, potHeight, potRadius * 2.2f);
    }

    // ── Green smoke rising from the pot ──
    private void BuildParticleSystem()
    {
        GameObject psObj = new GameObject("GreenSmoke");
        psObj.transform.SetParent(transform);
        psObj.transform.localPosition = new Vector3(0f, potHeight * 0.4f, 0f);

        smokeParticles = psObj.AddComponent<ParticleSystem>();

        var main = smokeParticles.main;
        main.startLifetime = 2.5f;
        main.startSpeed = 0.3f;
        main.startSize = new ParticleSystem.MinMaxCurve(minParticleSize, maxParticleSize);
        main.startColor = smokeColor;
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f; // float upward

        var emission = smokeParticles.emission;
        emission.rateOverTime = smokeRate;

        var shape = smokeParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = potRadius * 0.6f;

        var colorOverLifetime = smokeParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(smokeColor, 0f),
                new GradientColorKey(new Color(0.05f, 0.7f, 0.1f), 0.5f),
                new GradientColorKey(new Color(0.02f, 0.3f, 0.05f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(0.4f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var sizeOverLifetime = smokeParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(1f, 1.5f)
        ));

        // Renderer — use default particle material
        var psRenderer = psObj.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = null;
        try
        {
            particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                             Shader.Find("Particles/Standard Unlit") ??
                             Shader.Find("Mobile/Particles/Additive") ??
                             Shader.Find("Sprites/Default");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[MagicalPot] Exception while searching for shaders: " + ex.Message);
        }

        Material newMat = null;
        if (particleShader != null)
        {
            try
            {
                newMat = new Material(particleShader);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[MagicalPot] Failed to create material from shader: " + ex.Message);
            }
        }

        if (newMat == null)
        {
            Debug.LogWarning("[MagicalPot] Particle shader not found or invalid. Using fallback material.");
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Renderer quadRend = temp.GetComponent<Renderer>();
            if (quadRend != null && quadRend.sharedMaterial != null)
            {
                try
                {
                    newMat = new Material(quadRend.sharedMaterial);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[MagicalPot] Failed to create material from quad: " + ex.Message);
                }
            }
            DestroyImmediate(temp);
        }

        if (newMat != null)
        {
            psRenderer.material = newMat;
            psRenderer.material.color = smokeColor;
        }
        else
        {
            Debug.LogError("[MagicalPot] Absolutely failed to assign a particle material. Using default.");
        }
    }

    // ── Eerie green glow light ──
    private void BuildGlowLight()
    {
        GameObject lightObj = new GameObject("PotGlow");
        lightObj.transform.SetParent(transform);
        lightObj.transform.localPosition = new Vector3(0f, potHeight * 0.6f, 0f);

        potGlow = lightObj.AddComponent<Light>();
        potGlow.type = LightType.Point;
        potGlow.color = new Color(0.1f, 0.95f, 0.2f);
        potGlow.intensity = glowIntensity;
        potGlow.range = glowRange;
    }

    // ── Sphere trigger for player proximity ──
    private void BuildTriggerZone()
    {
        GameObject triggerObj = new GameObject("ProximityTrigger");
        triggerObj.transform.SetParent(transform);
        triggerObj.transform.localPosition = Vector3.zero;
        triggerObj.layer = gameObject.layer;

        SphereCollider sphere = triggerObj.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = triggerRadius;

        // Need a Rigidbody on the trigger to detect OnTriggerEnter
        Rigidbody rb = triggerObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Route trigger events back to this script
        PotTriggerRelay relay = triggerObj.AddComponent<PotTriggerRelay>();
        relay.pot = this;
    }

    // ── Floating prompt text ──
    private void BuildPromptUI()
    {
        promptUI = new GameObject("PromptUI");
        promptUI.transform.SetParent(transform);
        promptUI.transform.localPosition = new Vector3(0f, potHeight + 0.8f, 0f);

        // Use TextMesh for world-space text (works in VR without Canvas)
        TextMesh textMesh = promptUI.AddComponent<TextMesh>();
        textMesh.text = "✦ Pick up the Green Powder ✦";
        textMesh.fontSize = 32;
        textMesh.characterSize = 0.04f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(0.2f, 1f, 0.3f);
        textMesh.fontStyle = FontStyle.Bold;

        promptUI.SetActive(false);
    }

    // ─────────────────────── RUNTIME ───────────────────────

    public void OnPlayerEnterRange()
    {
        playerInRange = true;
        Debug.Log("[MagicalPot] Player entered range of the Magical Pot!");

        if (!powderSpawned)
        {
            promptUI.SetActive(true);
            SpawnGreenPowder();
        }
    }

    public void OnPlayerExitRange()
    {
        playerInRange = false;
        promptUI.SetActive(false);
        Debug.Log("[MagicalPot] Player left range of the Magical Pot.");
    }

    private void SpawnGreenPowder()
    {
        if (powderSpawned) return;
        powderSpawned = true;

        Vector3 spawnPos = transform.position + new Vector3(0f, potHeight + 0.3f, 0f);

        if (greenPowderPrefab != null)
        {
            powderInstance = Instantiate(greenPowderPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Create a default green powder object
            powderInstance = new GameObject("GreenPowder");
            powderInstance.transform.position = spawnPos;
            powderInstance.AddComponent<GreenPowder>();
        }

        Debug.Log("[MagicalPot] Green Powder spawned above the pot!");
        StartCoroutine(HidePromptAfterDelay(3f));
    }

    private IEnumerator HidePromptAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (promptUI != null) promptUI.SetActive(false);
    }

    private void Update()
    {
        // Pulse the glow light for a magical feel
        if (potGlow != null)
        {
            potGlow.intensity = glowIntensity + Mathf.Sin(Time.time * 2f) * glowPulseIntensity;
        }

        // Make prompt always face the camera
        if (promptUI != null && promptUI.activeSelf)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                promptUI.transform.LookAt(cam.transform);
                promptUI.transform.Rotate(0, 180, 0);
            }
        }
    }
}

/// <summary>
/// Small helper that sits on the trigger child object and relays
/// OnTriggerEnter / OnTriggerExit back to MagicalPot.
/// </summary>
public class PotTriggerRelay : MonoBehaviour
{
    [HideInInspector] public MagicalPot pot;

    private void OnTriggerEnter(Collider other)
    {
        // Detect AutoHand player or any CharacterController / tagged "Player"
        if (other.CompareTag("Player") ||
            other.GetComponent<CharacterController>() != null ||
            other.GetComponentInParent<CharacterController>() != null ||
            other.name.Contains("Player") ||
            other.transform.root.name.Contains("Player"))
        {
            pot.OnPlayerEnterRange();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") ||
            other.GetComponent<CharacterController>() != null ||
            other.GetComponentInParent<CharacterController>() != null ||
            other.name.Contains("Player") ||
            other.transform.root.name.Contains("Player"))
        {
            pot.OnPlayerExitRange();
        }
    }
}
