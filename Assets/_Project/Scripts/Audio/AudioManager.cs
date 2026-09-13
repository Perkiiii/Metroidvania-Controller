using System.Collections.Generic;
using UnityEngine;

public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource ambienceSource;

    private readonly List<AudioSource> oneShotSfxSources = new List<AudioSource>();
    private UnityEngine.Object ambienceOwner;
    private float ambienceTargetVolume;
    private float ambienceFadeSeconds;
    private bool ambiencePlaybackActive;
    private bool ambienceStopPending;

    /// <summary>Clip currently assigned to the dedicated ambience channel.</summary>
    public AudioClip CurrentAmbienceClip => ambienceSource != null ? ambienceSource.clip : null;

    /// <summary>Target volume for the current ambience fade.</summary>
    public float AmbienceTargetVolume => ambienceTargetVolume;

    /// <summary>True while ambience is requested, including a pending fade-out.</summary>
    public bool IsAmbienceActive => ambiencePlaybackActive;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveSources();
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Update()
    {
        UpdateAmbienceFade(Time.unscaledDeltaTime);
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        ClearAmbienceChannel();
        Instance = null;
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
        ConfigureOneShotSfxSource(source, clip, pitchMin, pitchMax, volume);
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

    /// <summary>
    /// Starts or updates the one global ambience channel. Repeating the same request does not
    /// restart the clip, which keeps weather transitions from allocating or retriggering audio.
    /// </summary>
    public void StartAmbience(
        UnityEngine.Object owner,
        AudioClip clip,
        float targetVolume = 1f,
        float fadeSeconds = 0.25f)
    {
        if (owner == null || clip == null || ambienceSource == null)
        {
            return;
        }

        bool sameClip = ambienceSource.clip == clip;
        bool wasActive = ambiencePlaybackActive;
        if (!sameClip)
        {
            ambienceSource.Stop();
            ambienceSource.clip = clip;
            ambienceSource.volume = 0f;
            wasActive = false;
        }

        // Ownership changes with every accepted start, even when the clip stays the same. The
        // active channel is reused so a room handoff does not restart an identical ambience.
        ambienceOwner = owner;
        ambienceSource.playOnAwake = false;
        ambienceSource.loop = true;
        ambienceTargetVolume = Mathf.Clamp01(targetVolume);
        ambienceFadeSeconds = Mathf.Max(0f, fadeSeconds);
        ambienceStopPending = false;
        ambiencePlaybackActive = true;

        if (!wasActive)
        {
            ambienceSource.Play();
        }

        if (ambienceFadeSeconds <= 0f)
        {
            ambienceSource.volume = ambienceTargetVolume;
        }
    }

    /// <summary>Adjusts the active ambience target without replacing its clip.</summary>
    public void UpdateAmbience(
        UnityEngine.Object owner,
        float targetVolume,
        float fadeSeconds = 0.25f)
    {
        if (!OwnsAmbience(owner) || ambienceSource == null || ambienceSource.clip == null)
        {
            return;
        }

        ambienceTargetVolume = Mathf.Clamp01(targetVolume);
        ambienceFadeSeconds = Mathf.Max(0f, fadeSeconds);
        ambienceStopPending = ambienceTargetVolume <= 0f;
        ambiencePlaybackActive = true;

        if (ambienceFadeSeconds <= 0f)
        {
            UpdateAmbienceFade(0f);
        }
    }

    /// <summary>
    /// Fades the dedicated ambience source to silence and stops it. No coroutine is retained, so
    /// scene unload and repeated clear/rain cycles cannot leave a runner behind.
    /// </summary>
    public void StopAmbience(UnityEngine.Object owner, float fadeSeconds = 0.25f)
    {
        if (!OwnsAmbience(owner))
        {
            return;
        }

        if (ambienceSource == null)
        {
            ClearAmbienceState();
            return;
        }

        ambienceTargetVolume = 0f;
        ambienceFadeSeconds = Mathf.Max(0f, fadeSeconds);
        ambienceStopPending = true;
        ambiencePlaybackActive = ambienceSource.clip != null;

        if (ambienceFadeSeconds <= 0f)
        {
            UpdateAmbienceFade(0f);
        }
    }

    private bool OwnsAmbience(UnityEngine.Object owner)
    {
        // Reference identity remains valid during Unity's destruction callbacks, while a stale
        // presenter still fails once another owner has taken the channel.
        return !ReferenceEquals(owner, null) && ReferenceEquals(ambienceOwner, owner);
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

        if (ambienceSource == null)
        {
            ambienceSource = ResolveChildSource("Ambience");
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

    private void ConfigureOneShotSfxSource(
        AudioSource source,
        AudioClip clip,
        float pitchMin,
        float pitchMax,
        float volume)
    {
        if (source == null)
        {
            return;
        }

        float baseVolume = sfxSource != null ? sfxSource.volume : 1f;

        source.playOnAwake = false;
        source.outputAudioMixerGroup = sfxSource != null ? sfxSource.outputAudioMixerGroup : null;
        source.spatialBlend = sfxSource != null ? sfxSource.spatialBlend : 0f;
        source.clip = clip;
        source.loop = false;
        source.pitch = Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax));
        source.volume = baseVolume * Mathf.Clamp01(volume);
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

    private void UpdateAmbienceFade(float deltaTime)
    {
        if (ambienceSource == null || ambienceSource.clip == null)
        {
            if (ambienceStopPending)
            {
                ClearAmbienceState();
            }

            return;
        }

        if (ambienceFadeSeconds <= 0f)
        {
            ambienceSource.volume = ambienceTargetVolume;
        }
        else
        {
            float step = Mathf.Max(0f, deltaTime) / ambienceFadeSeconds;
            ambienceSource.volume = Mathf.MoveTowards(
                ambienceSource.volume,
                ambienceTargetVolume,
                step);
        }

        if (ambienceStopPending && ambienceSource.volume <= 0.0001f)
        {
            ambienceSource.Stop();
            ambienceSource.clip = null;
            ambienceSource.volume = 0f;
            ClearAmbienceState();
        }
    }

    private void ClearAmbienceChannel()
    {
        if (ambienceSource != null)
        {
            ambienceSource.Stop();
            ambienceSource.clip = null;
            ambienceSource.volume = 0f;
        }

        ClearAmbienceState();
    }

    private void ClearAmbienceState()
    {
        ambienceOwner = null;
        ambiencePlaybackActive = false;
        ambienceStopPending = false;
        ambienceTargetVolume = 0f;
        ambienceFadeSeconds = 0f;
    }
}
