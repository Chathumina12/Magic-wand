using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// Editor script to automatically generate and wire up a VR-compatible World-Space Menu Canvas
/// and automatically attach volume linkers to all AudioSources in the scene.
/// </summary>
public class CreateGameMenuTool : EditorWindow
{
    [MenuItem("Tools/Magic Wand/📋 Create Game Menu UI")]
    public static void CreateMenu()
    {
        // 1. Create World Space Canvas
        GameObject canvasGO = new GameObject("GameMenuCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Game Menu Canvas");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Set size of canvas in pixels and scale down so it matches World-Space physical sizes (approx 2.5m wide)
        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(500, 450);
        canvasRect.localScale = new Vector3(0.005f, 0.005f, 0.005f); // Ideal scale for VR interfaces

        // Attach controller component
        GameMenuController controller = canvasGO.AddComponent<GameMenuController>();

        // 2. Create Panel Background
        DefaultControls.Resources uiResources = new DefaultControls.Resources();
        GameObject panelGO = DefaultControls.CreatePanel(uiResources);
        panelGO.name = "BackgroundPanel";
        panelGO.transform.SetParent(canvasGO.transform, false);
        
        Image panelImage = panelGO.GetComponent<Image>();
        panelImage.color = new Color(0.12f, 0.12f, 0.16f, 0.95f); // Deep slate dark panel
        
        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        controller.menuPanel = panelGO;

        // 3. Create Menu Header/Title Text
        GameObject titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(panelGO.transform, false);
        Text titleText = titleGO.AddComponent<Text>();
        titleText.text = "🧙 MAGIC WAND VR MENU 🧙";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 28;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.9f, 0.75f, 0.3f, 1f); // Warm gold title text
        
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.85f);
        titleRect.anchorMax = new Vector2(1f, 0.98f);
        titleRect.sizeDelta = Vector2.zero;

        // 4. Create Buttons
        // Button 1: Start
        GameObject startBtnGO = DefaultControls.CreateButton(uiResources);
        startBtnGO.name = "StartButton";
        startBtnGO.transform.SetParent(panelGO.transform, false);
        startBtnGO.GetComponentInChildren<Text>().text = "START / RESUME";
        startBtnGO.GetComponentInChildren<Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        startBtnGO.GetComponentInChildren<Text>().fontSize = 16;
        Button startBtn = startBtnGO.GetComponent<Button>();
        controller.startButton = startBtn; // Link reference

        RectTransform startBtnRect = startBtnGO.GetComponent<RectTransform>();
        startBtnRect.sizeDelta = new Vector2(250, 45);
        startBtnRect.anchoredPosition = new Vector3(0, 100, 0);

        // Button 2: Restart
        GameObject restartBtnGO = DefaultControls.CreateButton(uiResources);
        restartBtnGO.name = "RestartButton";
        restartBtnGO.transform.SetParent(panelGO.transform, false);
        restartBtnGO.GetComponentInChildren<Text>().text = "RESTART LEVEL";
        restartBtnGO.GetComponentInChildren<Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        restartBtnGO.GetComponentInChildren<Text>().fontSize = 16;
        Button restartBtn = restartBtnGO.GetComponent<Button>();
        controller.restartButton = restartBtn; // Link reference

        RectTransform restartBtnRect = restartBtnGO.GetComponent<RectTransform>();
        restartBtnRect.sizeDelta = new Vector2(250, 45);
        restartBtnRect.anchoredPosition = new Vector3(0, 45, 0);

        // Button 3: Quit
        GameObject quitBtnGO = DefaultControls.CreateButton(uiResources);
        quitBtnGO.name = "QuitButton";
        quitBtnGO.transform.SetParent(panelGO.transform, false);
        quitBtnGO.GetComponentInChildren<Text>().text = "QUIT TO DESKTOP";
        quitBtnGO.GetComponentInChildren<Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        quitBtnGO.GetComponentInChildren<Text>().fontSize = 16;
        Button quitBtn = quitBtnGO.GetComponent<Button>();
        controller.quitButton = quitBtn; // Link reference

        RectTransform quitBtnRect = quitBtnGO.GetComponent<RectTransform>();
        quitBtnRect.sizeDelta = new Vector2(250, 45);
        quitBtnRect.anchoredPosition = new Vector3(0, -10, 0);

        // 5. Create Sliders
        // Slider 1: BGM Volume
        GameObject bgmLabelGO = new GameObject("BGMLabel");
        bgmLabelGO.transform.SetParent(panelGO.transform, false);
        Text bgmLabel = bgmLabelGO.AddComponent<Text>();
        bgmLabel.text = "Background Music (BGM)";
        bgmLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bgmLabel.fontSize = 14;
        bgmLabel.color = Color.white;
        bgmLabel.alignment = TextAnchor.MiddleLeft;
        
        RectTransform bgmLabelRect = bgmLabelGO.GetComponent<RectTransform>();
        bgmLabelRect.sizeDelta = new Vector2(250, 20);
        bgmLabelRect.anchoredPosition = new Vector3(0, -75, 0);

        GameObject bgmSliderGO = DefaultControls.CreateSlider(uiResources);
        bgmSliderGO.name = "BGMSlider";
        bgmSliderGO.transform.SetParent(panelGO.transform, false);
        Slider bgmSlider = bgmSliderGO.GetComponent<Slider>();
        bgmSlider.minValue = 0f;
        bgmSlider.maxValue = 1f;
        controller.bgmSlider = bgmSlider;

        RectTransform bgmSliderRect = bgmSliderGO.GetComponent<RectTransform>();
        bgmSliderRect.sizeDelta = new Vector2(250, 20);
        bgmSliderRect.anchoredPosition = new Vector3(0, -95, 0);

        // Slider 2: VFX Volume
        GameObject vfxLabelGO = new GameObject("VFXLabel");
        vfxLabelGO.transform.SetParent(panelGO.transform, false);
        Text vfxLabel = vfxLabelGO.AddComponent<Text>();
        vfxLabel.text = "Sound Effects (VFX)";
        vfxLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        vfxLabel.fontSize = 14;
        vfxLabel.color = Color.white;
        vfxLabel.alignment = TextAnchor.MiddleLeft;
        
        RectTransform vfxLabelRect = vfxLabelGO.GetComponent<RectTransform>();
        vfxLabelRect.sizeDelta = new Vector2(250, 20);
        vfxLabelRect.anchoredPosition = new Vector3(0, -130, 0);

        GameObject vfxSliderGO = DefaultControls.CreateSlider(uiResources);
        vfxSliderGO.name = "VFXSlider";
        vfxSliderGO.transform.SetParent(panelGO.transform, false);
        Slider vfxSlider = vfxSliderGO.GetComponent<Slider>();
        vfxSlider.minValue = 0f;
        vfxSlider.maxValue = 1f;
        controller.vfxSlider = vfxSlider;

        RectTransform vfxSliderRect = vfxSliderGO.GetComponent<RectTransform>();
        vfxSliderRect.sizeDelta = new Vector2(250, 20);
        vfxSliderRect.anchoredPosition = new Vector3(0, -150, 0);

        // 6. Automatically Link Scene AudioSources
        int linkedAudioCount = 0;
        AudioSource[] allAudioSources = Object.FindObjectsOfType<AudioSource>(true);
        foreach (AudioSource source in allAudioSources)
        {
            // Skip the audio source attached to the menu canvas itself
            if (source.transform.IsChildOf(canvasGO.transform)) continue;

            SoundVolumeLinker linker = source.GetComponent<SoundVolumeLinker>();
            if (linker == null)
            {
                linker = source.gameObject.AddComponent<SoundVolumeLinker>();
                
                // Categorize BGM vs VFX based on the object's name
                string name = source.gameObject.name.ToLower();
                if (name.Contains("music") || name.Contains("bgm") || name.Contains("loop") || name.Contains("theme") || name.Contains("background"))
                {
                    linker.soundType = SoundVolumeLinker.SoundType.BGM;
                }
                else
                {
                    linker.soundType = SoundVolumeLinker.SoundType.VFX;
                }
                
                linker.maxLocalVolume = source.volume; // Set original volume as max scale
                Undo.RegisterCreatedObjectUndo(linker, "Auto-add SoundVolumeLinker");
                linkedAudioCount++;
            }
        }

        // 7. Position Canvas in front of VR Camera (if present)
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 targetPos = cam.transform.position + cam.transform.forward * 2f;
            targetPos.y = cam.transform.position.y;
            canvasGO.transform.position = targetPos;
            canvasGO.transform.LookAt(new Vector3(cam.transform.position.x, canvasGO.transform.position.y, cam.transform.position.z));
            canvasGO.transform.Rotate(0f, 180f, 0f); // face player
        }
        else
        {
            canvasGO.transform.position = new Vector3(0f, 1.5f, 2f);
        }

        // Save modifications to the scene
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        
        string resultMessage = $"A complete World-Space VR Game Menu has been successfully created in your scene!\n\n" +
                               $"* Sliders & Buttons configured\n" +
                               $"* AudioSources linked to volume controller: {linkedAudioCount}";
                               
        EditorUtility.DisplayDialog("Create Game Menu", resultMessage, "OK");
        Debug.Log($"[CreateGameMenuTool] {resultMessage}");
    }
}
