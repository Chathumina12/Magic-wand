using UnityEngine;
using System;

/// <summary>
/// Central manager for audio volume levels. 
/// Saves and loads volume values from PlayerPrefs.
/// </summary>
public static class AudioVolumeManager
{
    private const string BGM_KEY = "BGM_Volume";
    private const string VFX_KEY = "VFX_Volume";

    public static float BGMVolume { get; private set; }
    public static float VFXVolume { get; private set; }

    public static event Action OnVolumeChanged;

    static AudioVolumeManager()
    {
        LoadVolumes();
    }

    public static void LoadVolumes()
    {
        BGMVolume = PlayerPrefs.GetFloat(BGM_KEY, 0.75f);
        VFXVolume = PlayerPrefs.GetFloat(VFX_KEY, 0.75f);
    }

    public static void SetBGMVolume(float volume)
    {
        BGMVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(BGM_KEY, BGMVolume);
        PlayerPrefs.Save();
        OnVolumeChanged?.Invoke();
    }

    public static void SetVFXVolume(float volume)
    {
        VFXVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VFX_KEY, VFXVolume);
        PlayerPrefs.Save();
        OnVolumeChanged?.Invoke();
    }
}
