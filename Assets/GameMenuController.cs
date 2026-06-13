using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// Controls the VR/Game Menu UI panel. Handles start, restart, quit, and volume controls.
/// Toggles the menu via Esc key or Left VR Controller's X Button (Primary Button).
/// </summary>
public class GameMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("The main container panel holding all the menu UI items.")]
    public GameObject menuPanel;

    [Header("Buttons")]
    public Button startButton;
    public Button restartButton;
    public Button quitButton;

    [Header("Volume Sliders")]
    public Slider bgmSlider;
    public Slider vfxSlider;

    [Header("VR Laser Pointer Links (Optional)")]
    [Tooltip("Laser pointer GameObject on the Left VR Hand that should activate when menu is open.")]
    public GameObject leftHandPointer;
    [Tooltip("Laser pointer GameObject on the Right VR Hand that should activate when menu is open.")]
    public GameObject rightHandPointer;

    [Header("Restart Settings")]
    [Tooltip("The name of the start/demo scene to load when restarting.")]
    public string restartSceneName = "demo";

    [Header("Custom UI Styling (Optional Placeholders)")]
    [Tooltip("Drop a custom background sprite/texture here to style the panel.")]
    public Sprite customBackground;
    [Tooltip("Drop a custom button sprite/texture here to style all buttons.")]
    public Sprite customButtonGraphic;
    [Tooltip("Drop a custom slider handle sprite/texture here to style slider handles.")]
    public Sprite customSliderHandle;

    private bool isMenuOpen = false; // Start directly in-game
    private bool wasXButtonPressedLastFrame = false;

    private void Start()
    {
        // 1. Programmatically assign button click events at runtime (guarantees buttons trigger)
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
            startButton.onClick.AddListener(StartGame);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
            restartButton.onClick.AddListener(RestartGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
            quitButton.onClick.AddListener(QuitGame);
        }

        // 2. Apply Custom Graphics Placeholders (if assigned)
        ApplyCustomGraphics();

        // 3. Hook up sliders and load saved volumes
        if (bgmSlider != null)
        {
            bgmSlider.value = AudioVolumeManager.BGMVolume;
            bgmSlider.onValueChanged.RemoveListener(SetBGMVolume);
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        }

        if (vfxSlider != null)
        {
            vfxSlider.value = AudioVolumeManager.VFXVolume;
            vfxSlider.onValueChanged.RemoveListener(SetVFXVolume);
            vfxSlider.onValueChanged.AddListener(SetVFXVolume);
        }

        // 4. Hide menu on startup so player drops straight into the game
        ShowMenu(false);
    }

    private void Update()
    {
        // Keyboard fallback for toggling menu in Editor (Esc or Tab keys)
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleMenu();
        }

        // Detect X Button (Primary Button on the Left Hand Controller)
        CheckLeftControllerXButton();
    }

    /// <summary>
    /// Reads left hand VR controller input statically using standard Unity XR InputDevices.
    /// </summary>
    private void CheckLeftControllerXButton()
    {
        InputDevice leftHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHandDevice.isValid)
        {
            if (leftHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool isXPressed))
            {
                // Detect rising edge (button down event)
                if (isXPressed && !wasXButtonPressedLastFrame)
                {
                    ToggleMenu();
                }
                wasXButtonPressedLastFrame = isXPressed;
            }
        }
    }

    /// <summary>
    /// Toggles the menu open/closed.
    /// </summary>
    public void ToggleMenu()
    {
        ShowMenu(!isMenuOpen);
    }

    /// <summary>
    /// Opens or closes the menu, updates timescales, and positions the canvas in VR space.
    /// </summary>
    public void ShowMenu(bool show)
    {
        isMenuOpen = show;
        if (menuPanel != null)
        {
            menuPanel.SetActive(show);
        }

        // Toggle hand laser pointers so players can click buttons
        if (leftHandPointer != null) leftHandPointer.SetActive(show);
        if (rightHandPointer != null) rightHandPointer.SetActive(show);

        // Pause time while menu is open, resume when closed
        Time.timeScale = show ? 0f : 1f;

        if (show)
        {
            PositionMenuInFrontOfPlayer();
        }
    }

    /// <summary>
    /// Places the menu canvas exactly 2 meters in front of the VR camera at eye level.
    /// </summary>
    private void PositionMenuInFrontOfPlayer()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 targetPos = cam.transform.position + cam.transform.forward * 2f;
            targetPos.y = cam.transform.position.y; // Keep it at eye level rather than looking down/up
            transform.position = targetPos;

            // Face the player camera
            transform.LookAt(new Vector3(cam.transform.position.x, transform.position.y, cam.transform.position.z));
            transform.Rotate(0f, 180f, 0f); // Face towards the camera
        }
    }

    /// <summary>
    /// Programmatically overrides default UI sprites if custom graphics are assigned in the inspector slots.
    /// </summary>
    private void ApplyCustomGraphics()
    {
        // 1. Background Panel Customization
        if (customBackground != null && menuPanel != null)
        {
            Image panelImage = menuPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.sprite = customBackground;
                panelImage.color = Color.white; // Reset to white so custom texture colors render cleanly
            }
        }

        // 2. Button Customization
        if (customButtonGraphic != null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                Image btnImage = btn.GetComponent<Image>();
                if (btnImage != null)
                {
                    btnImage.sprite = customButtonGraphic;
                    btnImage.color = Color.white;
                }
            }
        }

        // 3. Slider Handle Customization
        if (customSliderHandle != null)
        {
            Slider[] sliders = GetComponentsInChildren<Slider>(true);
            foreach (Slider slider in sliders)
            {
                if (slider.handleRect != null)
                {
                    Image handleImage = slider.handleRect.GetComponent<Image>();
                    if (handleImage != null)
                    {
                        handleImage.sprite = customSliderHandle;
                        handleImage.color = Color.white;
                    }
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // BUTTON ACTIONS
    // ─────────────────────────────────────────────────────────────────────

    public void StartGame()
    {
        ShowMenu(false);
        Debug.Log("[GameMenu] Menu closed. Game resumed.");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f; // Make sure time scale is reset before loading scene
        SceneManager.LoadScene(restartSceneName);
        Debug.Log($"[GameMenu] Loading start/demo scene: {restartSceneName}");
    }

    public void QuitGame()
    {
        Debug.Log("[GameMenu] Quitting Application...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─────────────────────────────────────────────────────────────────────
    // VOLUME SLIDER CHANGE EVENTS
    // ─────────────────────────────────────────────────────────────────────

    public void SetBGMVolume(float volume)
    {
        AudioVolumeManager.SetBGMVolume(volume);
    }

    public void SetVFXVolume(float volume)
    {
        AudioVolumeManager.SetVFXVolume(volume);
    }
}
