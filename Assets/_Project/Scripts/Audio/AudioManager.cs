using System.Collections.Generic;
using UnityEngine;

public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    private readonly List<AudioSource> oneShotSfxSources = new List<AudioSource>();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveSources();
        DontDestroyOnLoad(gameObject);
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip);
    }

    public void PlaySFX(AudioClip clip, float pitchMin, float pitchMax, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource source = GetOneShotSfxSource();
        ConfigureSource(source, clip, pitchMin, pitchMax, volume, false);
        source.Play();
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip == null || musicSource == null)
        {
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    private AudioSource GetOneShotSfxSource()
    {
        for (int i = 0; i < oneShotSfxSources.Count; i++)
        {
            AudioSource source = oneShotSfxSources[i];
            if (source != null && !source.isPlaying)
            {
                return source;
            }
        }

        GameObject sourceObject = new GameObject("PooledSFX");
        if (sfxSource != null)
        {
            sourceObject.transform.SetParent(sfxSource.transform, false);
        }
        else
        {
            sourceObject.transform.SetParent(transform, false);
        }

        AudioSource newSource = sourceObject.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        CopySfxSourceSettings(newSource);
        oneShotSfxSources.Add(newSource);
        return newSource;
    }

    private void ResolveSources()
    {
        if (sfxSource == null)
        {
            sfxSource = ResolveChildSource("SFX");
        }

        if (musicSource == null)
        {
            musicSource = ResolveChildSource("Music");
        }
    }

    private AudioSource ResolveChildSource(string sourceName)
    {
        AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null && sources[i].gameObject.name == sourceName)
            {
                return sources[i];
            }
        }

        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }

    private void ConfigureSource(
        AudioSource source,
        AudioClip clip,
        float pitchMin,
        float pitchMax,
        float volume,
        bool loop)
    {
        CopySfxSourceSettings(source);
        source.clip = clip;
        source.loop = loop;
        source.pitch = Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax));
        source.volume *= Mathf.Clamp01(volume);
    }

    private void CopySfxSourceSettings(AudioSource target)
    {
        if (target == null || sfxSource == null)
        {
            return;
        }

        target.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
        target.volume = sfxSource.volume;
        target.pitch = sfxSource.pitch;
        target.spatialBlend = sfxSource.spatialBlend;
    }
}
