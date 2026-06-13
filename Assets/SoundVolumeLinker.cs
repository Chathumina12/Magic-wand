using UnityEngine;

/// <summary>
/// Attach this component to any GameObject with an AudioSource to automatically
/// link and scale its volume based on the central AudioVolumeManager settings.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SoundVolumeLinker : MonoBehaviour
{
    public enum SoundType
    {
        BGM,
        VFX
    }

    [Header("Volume Link Settings")]
    [Tooltip("Is this audio source for background music (BGM) or sound effects (VFX)?")]
    public SoundType soundType = SoundType.VFX;

    [Tooltip("The max volume of this audio source when the slider is set to 100%.")]
    [Range(0f, 1f)]
    public float maxLocalVolume = 1f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        // Subscribe to volume change events
        AudioVolumeManager.OnVolumeChanged += UpdateVolume;
        UpdateVolume();
    }

    private void OnDisable()
    {
        // Unsubscribe
        AudioVolumeManager.OnVolumeChanged -= UpdateVolume;
    }

    private void Start()
    {
        UpdateVolume();
    }

    /// <summary>
    /// Updates the AudioSource volume based on the linked SoundType.
    /// </summary>
    public void UpdateVolume()
    {
        if (audioSource == null) return;

        float targetVolume = (soundType == SoundType.BGM) 
            ? AudioVolumeManager.BGMVolume 
            : AudioVolumeManager.VFXVolume;

        audioSource.volume = targetVolume * maxLocalVolume;
    }
}
