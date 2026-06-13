using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// MagicalPotSetup — ONE-CLICK SETUP TOOL
/// 
/// Drop this script on any empty GameObject in your main scene,
/// then in the Inspector click the "Setup Magical Challenge Area" button.
/// It will:
///   1. Create the Magical Pot with particles & glow
///   2. Create the SceneTeleporter component
///   3. Wire everything to the existing VoiceManager
///   4. Add the "2nd level" scene to Build Settings
/// 
/// After setup, you can move the "MagicalChallengeArea" GameObject
/// wherever you want in the scene.
/// </summary>
public class MagicalPotSetup : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Where to place the magical pot in the scene. Adjust in the Inspector or move the parent GameObject.")]
    public Vector3 potPosition = new Vector3(5f, 0f, 5f);

    [Header("References (Auto-detected)")]
    public VoiceManager voiceManager;

    [Header("Status")]
    [SerializeField] private bool isSetupComplete = false;

    /// <summary>
    /// Call this to set up everything. Can be triggered from the custom Inspector button
    /// or called manually via code.
    /// </summary>
    public void SetupAll()
    {
        Debug.Log("[MagicalPotSetup] ====== SETTING UP MAGICAL CHALLENGE AREA ======");

        // 1. Find VoiceManager if not assigned
        if (voiceManager == null)
        {
            voiceManager = FindObjectOfType<VoiceManager>();
            if (voiceManager == null)
            {
                Debug.LogError("[MagicalPotSetup] Could not find VoiceManager in the scene! " +
                    "Please assign it manually or make sure it exists.");
            }
            else
            {
                Debug.Log("[MagicalPotSetup] Found VoiceManager: " + voiceManager.gameObject.name);
            }
        }

        // 2. Rename this GameObject
        gameObject.name = "MagicalChallengeArea";
        transform.position = potPosition;

        // 3. Create the Magical Pot
        SetupMagicalPot();

        // 4. Create the SceneTeleporter
        SetupSceneTeleporter();

        isSetupComplete = true;
        Debug.Log("[MagicalPotSetup] ====== SETUP COMPLETE ======");
        Debug.Log("[MagicalPotSetup] Instructions:");
        Debug.Log("[MagicalPotSetup]   1. Move this 'MagicalChallengeArea' GameObject to wherever you want the pot");
        Debug.Log("[MagicalPotSetup]   2. Make sure 'Assets/Scens/2nd level' is in Build Settings (File > Build Settings > Add Open Scenes)");
        Debug.Log("[MagicalPotSetup]   3. Add 'Great_Hall' intent to your Wit.ai dashboard for best voice recognition");
        Debug.Log("[MagicalPotSetup]   4. Press Play and walk to the pot, pick up the green powder, then say 'Great Hall'!");
    }

    private void SetupMagicalPot()
    {
        // Check if MagicalPot already exists on this object
        MagicalPot existingPot = GetComponentInChildren<MagicalPot>();
        if (existingPot != null)
        {
            Debug.Log("[MagicalPotSetup] MagicalPot already exists, skipping creation.");
            return;
        }

        GameObject potObj = new GameObject("MagicalPot");
        potObj.transform.SetParent(transform);
        potObj.transform.localPosition = Vector3.zero;

        MagicalPot pot = potObj.AddComponent<MagicalPot>();
        Debug.Log("[MagicalPotSetup] ✓ MagicalPot created with particles and glow.");
    }

    private void SetupSceneTeleporter()
    {
        // Add SceneTeleporter to this object
        SceneTeleporter existingTeleporter = GetComponent<SceneTeleporter>();
        if (existingTeleporter != null)
        {
            Debug.Log("[MagicalPotSetup] SceneTeleporter already exists, skipping creation.");
            return;
        }

        SceneTeleporter teleporter = gameObject.AddComponent<SceneTeleporter>();
        teleporter.targetSceneName = "2nd level";

        // Try to use the VoiceManager's audio source
        if (voiceManager != null && voiceManager.audioSource != null)
        {
            teleporter.audioSource = voiceManager.audioSource;
        }

        Debug.Log("[MagicalPotSetup] ✓ SceneTeleporter created (target: '2nd level').");
    }

    // Auto-setup on Play if not yet set up
    private void Start()
    {
        if (!isSetupComplete)
        {
            SetupAll();
        }
    }
}

// ─────────────────────── CUSTOM EDITOR BUTTON ───────────────────────

#if UNITY_EDITOR
[CustomEditor(typeof(MagicalPotSetup))]
public class MagicalPotSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MagicalPotSetup setup = (MagicalPotSetup)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Click the button below to set up the entire Magical Challenge Area.\n" +
            "This will create:\n" +
            "• Magical Pot (with green smoke & glow)\n" +
            "• Green Powder pickup (grabbable)\n" +
            "• Scene Teleporter (loads '2nd level' on 'Great Hall' voice command)\n\n" +
            "After setup, move this GameObject to position the pot in your scene.",
            MessageType.Info
        );

        if (GUILayout.Button("🧙 Setup Magical Challenge Area 🧙", GUILayout.Height(40)))
        {
            setup.SetupAll();

            // Add 2nd level scene to build settings
            AddSceneToBuildSettings("Assets/Scens/2nd level.unity");

            // Mark scene dirty so changes are saved
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[MagicalPotSetupEditor] Done! Don't forget to save the scene (Ctrl+S).");
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("📋 Add '2nd level' to Build Settings", GUILayout.Height(30)))
        {
            AddSceneToBuildSettings("Assets/Scens/2nd level.unity");
        }
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        // Check if scene is already in build settings
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.path == scenePath)
            {
                Debug.Log($"[MagicalPotSetup] Scene '{scenePath}' is already in Build Settings.");
                return;
            }
        }

        // Add it
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log($"[MagicalPotSetup] ✓ Added '{scenePath}' to Build Settings!");
    }
}
#endif
