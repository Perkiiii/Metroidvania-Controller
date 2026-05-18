using System;
using MoreMountains.Feedbacks;
using UnityEngine;

public enum CameraShakeCue
{
    SmallShake = 0,
    MediumShake = 1,
    FallRumble = 2,
    IntenseShake = 3,
    Stop = 4
}

public sealed class CameraShakeCueService : MonoBehaviour, ICameraShakeService
{
    [Serializable]
    private struct ShakeCueSettings
    {
        public CameraShakeCue cue;
        public float duration;
        public float amplitude;
        public float frequency;
        public bool infinite;
        public bool useUnscaledTime;
    }

    [Header("Optional MMF Players")]
    [SerializeField] private MMF_Player smallShake;
    [SerializeField] private MMF_Player mediumShake;
    [SerializeField] private MMF_Player intenseShake;

    [SerializeField]
    private ShakeCueSettings[] cues =
    {
        new ShakeCueSettings
        {
            cue = CameraShakeCue.SmallShake,
            duration = 0.13f,
            amplitude = 0.10f,
            frequency = 25f,
            useUnscaledTime = true
        },
        new ShakeCueSettings
        {
            cue = CameraShakeCue.MediumShake,
            duration = 0.30f,
            amplitude = 0.25f,
            frequency = 20f,
            useUnscaledTime = true
        },
        new ShakeCueSettings
        {
            cue = CameraShakeCue.IntenseShake,
            duration = 0.45f,
            amplitude = 0.4f,
            frequency = 18f,
            useUnscaledTime = true
        },
        new ShakeCueSettings
        {
            cue = CameraShakeCue.FallRumble,
            duration = 0.5f,
            amplitude = 0.08f,
            frequency = 18f,
            infinite = true,
            useUnscaledTime = true
        }
    };

    public void Play(CameraShakeCue cue)
    {
        if (cue == CameraShakeCue.Stop)
        {
            StopAll();
            return;
        }

        for (int i = 0; i < cues.Length; i++)
        {
            if (cues[i].cue == cue)
            {
                Trigger(cues[i], 1f);
                return;
            }
        }
    }

    public void Shake(CameraShakeProfile profile, Vector2 worldPosition, float intensityMultiplier = 1f)
    {
        if (profile == null)
        {
            Shake(CameraShakeIntensity.Small, worldPosition, intensityMultiplier);
            return;
        }

        Trigger(
            profile.Duration,
            profile.Amplitude * intensityMultiplier,
            profile.Frequency,
            profile.Infinite,
            profile.UseUnscaledTime);
    }

    public void Shake(CameraShakeIntensity preset, Vector2 worldPosition, float intensityMultiplier = 1f)
    {
        switch (preset)
        {
            case CameraShakeIntensity.Medium:
                PlayFeedbackOrCue(mediumShake, CameraShakeCue.MediumShake, intensityMultiplier);
                break;
            case CameraShakeIntensity.Intense:
                PlayFeedbackOrCue(intenseShake, CameraShakeCue.IntenseShake, intensityMultiplier);
                break;
            case CameraShakeIntensity.FallRumble:
                PlayFeedbackOrCue(null, CameraShakeCue.FallRumble, intensityMultiplier);
                break;
            default:
                PlayFeedbackOrCue(smallShake, CameraShakeCue.SmallShake, intensityMultiplier);
                break;
        }
    }

    public void Cancel(object source)
    {
        StopAll();
    }

    public void StopAll()
    {
        MMCameraShakeStopEvent.Trigger(null);
    }

    private void PlayFeedbackOrCue(MMF_Player player, CameraShakeCue cue, float intensityMultiplier)
    {
        if (player != null)
        {
            player.PlayFeedbacks();
            return;
        }

        for (int i = 0; i < cues.Length; i++)
        {
            if (cues[i].cue == cue)
            {
                Trigger(cues[i], intensityMultiplier);
                return;
            }
        }
    }

    private static void Trigger(ShakeCueSettings settings, float intensityMultiplier)
    {
        Trigger(
            settings.duration,
            settings.amplitude * intensityMultiplier,
            settings.frequency,
            settings.infinite,
            settings.useUnscaledTime);
    }

    private static void Trigger(float duration, float amplitude, float frequency, bool infinite, bool useUnscaledTime)
    {
        MMCameraShakeEvent.Trigger(
            duration,
            amplitude,
            frequency,
            0f,
            0f,
            0f,
            infinite,
            null,
            useUnscaledTime);
    }
}
