using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using Oculus.Voice;
using UnityEditor.Events;
using UnityEngine.Events;

/// <summary>
/// VoiceManagerSetupTool — ONE-CLICK SETUP WINDOW
///
/// Opens from the Unity Menu: Tools > Magic Wand > Setup Voice Manager
///
/// This tool automatically:
/// 1. Creates a "VoiceManager" GameObject in the active scene
/// 2. Adds VoiceManager + AudioSource components to it
/// 3. Finds AppVoiceExperience in the scene and assigns it
/// 4. Finds/creates a wand Point Light and assigns it
/// 5. Finds FloatingMover components and assigns them
/// 6. Finds DoorRotator component and assigns it
/// 7. Loads or creates a fire VFX GameObject and assigns it
/// 8. Creates + assigns SceneTeleporter
/// 9. Loads all spell audio clips from Assets/Sound/ and assigns them
/// 10. Adds "2nd level" scene to Build Settings
/// </summary>
public class VoiceManagerSetupTool : EditorWindow
{
    // ─── Setup state ───
    private string statusLog = "Click 'Run Full Auto-Setup' to begin.";
    private Vector2 scrollPos;
    private bool setupComplete = false;

    // ─── References found during scan ───
    private AppVoiceExperience foundVoiceExp;
    private Light foundLight;
    private FloatingMover foundFloater1;
    private FloatingMover foundFloater2;
    private DoorRotator foundDoor1;
    private DoorRotator foundDoor2;
    private GameObject foundFire;
    private SceneTeleporter foundTeleporter;
    private AudioSource foundAudioSource;

    // ─── Audio clip paths (relative to Assets/) ───
    private const string LUMOS_CLIP        = "Sound/lumos-101soundboards.mp3";
    private const string ALOHOMORA_CLIP    = "Sound/alohomora-101soundboards.mp3";
    private const string INCENDIO_CLIP     = "Sound/incendio-101soundboards.mp3";
    private const string DESCENDO_CLIP     = "Sound/decendo .WAV";
    private const string WINGARDIUM_CLIP   = "Sound/wenga.WAV";
    private const string FINITE_CLIP       = "Sound/8d82b5_HP_Hermione_Granger_Confringo_Sound_Effect.mp3";
    private const string NOX_CLIP          = "Sound/0627 (1).WAV";

    // ─── VFX / prefab paths ───
    private const string FIRE_PREFAB_PATH  = "VFXPACK_FIRE_WALLCOEUR/Prefab/VFX_Fire.prefab";

    // ─── Scene name for 2nd level ───
    private const string SECOND_LEVEL_SCENE = "Assets/Scens/2nd level.unity";

    [MenuItem("Tools/Magic Wand/🧙 Setup Voice Manager")]
    [MenuItem("Window/Magic Wand Setup")]
    public static void ShowWindow()
    {
        var win = GetWindow<VoiceManagerSetupTool>("Magic Wand Setup");
        win.minSize = new Vector2(500, 620);
        win.maxSize = new Vector2(600, 900);
    }

    private void OnGUI()
    {
        // ── Header ──
        GUILayout.Space(10);
        var headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        GUILayout.Label("🧙 Magic Wand VR — Voice Manager Setup", headerStyle);
        GUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "This tool automatically sets up the entire voice spell system in your active scene.\n" +
            "Open the scene you want to set up, then click the button below.",
            MessageType.Info);

        GUILayout.Space(10);

        // ── Current scene display ──
        string sceneName = EditorSceneManager.GetActiveScene().name;
        EditorGUILayout.LabelField("Active Scene:", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField(sceneName);
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);

        // ── Main setup button ──
        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            fixedHeight = 50
        };
        GUI.backgroundColor = setupComplete ? Color.green : new Color(0.3f, 0.7f, 1f);
        if (GUILayout.Button("▶  Run Full Auto-Setup", btnStyle))
        {
            statusLog = "";
            RunFullSetup();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(5);

        // ── Individual step buttons ──
        EditorGUILayout.LabelField("Individual Steps:", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan Scene")) ScanScene();
            if (GUILayout.Button("Add '2nd level' to Build")) AddSceneToBuildSettings();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Fire VFX")) CreateFireVFX();
            if (GUILayout.Button("Create Floating Objects")) CreateFloatingObjects();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Setup Cauldron + Teleporter")) SetupCauldronAndTeleporter();
            if (GUILayout.Button("Assign Audio Clips")) AssignAudioClips();
        }

        GUILayout.Space(10);

        // ── Status log ──
        EditorGUILayout.LabelField("Setup Log:", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos,
            GUILayout.MinHeight(200), GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(statusLog, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);
        if (GUILayout.Button("Clear Log"))
            statusLog = "";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MAIN SETUP ENTRY POINT
    // ═══════════════════════════════════════════════════════════════════════════

    private void RunFullSetup()
    {
        Log("════════════════════════════════════════");
        Log("  MAGIC WAND VOICE MANAGER — FULL SETUP");
        Log("════════════════════════════════════════");

        // Step 1: Scan scene for existing objects
        Log("\n[STEP 1] Scanning scene...");
        ScanScene();

        // Step 2: Create or find VoiceManager GameObject
        Log("\n[STEP 2] Setting up VoiceManager GameObject...");
        VoiceManager vm = SetupVoiceManagerGameObject();
        if (vm == null)
        {
            Log("ERROR: Could not create VoiceManager! Aborting.");
            return;
        }

        // Step 3: Assign AppVoiceExperience
        Log("\n[STEP 3] Assigning AppVoiceExperience...");
        AssignVoiceExperience(vm);

        // Step 4: Assign Light
        Log("\n[STEP 4] Setting up wand light...");
        AssignLight(vm);

        // Step 5: Create fire VFX
        Log("\n[STEP 5] Setting up fire VFX...");
        CreateFireVFX();
        if (foundFire != null)
        {
            SerializedObject so = new SerializedObject(vm);
            so.FindProperty("fire").objectReferenceValue = foundFire;
            so.ApplyModifiedProperties();
            Log("  ✓ Fire assigned to VoiceManager.");
        }

        // Step 6: Set up floating objects
        Log("\n[STEP 6] Setting up floating objects...");
        CreateFloatingObjects();
        AssignFloatingMovers(vm);

        // Step 7: Assign door rotator
        Log("\n[STEP 7] Setting up door...");
        AssignDoorRotator(vm);

        // Step 8: Set up cauldron + teleporter
        Log("\n[STEP 8] Setting up Magical Cauldron + SceneTeleporter...");
        SetupCauldronAndTeleporter();
        AssignTeleporter(vm);

        // Step 9: Assign audio clips
        Log("\n[STEP 9] Assigning spell audio clips...");
        AssignAudioClips(vm);

        // Step 10: Add 2nd level scene to Build Settings
        Log("\n[STEP 10] Adding '2nd level' to Build Settings...");
        AddSceneToBuildSettings();

        // Step 11: Hook up grabbable wands in scene
        Log("\n[STEP 11] Hooking up grabbable wands in scene...");
        HookUpWands(vm);

        // Step 12: Mark scene dirty and save
        Log("\n[STEP 12] Saving scene...");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        setupComplete = true;
        Log("\n════════════════════════════════════════");
        Log("  ✅ SETUP COMPLETE!");
        Log("════════════════════════════════════════");
        Log("\nIMPORTANT — Check the following:");
        Log("  1. Select the 'VoiceManager' GameObject in Hierarchy");
        Log("  2. In Inspector, verify all fields are filled (no None)");
        Log("  3. If 'Voice Experience' is still None, drag the");
        Log("     'AppVoiceExperience' component to that field manually");
        Log("  4. Ctrl+S to save the scene");
        Log("\nPRESS PLAY and say a spell!");
        Repaint();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INDIVIDUAL SETUP STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Scans the active scene for all relevant components.</summary>
    private void ScanScene()
    {
        // AppVoiceExperience
        foundVoiceExp = Object.FindObjectOfType<AppVoiceExperience>();
        if (foundVoiceExp != null) Log($"  ✓ AppVoiceExperience found: '{foundVoiceExp.gameObject.name}'");
        else Log("  ✗ AppVoiceExperience NOT found in scene.");

        // Lights — prefer a Point or Spot light (wand light), fall back to Directional
        Light[] allLights = Object.FindObjectsOfType<Light>();
        foundLight = null;
        foreach (var l in allLights)
        {
            if (l.type == LightType.Point || l.type == LightType.Spot)
            {
                foundLight = l;
                break;
            }
        }
        if (foundLight == null && allLights.Length > 0)
            foundLight = allLights[0];

        if (foundLight != null) Log($"  ✓ Light found: '{foundLight.gameObject.name}' ({foundLight.type})");
        else Log("  ✗ No Light found in scene — will create one.");

        // FloatingMovers
        FloatingMover[] movers = Object.FindObjectsOfType<FloatingMover>();
        foundFloater1 = movers.Length > 0 ? movers[0] : null;
        foundFloater2 = movers.Length > 1 ? movers[1] : null;
        Log($"  ✓ FloatingMovers found: {movers.Length}");

        // DoorRotator
        DoorRotator[] doors = Object.FindObjectsOfType<DoorRotator>();
        foundDoor1 = doors.Length > 0 ? doors[0] : null;
        foundDoor2 = doors.Length > 1 ? doors[1] : null;
        if (foundDoor1 != null) Log($"  ✓ DoorRotator 1 found: '{foundDoor1.gameObject.name}'");
        if (foundDoor2 != null) Log($"  ✓ DoorRotator 2 found: '{foundDoor2.gameObject.name}'");
        if (foundDoor1 == null && foundDoor2 == null) Log("  ✗ DoorRotator NOT found — will try to add to a Door object.");

        // Fire (any inactive or active object with 'fire' in name)
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name.ToLower().Contains("fire") && go.scene.IsValid())
            {
                foundFire = go;
                break;
            }
        }
        if (foundFire != null) Log($"  ✓ Fire object found: '{foundFire.name}'");
        else Log("  ✗ No fire object — will instantiate from prefab.");

        // SceneTeleporter
        foundTeleporter = Object.FindObjectOfType<SceneTeleporter>();
        if (foundTeleporter != null) Log($"  ✓ SceneTeleporter found: '{foundTeleporter.gameObject.name}'");
        else Log("  ✗ SceneTeleporter not found — will create.");

        Log("  → Scan complete.");
    }

    /// <summary>Creates or finds the VoiceManager GameObject and returns its VoiceManager component.</summary>
    private VoiceManager SetupVoiceManagerGameObject()
    {
        // Check if one already exists
        VoiceManager existing = Object.FindObjectOfType<VoiceManager>();
        if (existing != null)
        {
            Log($"  ✓ VoiceManager already exists on '{existing.gameObject.name}'");
            EnsureAudioSource(existing.gameObject);
            return existing;
        }

        // Create new GameObject
        GameObject vmGO = new GameObject("VoiceManager");
        Undo.RegisterCreatedObjectUndo(vmGO, "Create VoiceManager");

        // Add components
        VoiceManager vm = vmGO.AddComponent<VoiceManager>();
        EnsureAudioSource(vmGO);

        Log($"  ✓ Created GameObject 'VoiceManager' with VoiceManager + AudioSource.");
        return vm;
    }

    private AudioSource EnsureAudioSource(GameObject go)
    {
        AudioSource src = go.GetComponent<AudioSource>();
        if (src == null)
        {
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D audio for spell sounds
            Log("  ✓ Added AudioSource to VoiceManager.");
        }
        foundAudioSource = src;
        return src;
    }

    private void AssignVoiceExperience(VoiceManager vm)
    {
        if (foundVoiceExp == null)
        {
            // Try finding it on the VoiceManager's own GameObject or children
            foundVoiceExp = vm.GetComponentInChildren<AppVoiceExperience>();
            if (foundVoiceExp == null)
            {
                // Try finding it globally in the scene
                foundVoiceExp = Object.FindObjectOfType<AppVoiceExperience>();
            }
        }

        if (foundVoiceExp == null)
        {
            Log("  ⚠ AppVoiceExperience not found in scene. Creating a new 'App Voice Experience' GameObject...");
            GameObject voiceGO = new GameObject("App Voice Experience");
            Undo.RegisterCreatedObjectUndo(voiceGO, "Create App Voice Experience");
            foundVoiceExp = voiceGO.AddComponent<AppVoiceExperience>();
        }

        // Find WitConfiguration in the project, prioritizing "voice" or "voice sdk"
        ScriptableObject bestConfig = null;
        string bestPath = "";
        string[] guids = AssetDatabase.FindAssets("t:WitConfiguration");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (config != null)
            {
                bool isVoiceName = string.Equals(config.name, "voice", System.StringComparison.OrdinalIgnoreCase);
                bool isInVoiceSdk = path.ToLower().Contains("voice sdk");

                if (isVoiceName || isInVoiceSdk)
                {
                    bestConfig = config;
                    bestPath = path;
                    break;
                }

                if (bestConfig == null)
                {
                    bestConfig = config;
                    bestPath = path;
                }
            }
        }

        if (bestConfig != null)
        {
            SerializedObject voiceExpSO = new SerializedObject(foundVoiceExp);
            var runtimeConfigProp = voiceExpSO.FindProperty("witRuntimeConfiguration");
            if (runtimeConfigProp != null)
            {
                var witConfigProp = runtimeConfigProp.FindPropertyRelative("witConfiguration");
                if (witConfigProp != null)
                {
                    if (witConfigProp.objectReferenceValue != bestConfig)
                    {
                        witConfigProp.objectReferenceValue = bestConfig;
                        voiceExpSO.ApplyModifiedProperties();
                        Log($"    ✓ Configured AppVoiceExperience with WitConfiguration asset: '{bestConfig.name}' (from '{bestPath}')");
                    }
                    else
                    {
                        Log($"    ✓ AppVoiceExperience is already configured with WitConfiguration asset: '{bestConfig.name}'");
                    }
                }
            }
        }
        else
        {
            Log("    ⚠ No WitConfiguration asset found in project. Please configure it manually in the inspector.");
        }

        SerializedObject so = new SerializedObject(vm);
        so.FindProperty("voiceExperience").objectReferenceValue = foundVoiceExp;
        so.ApplyModifiedProperties();
        Log($"  ✓ AppVoiceExperience assigned: '{foundVoiceExp.gameObject.name}'");
    }

    private void HookUpWands(VoiceManager vm)
    {
        var grabbables = Object.FindObjectsOfType<Autohand.Grabbable>();
        int hookedCount = 0;
        foreach (var g in grabbables)
        {
            if (g.name.ToLower().Contains("wand"))
            {
                // Clear existing ActivateVoice persistent listeners to avoid duplicates
                for (int i = g.onSqueeze.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    if (g.onSqueeze.GetPersistentMethodName(i) == "ActivateVoice")
                    {
                        UnityEventTools.RemovePersistentListener(g.onSqueeze, i);
                    }
                }

                // Add persistent listener
                UnityEventTools.AddVoidPersistentListener(g.onSqueeze, vm.ActivateVoice);
                hookedCount++;
                Log($"  ✓ Hooked up squeeze trigger on wand '{g.name}' to VoiceManager.ActivateVoice()");
            }
        }

        if (hookedCount == 0)
        {
            Log("  ⚠ No grabbable wands found in scene (searched for Grabbables with 'wand' in their name).");
            Log("  → If you have a wand object, make sure it has the Autohand.Grabbable component and has 'wand' in its name.");
        }
        else
        {
            Log($"  ✓ Successfully hooked up {hookedCount} wand(s).");
        }
    }

    private void AssignLight(VoiceManager vm)
    {
        if (foundLight == null)
        {
            // Create a wand point light
            GameObject lightGO = new GameObject("WandLight");
            Undo.RegisterCreatedObjectUndo(lightGO, "Create WandLight");
            foundLight = lightGO.AddComponent<Light>();
            foundLight.type = LightType.Point;
            foundLight.color = new Color(1f, 0.97f, 0.7f);
            foundLight.intensity = 3f;
            foundLight.range = 8f;
            foundLight.enabled = false; // starts off — Lumos turns it on
            lightGO.transform.position = new Vector3(0f, 1.5f, 0f);
            Log("  ✓ Created 'WandLight' Point Light (starts OFF — Lumos activates it).");
        }

        SerializedObject so = new SerializedObject(vm);
        so.FindProperty("light").objectReferenceValue = foundLight;
        so.ApplyModifiedProperties();
        Log($"  ✓ Light assigned: '{foundLight.gameObject.name}'");
    }

    private void CreateFireVFX()
    {
        if (foundFire != null) return; // already found in scene

        // Try to load and instantiate the fire prefab
        string prefabPath = "Assets/" + FIRE_PREFAB_PATH;
        GameObject firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (firePrefab != null)
        {
            foundFire = (GameObject)PrefabUtility.InstantiatePrefab(firePrefab);
            foundFire.name = "FireVFX_Incendio";
            foundFire.transform.position = new Vector3(0f, 0f, 2f); // in front of start position
            foundFire.SetActive(false); // Incendio activates it
            Undo.RegisterCreatedObjectUndo(foundFire, "Create Fire VFX");
            Log($"  ✓ Fire VFX instantiated from prefab: '{prefabPath}'");
            Log("  → Fire starts INACTIVE. 'Incendio' will activate it.");
        }
        else
        {
            // Fallback: create a simple particle fire
            foundFire = CreateSimpleFireParticles();
            Log("  ⚠ Could not find fire prefab at: " + prefabPath);
            Log("  → Created a simple particle fire as fallback.");
        }
    }

    private GameObject CreateSimpleFireParticles()
    {
        GameObject fireGO = new GameObject("FireEffect_Incendio");
        Undo.RegisterCreatedObjectUndo(fireGO, "Create Fire Effect");
        fireGO.transform.position = new Vector3(0f, 0f, 2f);

        ParticleSystem ps = fireGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new Color(1f, 0.4f, 0f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSpeed = 1f;
        main.startLifetime = 0.8f;
        main.maxParticles = 100;
        main.gravityModifier = -0.3f;

        var emission = ps.emission;
        emission.rateOverTime = 40f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.5f, 0f), 0f),
                new GradientColorKey(new Color(0.8f, 0.1f, 0f), 0.5f),
                new GradientColorKey(Color.black, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        // Add a point light for the fire glow
        GameObject glowGO = new GameObject("FireGlow");
        glowGO.transform.SetParent(fireGO.transform);
        glowGO.transform.localPosition = Vector3.zero;
        Light fireLight = glowGO.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.4f, 0.1f);
        fireLight.intensity = 2f;
        fireLight.range = 4f;

        fireGO.SetActive(false);
        return fireGO;
    }

    private void CreateFloatingObjects()
    {
        if (foundFloater1 != null) return; // already exists

        // Create two floating objects (books/orbs) for Wingardium Leviosa demo
        foundFloater1 = CreateFloatingObject("FloatingBook_1", new Vector3(-1f, 0.8f, 2f));
        foundFloater2 = CreateFloatingObject("FloatingBook_2", new Vector3(1f, 0.8f, 2f));
        Log("  ✓ Created 2 floating objects for Wingardium Leviosa / Descendo.");
        Log("  → You can move them to any position in your scene.");
    }

    private FloatingMover CreateFloatingObject(string objName, Vector3 position)
    {
        // Check if object already exists
        GameObject existing = GameObject.Find(objName);
        if (existing != null)
            return existing.GetComponent<FloatingMover>() ?? existing.AddComponent<FloatingMover>();

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objName;
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.2f, 0.04f, 0.3f); // book-like

        // Apply a magical blue material
        Renderer rend = go.GetComponent<Renderer>();
        Material mat = new Material(
            Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Standard"));
        mat.color = new Color(0.15f, 0.25f, 0.9f, 1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.05f, 0.1f, 0.4f) * 2f);
        rend.material = mat;

        FloatingMover fm = go.AddComponent<FloatingMover>();
        Undo.RegisterCreatedObjectUndo(go, "Create Floating Object");
        return fm;
    }

    private void AssignFloatingMovers(VoiceManager vm)
    {
        // Re-scan in case we just created them
        FloatingMover[] movers = Object.FindObjectsOfType<FloatingMover>();
        foundFloater1 = movers.Length > 0 ? movers[0] : foundFloater1;
        foundFloater2 = movers.Length > 1 ? movers[1] : foundFloater2;

        SerializedObject so = new SerializedObject(vm);
        if (foundFloater1 != null)
        {
            so.FindProperty("floatingMover").objectReferenceValue = foundFloater1;
            Log($"  ✓ floatingMover assigned: '{foundFloater1.gameObject.name}'");
        }
        if (foundFloater2 != null)
        {
            so.FindProperty("floatingMover1").objectReferenceValue = foundFloater2;
            Log($"  ✓ floatingMover1 assigned: '{foundFloater2.gameObject.name}'");
        }
        so.ApplyModifiedProperties();
    }

    private void AssignDoorRotator(VoiceManager vm)
    {
        // Re-scan
        DoorRotator[] doors = Object.FindObjectsOfType<DoorRotator>();
        foundDoor1 = doors.Length > 0 ? doors[0] : null;
        foundDoor2 = doors.Length > 1 ? doors[1] : null;

        if (foundDoor1 == null)
        {
            // Look for any GameObject with "door" in its name and add DoorRotator to it
            List<GameObject> doorGOs = FindGameObjectsByNameContains("door");
            if (doorGOs.Count == 0) doorGOs = FindGameObjectsByNameContains("Door");

            if (doorGOs.Count > 0)
            {
                foundDoor1 = doorGOs[0].AddComponent<DoorRotator>();
                Log($"  ✓ Added DoorRotator to existing door 1: '{doorGOs[0].name}'");
                if (doorGOs.Count > 1)
                {
                    foundDoor2 = doorGOs[1].AddComponent<DoorRotator>();
                    Log($"  ✓ Added DoorRotator to existing door 2: '{doorGOs[1].name}'");
                }
            }
            else
            {
                // Create a placeholder door
                GameObject newDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                newDoor.name = "Door_Alohomora";
                newDoor.transform.position = new Vector3(0f, 1f, 3f);
                newDoor.transform.localScale = new Vector3(1f, 2f, 0.1f);
                Undo.RegisterCreatedObjectUndo(newDoor, "Create Door");
                foundDoor1 = newDoor.AddComponent<DoorRotator>();
                Log("  ✓ Created placeholder door 'Door_Alohomora' — move it to your actual door position.");
            }
        }
        else
        {
            Log($"  ✓ DoorRotators found: {doors.Length}");
        }

        SerializedObject so = new SerializedObject(vm);
        if (foundDoor1 != null)
        {
            so.FindProperty("doorRotator").objectReferenceValue = foundDoor1;
            Log($"  ✓ doorRotator assigned: '{foundDoor1.gameObject.name}'");
        }
        if (foundDoor2 != null)
        {
            so.FindProperty("doorRotator1").objectReferenceValue = foundDoor2;
            Log($"  ✓ doorRotator1 assigned: '{foundDoor2.gameObject.name}'");
        }
        so.ApplyModifiedProperties();
    }

    private void SetupCauldronAndTeleporter()
    {
        // Check if MagicalPotSetup or SceneTeleporter already exists
        foundTeleporter = Object.FindObjectOfType<SceneTeleporter>();
        MagicalPot existingPot = Object.FindObjectOfType<MagicalPot>();

        if (foundTeleporter != null)
        {
            Log($"  ✓ SceneTeleporter already exists: '{foundTeleporter.gameObject.name}'");
            return;
        }

        // Create the MagicalChallengeArea
        GameObject challengeArea = new GameObject("MagicalChallengeArea");
        Undo.RegisterCreatedObjectUndo(challengeArea, "Create Challenge Area");
        challengeArea.transform.position = new Vector3(5f, 0f, 5f);

        // Add MagicalPot
        if (existingPot == null)
        {
            GameObject potGO = new GameObject("MagicalPot");
            potGO.transform.SetParent(challengeArea.transform);
            potGO.transform.localPosition = Vector3.zero;
            potGO.AddComponent<MagicalPot>();
            Log("  ✓ MagicalPot created inside MagicalChallengeArea.");
        }

        // Add SceneTeleporter to the challenge area
        foundTeleporter = challengeArea.AddComponent<SceneTeleporter>();
        foundTeleporter.targetSceneName = "2nd level";
        Log("  ✓ SceneTeleporter created on MagicalChallengeArea.");
        Log("  → Move 'MagicalChallengeArea' to where you want the cauldron in your scene.");
    }

    private void AssignTeleporter(VoiceManager vm)
    {
        foundTeleporter = Object.FindObjectOfType<SceneTeleporter>();
        if (foundTeleporter == null) return;

        // Also assign the VoiceManager's audio source to the teleporter
        AudioSource vmAudio = vm.GetComponent<AudioSource>();
        if (vmAudio != null)
        {
            SerializedObject stSO = new SerializedObject(foundTeleporter);
            stSO.FindProperty("audioSource").objectReferenceValue = vmAudio;
            stSO.ApplyModifiedProperties();
        }

        SerializedObject so = new SerializedObject(vm);
        so.FindProperty("sceneTeleporter").objectReferenceValue = foundTeleporter;
        so.ApplyModifiedProperties();
        Log($"  ✓ SceneTeleporter assigned to VoiceManager: '{foundTeleporter.gameObject.name}'");
    }

    private void AssignAudioClips(VoiceManager vm = null)
    {
        if (vm == null) vm = Object.FindObjectOfType<VoiceManager>();
        if (vm == null)
        {
            Log("  ✗ No VoiceManager found — run full setup first.");
            return;
        }

        SerializedObject so = new SerializedObject(vm);

        // Also assign the AudioSource (the one on the same GameObject)
        AudioSource audioSrc = vm.GetComponent<AudioSource>();
        if (audioSrc != null)
        {
            so.FindProperty("audioSource").objectReferenceValue = audioSrc;
            Log("  ✓ AudioSource assigned to VoiceManager.");
        }

        // Assign spell clips
        TryAssignAudioClip(so, "lumos",             LUMOS_CLIP,      "Lumos");
        TryAssignAudioClip(so, "alohomora",         ALOHOMORA_CLIP,  "Alohomora");
        TryAssignAudioClip(so, "incendio",          INCENDIO_CLIP,   "Incendio");
        TryAssignAudioClip(so, "descendo",          DESCENDO_CLIP,   "Descendo");
        TryAssignAudioClip(so, "wingardiumLeviosa", WINGARDIUM_CLIP, "Wingardium Leviosa");
        TryAssignAudioClip(so, "finiteIncantatem",  FINITE_CLIP,     "Finite Incantatem");
        TryAssignAudioClip(so, "nox",               NOX_CLIP,        "Nox");

        // Note about missing clips
        Log("  ⚠ No audio clips found for: greatHall");
        Log("    → Add a 'Great Hall' sound to Assets/Sound/ and assign it manually.");
        Log("  ⚠ No audio clip for unrecognizedSpellSound");
        Log("    → Add any sound file and assign it to VoiceManager.unrecognizedSpellSound manually.");

        so.ApplyModifiedProperties();
        Log("  ✓ Audio clips assignment done.");
    }

    private void TryAssignAudioClip(SerializedObject so, string fieldName, string assetRelPath, string spellName)
    {
        string fullPath = "Assets/" + assetRelPath;
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(fullPath);
        if (clip != null)
        {
            so.FindProperty(fieldName).objectReferenceValue = clip;
            Log($"  ✓ {spellName} clip assigned: '{Path.GetFileName(assetRelPath)}'");
        }
        else
        {
            Log($"  ✗ {spellName} clip NOT found at: {fullPath}");
            Log($"    → Assign manually in Inspector.");
        }
    }

    private void AddSceneToBuildSettings()
    {
        string scenePath = SECOND_LEVEL_SCENE;

        // Check if already present
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.path == scenePath)
            {
                Log($"  ✓ '{scenePath}' is already in Build Settings.");
                return;
            }
        }

        // Add it
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        Log($"  ✓ Added '{scenePath}' to Build Settings!");
        Log("    The '2nd level' scene can now be loaded by SceneManager.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // UTILITY
    // ═══════════════════════════════════════════════════════════════════════════

    private GameObject FindGameObjectByNameContains(string keyword)
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.scene.IsValid() && go.name.ToLower().Contains(keyword.ToLower()))
                return go;
        }
        return null;
    }

    private List<GameObject> FindGameObjectsByNameContains(string keyword)
    {
        List<GameObject> results = new List<GameObject>();
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.scene.IsValid() && go.name.ToLower().Contains(keyword.ToLower()))
            {
                // Avoid adding to child details/colliders if parent also matches
                if (go.transform.parent != null && go.transform.parent.name.ToLower().Contains(keyword.ToLower()))
                {
                    continue;
                }
                results.Add(go);
            }
        }
        return results;
    }

    private void Log(string message)
    {
        statusLog += message + "\n";
        Debug.Log("[MagicWandSetup] " + message);
        Repaint();
    }
}
