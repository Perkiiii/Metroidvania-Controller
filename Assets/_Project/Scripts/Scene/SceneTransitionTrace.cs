using System.Collections.Generic;
using UnityEngine;

public readonly struct SceneTransitionTraceEntry
{
    public readonly int Sequence;
    public readonly SceneTransitionTraceMarker Marker;
    public readonly float Realtime;
    public readonly GameState State;
    public readonly string Detail;

    public SceneTransitionTraceEntry(
        int sequence,
        SceneTransitionTraceMarker marker,
        float realtime,
        GameState state,
        string detail)
    {
        Sequence = sequence;
        Marker = marker;
        Realtime = realtime;
        State = state;
        Detail = detail ?? "";
    }
}

public sealed class SceneTransitionTrace
{
    private readonly List<SceneTransitionTraceEntry> entries = new List<SceneTransitionTraceEntry>();
    private readonly List<SceneTransitionTraceMarker> markers = new List<SceneTransitionTraceMarker>();

    public int TransitionId { get; }
    public string TargetScene { get; }
    public string DestinationPassageGuid { get; }
    public FadeProfile FadeOverride { get; }
    public SceneTransitionKind Kind { get; }
    public string SourceDescription { get; }
    public float StartedAtRealtime { get; }
    public float EndedAtRealtime { get; private set; }
    public bool IsComplete { get; private set; }
    public bool IsFailed { get; private set; }
    public bool IsClosed { get; private set; }
    public string FailureDetail { get; private set; }

    public IReadOnlyList<SceneTransitionTraceEntry> Entries => entries;
    public IReadOnlyList<SceneTransitionTraceMarker> Markers => markers;

    public SceneTransitionTrace(int transitionId, SceneTransitionRequest request)
    {
        TransitionId = transitionId;
        TargetScene = request.TargetScene ?? "";
        DestinationPassageGuid = request.DestinationPassageGuid ?? "";
        FadeOverride = request.FadeOverride;
        Kind = request.Kind;
        SourceDescription = request.SourceDescription ?? "";
        StartedAtRealtime = Time.realtimeSinceStartup;
        EndedAtRealtime = -1f;
        FailureDetail = "";
    }

    public SceneTransitionTraceEntry Add(SceneTransitionTraceMarker marker, GameState state, string detail = "")
    {
        SceneTransitionTraceEntry entry = new SceneTransitionTraceEntry(
            entries.Count,
            marker,
            Time.realtimeSinceStartup,
            state,
            detail);

        entries.Add(entry);
        markers.Add(marker);
        return entry;
    }

    public void MarkFailed(string detail = "")
    {
        IsFailed = true;
        FailureDetail = detail ?? "";
    }

    public void MarkComplete()
    {
        IsComplete = true;
    }

    public void Close()
    {
        if (IsClosed)
            return;

        EndedAtRealtime = Time.realtimeSinceStartup;
        IsClosed = true;
    }

    public bool Contains(SceneTransitionTraceMarker marker)
    {
        return IndexOf(marker) >= 0;
    }

    public int IndexOf(SceneTransitionTraceMarker marker)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Marker == marker)
                return i;
        }

        return -1;
    }

    public bool ContainsInOrder(params SceneTransitionTraceMarker[] expected)
    {
        if (expected == null || expected.Length == 0)
            return true;

        int expectedIndex = 0;
        for (int i = 0; i < entries.Count && expectedIndex < expected.Length; i++)
        {
            if (entries[i].Marker == expected[expectedIndex])
                expectedIndex++;
        }

        return expectedIndex == expected.Length;
    }
}
