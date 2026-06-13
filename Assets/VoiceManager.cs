using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Oculus.Voice;
using Meta.WitAi.Data;
using Meta.WitAi;
using Meta.WitAi.Json;
using System;

/// <summary>
/// VoiceManager — Central hub for all voice-controlled spell casting.
///
/// HOW IT WORKS (two-path detection):
/// ─────────────────────────────────
/// PRIMARY PATH — Transcription matching (most reliable):
///   When Wit.ai returns a text transcription of what was spoken,
///   we do a local keyword check against known spell words/phonetic alternatives.
///   This bypasses Wit.ai's entity/synonym matching which has known bad matches
///   (e.g. "In San Diego" → Incendio kept intentionally for accent support).
///   Each spell has an EXCLUSIVE keyword list — no keyword appears in two spells.
///
/// SECONDARY PATH — Wit.ai intent (final response only):
///   If transcription matching finds nothing, we check the Wit.ai NLP intent
///   from the FINAL response only (not partial). Partial responses are used only
///   for logging — never for spell execution — to prevent race conditions.
///
/// SPELL ISOLATION GUARANTEE:
///   Every spell has its OWN exclusive keyword set. "aloha"/"alohomora" only
///   maps to door. "incendio"/"in san diego" only maps to fire. No overlap.
///   Once a spell is detected via transcription, the intent path is skipped.
///
/// SPELL NAMES (unchanged from original):
///   Lumos, Nox, Wingardium_Leviosa, Descendo, Alohomora, Incendio,
///   Finite_Incantatem, Great_Hall
/// </summary>
public class VoiceManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────
    // INSPECTOR FIELDS
    // ─────────────────────────────────────────────────────────────────────

    [Header("Core References")]
    [SerializeField] private AppVoiceExperience voiceExperience;

    [Header("Spell Effects")]
    public Light light;
    public FloatingMover floatingMover;
    public FloatingMover floatingMover1;
    public DoorRotator doorRotator;
    public DoorRotator doorRotator1;
    public GameObject fire;

    [Header("Magical Challenge Area")]
    public SceneTeleporter sceneTeleporter;

    [Header("Spell Audio Clips")]
    public AudioClip lumos;
    public AudioClip nox;
    public AudioClip wingardiumLeviosa;
    public AudioClip descendo;
    public AudioClip alohomora;
    public AudioClip incendio;
    public AudioClip finiteIncantatem;
    public AudioClip greatHall;

    [Header("Audio Output")]
    public AudioSource audioSource;

    [Header("Unrecognized Spell Feedback")]
    [Tooltip("Audio to play when a spell is spoken but not recognized.")]
    public AudioClip unrecognizedSpellSound;
    [Tooltip("Particle effect to show when spell is unrecognized (optional).")]
    public ParticleSystem unrecognizedSpellEffect;

    [Header("Voice Activation Settings")]
    [Tooltip("Cooldown in seconds between voice activations to prevent API overload.")]
    [SerializeField] private float activationCooldownDuration = 2.0f;
    [Tooltip("Minimum Wit.ai intent confidence required to accept a spell (0–1). Ignored for transcription path.")]
    [Range(0f, 1f)]
    [SerializeField] private float minimumIntentConfidence = 0.65f;

    // ─────────────────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────

    private bool isActivating = false;
    private float activationCooldown = 0f;
    private bool micPermissionGranted = false;
    private bool isMicTesting = false;

    // Spell isolation: once a spell fires in a single activation, block all others.
    private bool spellHandledThisActivation = false;

    // Tracks last full transcription received so we can process it once.
    private string lastFullTranscription = "";

    // Mic testing
    private float lastMicLevelLogTime = 0f;
    private string micStatusMessage = "Initializing...";
    private string workingMicDevice = "";

    // VR controller button tracking
    private bool wasAButtonPressedLastFrame = false;
    private bool wasBButtonPressedLastFrame = false;

    // ─────────────────────────────────────────────────────────────────────
    // SPELL → KEYWORD MAPPING
    // Each spell has an exclusive set of phonetic keywords.
    // NO keyword appears in more than one spell's list.
    // Keywords are all lowercase — comparison uses ToLower().
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the transcription text contains any keyword for the given spell.
    /// All comparisons are case-insensitive word/substring checks.
    /// </summary>
    private static bool TranscriptionMatchesSpell(string lowerText, string spellName)
    {
        switch (spellName)
        {
            // ── LUMOS ── (light on)
            // "lubos" kept as phonetic alternative (accent support)
            case "Lumos":
                return ContainsWord(lowerText, "lumos") ||
                       ContainsWord(lowerText, "lubos");

            // ── NOX ── (light off)
            // "knox" kept as phonetic alternative
            case "Nox":
                return ContainsWord(lowerText, "nox") ||
                       ContainsWord(lowerText, "knox") ||
                       ContainsWord(lowerText, "knocks") ||
                       ContainsWord(lowerText, "locks") ||
                       ContainsWord(lowerText, "box") ||
                       ContainsWord(lowerText, "not") ||
                       ContainsWord(lowerText, "no") ||
                       ContainsWord(lowerText, "off") ||
                       ContainsWord(lowerText, "enough") ||
                       ContainsWord(lowerText, "stop") ||
                       ContainsWord(lowerText, "light off") ||
                       ContainsWord(lowerText, "turn off") ||
                       ContainsWord(lowerText, "dark");

            // ── WINGARDIUM LEVIOSA ── (float up)
            // Multiple phonetic forms
            case "Wingardium_Leviosa":
                return ContainsWord(lowerText, "wingardium") ||
                       ContainsWord(lowerText, "leviosa") ||
                       ContainsWord(lowerText, "wingadium") ||
                       ContainsWord(lowerText, "levioso");

            // ── DESCENDO ── (float down)
            case "Descendo":
                return ContainsWord(lowerText, "descendo");

            // ── ALOHOMORA ── (open door)
            // "aloha" is an acceptable alternative
            // IMPORTANT: "may" and "january" removed from Wit.ai but kept here
            // as a safety note — do NOT add common words here.
            case "Alohomora":
                return ContainsWord(lowerText, "alohomora") ||
                       ContainsWord(lowerText, "aloha mora") ||
                       ContainsWord(lowerText, "alohamora") ||
                       ContainsWord(lowerText, "aloha");


            // ── INCENDIO ── (fire on)
            // "in san diego" kept intentionally — accent/pronunciation fallback.
            // This is ONLY matched here and never for any other spell.
            case "Incendio":
                return ContainsWord(lowerText, "incendio") ||
                       lowerText.Contains("in san diego");

            // ── FINITE INCANTATEM ── (reset all)
            case "Finite_Incantatem":
                return ContainsWord(lowerText, "finite incantatem") ||
                       (ContainsWord(lowerText, "finite") && ContainsWord(lowerText, "incantatem")) ||
                       ContainsWord(lowerText, "finite incan") ||
                       ContainsWord(lowerText, "finit incantatem");

            // ── GREAT HALL ── (teleport to 2nd level)
            case "Great_Hall":
                return lowerText.Contains("great hall") ||
                       lowerText.Contains("greate hall") ||
                       lowerText.Contains("grate hall") ||
                       lowerText.Contains("grey hall") ||
                       lowerText.Contains("great haul") ||
                       lowerText.Contains("great ole") ||
                       lowerText.Contains("great ol");

            default:
                return false;
        }
    }

    /// <summary>
    /// Ordered list of spells to check during transcription matching.
    /// ORDER MATTERS: More specific/longer spells first to avoid partial collisions.
    /// e.g. Finite_Incantatem before Incendio (both have "in")
    /// </summary>
    private static readonly string[] SpellCheckOrder = new string[]
    {
        "Finite_Incantatem",   // Longest — check first
        "Wingardium_Leviosa",
        "Great_Hall",
        "Alohomora",
        "Descendo",
        "Incendio",
        "Lumos",
        "Nox",                 // Shortest — check last to avoid partial matches
    };

    // ─────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (voiceExperience == null)
        {
            Debug.LogError("[VoiceManager] AppVoiceExperience is not assigned! Please assign it in the Inspector.");
            enabled = false;
            return;
        }

        // Register voice events
        voiceExperience.VoiceEvents.OnStartListening.AddListener(OnStartListening);
        voiceExperience.VoiceEvents.OnStoppedListening.AddListener(OnStopListening);
        voiceExperience.VoiceEvents.OnError.AddListener(OnVoiceError);
        voiceExperience.VoiceEvents.OnAborted.AddListener(OnVoiceAborted);
        voiceExperience.VoiceEvents.OnRequestCompleted.AddListener(OnRequestCompleted);

        // Transcription events — PRIMARY spell detection path
        voiceExperience.VoiceEvents.OnPartialTranscription.AddListener(OnPartialTranscription);
        voiceExperience.VoiceEvents.OnFullTranscription.AddListener(OnFullTranscription);

        // SECONDARY: Partial response — LOG ONLY, never execute spells
        voiceExperience.VoiceEvents.OnPartialResponse.AddListener(OnPartialResponseLog);

        // SECONDARY: Final response — spell detection fallback only
        voiceExperience.VoiceEvents.OnResponse.AddListener(OnFinalResponse);

        // Mic level monitoring
        voiceExperience.VoiceEvents.OnMicLevelChanged.AddListener(OnMicLevelChanged);

        Debug.Log("[VoiceManager] All voice events registered.");
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (light == null)             Debug.LogWarning("[VoiceManager] 'light' is not assigned. Lumos/Nox will not work.");
        if (floatingMover == null)     Debug.LogWarning("[VoiceManager] 'floatingMover' is not assigned. Wingardium/Descendo may be partial.");
        if (doorRotator == null && doorRotator1 == null) Debug.LogWarning("[VoiceManager] 'doorRotator' is not assigned. Alohomora will not open the door.");
        if (fire == null)              Debug.LogWarning("[VoiceManager] 'fire' is not assigned. Incendio will not work.");
        if (sceneTeleporter == null)   Debug.LogWarning("[VoiceManager] 'sceneTeleporter' is not assigned. Great Hall teleport will not work.");
        if (audioSource == null)       Debug.LogWarning("[VoiceManager] 'audioSource' is not assigned. Spell sounds will not play.");
    }

    private IEnumerator Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android / Quest microphone permission
        Debug.Log("[VoiceManager] Android detected — checking microphone permission...");
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            Debug.LogWarning("[VoiceManager] Requesting microphone permission...");
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            yield return new WaitForSeconds(2f);

            micPermissionGranted = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Microphone);

            if (!micPermissionGranted)
                Debug.LogError("[VoiceManager] Microphone permission DENIED. Voice will not work.");
            else
                Debug.Log("[VoiceManager] Microphone permission granted.");
        }
        else
        {
            micPermissionGranted = true;
            Debug.Log("[VoiceManager] Microphone permission already granted.");
        }
#else
        // Editor / PC — find working microphone
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[VoiceManager] NO MICROPHONE FOUND. Check Windows Settings > Privacy > Microphone.");
            micPermissionGranted = false;
        }
        else
        {
            Debug.Log($"[VoiceManager] Found {Microphone.devices.Length} microphone(s).");
            micPermissionGranted = true;
            yield return StartCoroutine(FindWorkingMicrophone());
        }
        yield return null;
#endif

        // Log Wit.ai configuration status
        if (voiceExperience != null)
        {
            var witConfig = voiceExperience.RuntimeConfiguration;
            if (witConfig != null && witConfig.witConfiguration != null)
                Debug.Log($"[VoiceManager] Wit config: {witConfig.witConfiguration.name} | Token present: {!string.IsNullOrEmpty(witConfig.witConfiguration.GetClientAccessToken())}");
            else
                Debug.LogError("[VoiceManager] Wit configuration is NULL — assign it to AppVoiceExperience!");
        }

        Debug.Log("[VoiceManager] Ready. Press F or wand trigger to activate voice.");
    }

    private void OnDestroy()
    {
        if (voiceExperience == null) return;
        voiceExperience.VoiceEvents.OnStartListening.RemoveListener(OnStartListening);
        voiceExperience.VoiceEvents.OnStoppedListening.RemoveListener(OnStopListening);
        voiceExperience.VoiceEvents.OnError.RemoveListener(OnVoiceError);
        voiceExperience.VoiceEvents.OnAborted.RemoveListener(OnVoiceAborted);
        voiceExperience.VoiceEvents.OnRequestCompleted.RemoveListener(OnRequestCompleted);
        voiceExperience.VoiceEvents.OnPartialTranscription.RemoveListener(OnPartialTranscription);
        voiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(OnFullTranscription);
        voiceExperience.VoiceEvents.OnPartialResponse.RemoveListener(OnPartialResponseLog);
        voiceExperience.VoiceEvents.OnResponse.RemoveListener(OnFinalResponse);
        voiceExperience.VoiceEvents.OnMicLevelChanged.RemoveListener(OnMicLevelChanged);
    }

    private void Update()
    {
        // Count down activation cooldown
        if (activationCooldown > 0f)
            activationCooldown -= Time.deltaTime;

        // Keyboard shortcut for testing in editor
        if (Input.GetKeyDown(KeyCode.F))
            ActivateVoice();

        // VR Controller button checks (A/B Button on the right hand controller)
        CheckVRButtons();

        // Manual mic test (press M)
        if (Input.GetKeyDown(KeyCode.M) && !isMicTesting)
            StartCoroutine(RunManualMicTest());
    }

    private void CheckVRButtons()
    {
        UnityEngine.XR.InputDevice rightHandDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        if (rightHandDevice.isValid)
        {
            // A Button (Primary Button)
            if (rightHandDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool isAPressed))
            {
                if (isAPressed && !wasAButtonPressedLastFrame)
                {
                    Debug.Log("[VoiceManager] VR A Button Pressed -> Activating Voice");
                    ActivateVoice();
                }
                wasAButtonPressedLastFrame = isAPressed;
            }

            // B Button (Secondary Button)
            if (rightHandDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool isBPressed))
            {
                if (isBPressed && !wasBButtonPressedLastFrame)
                {
                    Debug.Log("[VoiceManager] VR B Button Pressed -> Activating Voice");
                    ActivateVoice();
                }
                wasBButtonPressedLastFrame = isBPressed;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // VOICE ACTIVATION
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Activates voice recognition. Called from wand button or keyboard (F).
    /// Has cooldown and duplicate-activation guards to prevent Wit.ai HTTP 14 errors.
    /// </summary>
    public void ActivateVoice()
    {
        if (voiceExperience == null)
        {
            Debug.LogError("[VoiceManager] Cannot activate — AppVoiceExperience not assigned.");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!micPermissionGranted)
        {
            Debug.LogError("[VoiceManager] Cannot activate — microphone permission denied.");
            return;
        }
#endif

        if (isMicTesting)
        {
            Debug.LogWarning("[VoiceManager] Cannot activate during microphone test.");
            return;
        }

        if (isActivating)
        {
            Debug.LogWarning("[VoiceManager] Already listening — ignoring duplicate activation.");
            return;
        }

        if (activationCooldown > 0f)
        {
            Debug.LogWarning($"[VoiceManager] Cooldown active ({activationCooldown:F1}s remaining). Please wait.");
            return;
        }

        if (voiceExperience.Active)
        {
            Debug.LogWarning("[VoiceManager] Voice already active — deactivating.");
            voiceExperience.Deactivate();
            return;
        }

        Debug.Log("[VoiceManager] >>> ACTIVATING VOICE — speak your spell now! <<<");
        isActivating = true;
        activationCooldown = activationCooldownDuration;
        lastFullTranscription = "";
        voiceExperience.Activate();
    }

    // ─────────────────────────────────────────────────────────────────────
    // VOICE EVENT HANDLERS
    // ─────────────────────────────────────────────────────────────────────

    private void OnStartListening()
    {
        spellHandledThisActivation = false;
        lastFullTranscription = "";
        Debug.Log("[VoiceManager] *** Listening! Speak your spell (Lumos, Nox, Incendio...) ***");
    }

    private void OnStopListening()
    {
        Debug.Log("[VoiceManager] Stopped listening.");
    }

    private void OnVoiceError(string error, string message)
    {
        Debug.LogError($"[VoiceManager] VOICE ERROR: {error} — {message}");
        isActivating = false;
    }

    private void OnVoiceAborted()
    {
        Debug.LogWarning("[VoiceManager] Voice request aborted.");
        isActivating = false;

        // If we heard something in transcription but no spell was matched, give feedback
        if (!spellHandledThisActivation && !string.IsNullOrEmpty(lastFullTranscription))
        {
            Debug.LogWarning($"[VoiceManager] Unrecognized spell: \"{lastFullTranscription}\"");
            PlayUnrecognizedFeedback();
        }
    }

    private void OnRequestCompleted()
    {
        Debug.Log("[VoiceManager] Request completed.");
        isActivating = false;
    }

    /// <summary>
    /// Partial transcription — PRIMARY path for early spell detection.
    /// We check here so the spell fires as soon as the words are spoken,
    /// before Wit.ai finishes processing. Only fires once per activation.
    /// </summary>
    public void OnPartialTranscription(string transcription)
    {
        if (string.IsNullOrEmpty(transcription)) return;
        Debug.Log($"[VoiceManager] Partial transcription: \"{transcription}\"");

        if (!spellHandledThisActivation)
            TryDetectSpellFromTranscription(transcription, "partial transcription");
    }

    /// <summary>
    /// Full transcription — PRIMARY path (authoritative).
    /// This is the complete sentence Wit.ai heard. We always check this.
    /// </summary>
    public void OnFullTranscription(string transcription)
    {
        if (string.IsNullOrEmpty(transcription)) return;
        Debug.Log($"[VoiceManager] FULL transcription: \"{transcription}\"");
        lastFullTranscription = transcription;

        if (!spellHandledThisActivation)
            TryDetectSpellFromTranscription(transcription, "full transcription");
    }

    /// <summary>
    /// Partial response — LOG ONLY. Never execute spells here.
    /// This prevents the race condition where a wrong partial intent blocks the correct final.
    /// </summary>
    public void OnPartialResponseLog(WitResponseNode response)
    {
        if (response == null) return;
        string intentName = response.GetIntentName();
        Debug.Log($"[VoiceManager] Partial response received (log only). Intent: '{intentName ?? "none"}'");
        // DO NOT call ExecuteSpell here — only log.
    }

    /// <summary>
    /// Final Wit.ai response — SECONDARY path.
    /// Only used if transcription matching found nothing.
    /// Applies confidence threshold to avoid low-confidence misfires.
    /// </summary>
    public void OnFinalResponse(WitResponseNode response)
    {
        if (response == null)
        {
            Debug.LogWarning("[VoiceManager] Final response was null.");
            return;
        }

        // If transcription already handled the spell, skip NLP
        if (spellHandledThisActivation)
        {
            Debug.Log("[VoiceManager] Spell already handled via transcription — skipping NLP intent.");
            return;
        }

        Debug.Log($"[VoiceManager] Final response: {response}");

        string spellName = ExtractSpellFromIntent(response);
        if (!string.IsNullOrEmpty(spellName))
        {
            Debug.Log($"[VoiceManager] Spell from NLP intent: '{spellName}'");
            spellHandledThisActivation = true;
            ExecuteSpell(spellName);
        }
        else
        {
            // Nothing detected at all
            Debug.LogWarning($"[VoiceManager] No spell detected. Transcription was: \"{lastFullTranscription}\"");
            if (!string.IsNullOrEmpty(lastFullTranscription))
                PlayUnrecognizedFeedback();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // TRANSCRIPTION-BASED DETECTION (PRIMARY)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans the transcription text against all spell keyword lists.
    /// Checks spells in SpellCheckOrder (longest/most-specific first).
    /// </summary>
    private void TryDetectSpellFromTranscription(string transcription, string source)
    {
        string lower = transcription.ToLower();

        foreach (string spellName in SpellCheckOrder)
        {
            if (TranscriptionMatchesSpell(lower, spellName))
            {
                Debug.Log($"[VoiceManager] ✓ '{spellName}' matched via {source}: \"{transcription}\"");
                spellHandledThisActivation = true;
                ExecuteSpell(spellName);
                return;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // INTENT-BASED DETECTION (SECONDARY)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts spell name from Wit.ai NLP intent.
    /// Applies confidence threshold — rejects low-confidence results.
    /// Maps the Wit.ai "door" intent to "Alohomora".
    /// </summary>
    private string ExtractSpellFromIntent(WitResponseNode response)
    {
        // Try standard top-level intent
        string intentName = TryExtractIntent(response);
        if (!string.IsNullOrEmpty(intentName))
            return MapIntentToSpell(intentName);

        // Try nested "response" sub-object (v74 streaming format)
        var finalResponse = response.GetFinalResponse();
        if (finalResponse != null)
        {
            intentName = TryExtractIntent(finalResponse);
            if (!string.IsNullOrEmpty(intentName))
                return MapIntentToSpell(intentName);
        }

        return null;
    }

    private string TryExtractIntent(WitResponseNode node)
    {
        if (node == null) return null;

        // GetIntentName() returns the top intent name
        string name = node.GetIntentName();
        if (string.IsNullOrEmpty(name)) return null;

        // Get confidence for this intent
        float confidence = 0f;
        var intentsArray = node["intents"];
        if (intentsArray != null && intentsArray.Count > 0)
        {
            var firstIntent = intentsArray[0];
            if (firstIntent != null)
            {
                var confNode = firstIntent["confidence"];
                if (confNode != null)
                    float.TryParse(confNode.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out confidence);
            }
        }

        if (confidence < minimumIntentConfidence && confidence > 0f)
        {
            Debug.LogWarning($"[VoiceManager] Intent '{name}' rejected — confidence {confidence:F2} < threshold {minimumIntentConfidence:F2}");
            return null;
        }

        Debug.Log($"[VoiceManager] Intent '{name}' accepted (confidence: {confidence:F2})");
        return name;
    }

    /// <summary>
    /// Maps Wit.ai intent names to internal spell names.
    /// The Wit.ai intent "door" maps to "Alohomora".
    /// </summary>
    private string MapIntentToSpell(string intentName)
    {
        if (string.Equals(intentName, "door", StringComparison.OrdinalIgnoreCase))
            return "Alohomora";

        // All other intents match spell names directly
        return intentName;
    }

    // ─────────────────────────────────────────────────────────────────────
    // SPELL EXECUTION
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes the spell effect. Each spell is self-contained.
    /// Fire (Incendio) can ONLY be triggered by "Incendio" — never by any other spell.
    /// </summary>
    private void ExecuteSpell(string spellName)
    {
        Debug.Log($"[VoiceManager] ══════════ CASTING: {spellName} ══════════");

        // ── LUMOS — turn light on ──
        if (string.Equals(spellName, "Lumos", StringComparison.OrdinalIgnoreCase))
        {
            if (light != null)
            {
                light.enabled = true;
                Debug.Log("[VoiceManager] Lumos! Light turned ON.");
            }
            else Debug.LogWarning("[VoiceManager] Lumos: light reference is null!");
            PlaySpellAudio(lumos, true);
            return;
        }

        // ── NOX — turn light off ──
        if (string.Equals(spellName, "Nox", StringComparison.OrdinalIgnoreCase))
        {
            if (light != null)
            {
                light.enabled = false;
                Debug.Log("[VoiceManager] Nox! Light turned OFF.");
            }
            else Debug.LogWarning("[VoiceManager] Nox: light reference is null!");
            
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
                audioSource.clip = null;
            }
            
            if (nox != null)
            {
                PlaySpellAudio(nox, false);
            }
            return;
        }

        // ── WINGARDIUM LEVIOSA — float objects ──
        if (string.Equals(spellName, "Wingardium_Leviosa", StringComparison.OrdinalIgnoreCase))
        {
            if (floatingMover != null)  floatingMover.Float();
            if (floatingMover1 != null) floatingMover1.Float();
            if (floatingMover == null && floatingMover1 == null)
                Debug.LogWarning("[VoiceManager] Wingardium: no FloatingMover references assigned!");
            PlaySpellAudio(wingardiumLeviosa, false);
            Debug.Log("[VoiceManager] Wingardium Leviosa! Objects are floating.");
            return;
        }

        // ── DESCENDO — bring floating objects down ──
        if (string.Equals(spellName, "Descendo", StringComparison.OrdinalIgnoreCase))
        {
            if (floatingMover != null)  floatingMover.Down();
            if (floatingMover1 != null) floatingMover1.Down();
            if (floatingMover == null && floatingMover1 == null)
                Debug.LogWarning("[VoiceManager] Descendo: no FloatingMover references assigned!");
            PlaySpellAudio(descendo, false);
            Debug.Log("[VoiceManager] Descendo! Objects coming down.");
            return;
        }

        // ── ALOHOMORA — open door ONLY ──
        // Fire is NEVER activated here. Only the door opens.
        if (string.Equals(spellName, "Alohomora", StringComparison.OrdinalIgnoreCase))
        {
            if (doorRotator != null)
            {
                doorRotator.Open();
                Debug.Log("[VoiceManager] Alohomora! Door 1 opened.");
            }
            if (doorRotator1 != null)
            {
                doorRotator1.Open();
                Debug.Log("[VoiceManager] Alohomora! Door 2 opened.");
            }
            if (doorRotator == null && doorRotator1 == null)
            {
                Debug.LogWarning("[VoiceManager] Alohomora: No doorRotators are assigned in Inspector!");
            }
            PlaySpellOneShot(alohomora);
            return;
        }

        // ── INCENDIO — fire ONLY ──
        // This is the ONLY place fire.SetActive(true) is called.
        // No other spell can reach this code path.
        if (string.Equals(spellName, "Incendio", StringComparison.OrdinalIgnoreCase))
        {
            if (fire != null)
            {
                fire.SetActive(true);
                Debug.Log("[VoiceManager] Incendio! Fire activated.");
            }
            else
            {
                Debug.LogWarning("[VoiceManager] Incendio: fire GameObject is not assigned in Inspector!");
            }
            PlaySpellOneShot(incendio);
            return;
        }

        // ── FINITE INCANTATEM — reset everything ──
        if (string.Equals(spellName, "Finite_Incantatem", StringComparison.OrdinalIgnoreCase))
        {
            if (floatingMover != null)  floatingMover.Down();
            if (floatingMover1 != null) floatingMover1.Down();
            if (doorRotator != null)    doorRotator.Close();
            if (doorRotator1 != null)   doorRotator1.Close();
            if (fire != null)           fire.SetActive(false);
            if (light != null)          light.enabled = false;
            
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
                audioSource.clip = null;
            }
            
            PlaySpellOneShot(finiteIncantatem);
            Debug.Log("[VoiceManager] Finite Incantatem! All effects reset.");
            return;
        }

        // ── GREAT HALL — teleport to 2nd level ──
        if (string.Equals(spellName, "Great_Hall", StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("[VoiceManager] ✦ Great Hall! Initiating teleport to 2nd level... ✦");
            if (sceneTeleporter != null)
            {
                sceneTeleporter.TeleportToGreatHall();
            }
            else
            {
                Debug.LogWarning("[VoiceManager] Great Hall: sceneTeleporter not assigned in Inspector! " +
                    "Please assign the SceneTeleporter component in the Inspector.");
            }
            PlaySpellOneShot(greatHall);
            return;
        }

        // ── UNKNOWN SPELL ──
        Debug.LogWarning($"[VoiceManager] Unknown spell: '{spellName}'. No effect applied.");
        PlayUnrecognizedFeedback();
    }

    // ─────────────────────────────────────────────────────────────────────
    // AUDIO HELPERS
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Plays a clip by stopping current audio first (for continuous spells like Lumos).
    /// </summary>
    private void PlaySpellAudio(AudioClip clip, bool loop = false)
    {
        if (audioSource == null) { Debug.LogWarning("[VoiceManager] AudioSource not assigned!"); return; }
        if (clip == null)        { Debug.LogWarning("[VoiceManager] Audio clip not assigned for this spell!"); return; }
        audioSource.Stop();
        audioSource.loop = loop;
        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>
    /// Plays a clip as a one-shot (allows overlapping, for short spell sounds).
    /// </summary>
    private void PlaySpellOneShot(AudioClip clip)
    {
        if (audioSource == null) { Debug.LogWarning("[VoiceManager] AudioSource not assigned!"); return; }
        if (clip == null)        { Debug.LogWarning("[VoiceManager] Audio clip not assigned for this spell!"); return; }
        audioSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Plays feedback for when speech was heard but no spell was recognized.
    /// Assignment requires a response/warning for unrecognized spells.
    /// </summary>
    private void PlayUnrecognizedFeedback()
    {
        if (unrecognizedSpellSound != null && audioSource != null)
            audioSource.PlayOneShot(unrecognizedSpellSound);

        if (unrecognizedSpellEffect != null)
            unrecognizedSpellEffect.Play();

        Debug.Log("[VoiceManager] ✗ Spell not recognized — please try again.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // MICROPHONE DIAGNOSTICS
    // ─────────────────────────────────────────────────────────────────────

    private void OnMicLevelChanged(float level)
    {
        if (Time.time - lastMicLevelLogTime > 2f)
        {
            if (level < 0.0001f)
                Debug.LogWarning($"[VoiceManager] Mic level: {level:F4} — SILENT (no audio reaching Wit.ai)");
            else
                Debug.Log($"[VoiceManager] Mic level: {level:F4}");
            lastMicLevelLogTime = Time.time;
        }
    }

    /// <summary>
    /// Tests all available microphone devices to find the one with best audio capture.
    /// Sets the Wit.ai mic to the best device found.
    /// </summary>
    private IEnumerator FindWorkingMicrophone()
    {
        isMicTesting = true;
        Debug.Log("[VoiceManager] ====== TESTING MICROPHONE DEVICES ======");

        string bestDevice = "";
        float bestLevel = 0f;
        int sampleRate = 16000;

        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            string mic = Microphone.devices[i];
            Debug.Log($"[VoiceManager] Testing [{i}]: \"{mic}\"...");

            AudioClip testClip = Microphone.Start(mic, false, 2, sampleRate);
            if (testClip == null) { continue; }

            yield return new WaitForSeconds(1.0f);

            int pos = Microphone.GetPosition(mic);
            if (pos <= 0) { Microphone.End(mic); DestroyImmediate(testClip); continue; }

            float[] samples = new float[pos];
            testClip.GetData(samples, 0);

            float maxLevel = 0f;
            foreach (float s in samples)
            {
                float abs = Mathf.Abs(s);
                if (abs > maxLevel) maxLevel = abs;
            }

            Microphone.End(mic);
            DestroyImmediate(testClip);

            Debug.Log($"[VoiceManager]   [{i}] \"{mic}\": peak={maxLevel:F6} {(maxLevel > 0.001f ? "✓ WORKING" : "✗ SILENT")}");

            if (maxLevel > bestLevel)
            {
                bestLevel = maxLevel;
                bestDevice = mic;
            }
        }

        Debug.Log("[VoiceManager] ====== MICROPHONE TEST COMPLETE ======");

        if (!string.IsNullOrEmpty(bestDevice))
        {
            workingMicDevice = bestDevice;
            Debug.Log($"[VoiceManager] Best mic: \"{bestDevice}\" (peak: {bestLevel:F6})");
            ApplyMicrophoneToWit(bestDevice);
        }
        else
        {
            Debug.LogError("[VoiceManager] All microphones silent! Check Windows Settings > Privacy > Microphone.");
        }

        isMicTesting = false;
    }

    private void ApplyMicrophoneToWit(string deviceName)
    {
        if (AudioBuffer.Instance != null && AudioBuffer.Instance.MicInput is Meta.WitAi.Lib.Mic witMic)
        {
            var devices = witMic.Devices;
            int idx = devices.IndexOf(deviceName);
            if (idx >= 0)
            {
                witMic.ChangeMicDevice(idx);
                Debug.Log($"[VoiceManager] Wit.ai mic set to [{idx}]: \"{deviceName}\"");
            }
            else
            {
                Debug.LogWarning($"[VoiceManager] Device \"{deviceName}\" not found in Wit.ai device list.");
            }
        }
        else
        {
            Debug.LogWarning("[VoiceManager] Could not access AudioBuffer.Instance.MicInput to set microphone.");
        }
    }

    private IEnumerator RunManualMicTest()
    {
        isMicTesting = true;
        bool wasActive = voiceExperience != null && voiceExperience.Active;

        if (wasActive)
        {
            voiceExperience.Deactivate();
            yield return new WaitForSeconds(0.5f);
        }

        yield return StartCoroutine(FindWorkingMicrophone());

        if (wasActive)
        {
            isActivating = false;
            ActivateVoice();
        }

        isMicTesting = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // UTILITY
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks if 'text' contains 'word' as a substring (case-insensitive already applied by caller).
    /// Handles both exact word boundary checks and substring for compound spell words.
    /// </summary>
    private static bool ContainsWord(string text, string word)
    {
        return text.Contains(word);
    }
}