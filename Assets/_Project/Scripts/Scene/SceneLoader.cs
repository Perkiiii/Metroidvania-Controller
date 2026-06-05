using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneLoader
{
    public bool CanLoad(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    public IEnumerator LoadSingle(string sceneName)
    {
        yield return LoadSingle(sceneName, null);
    }

    public IEnumerator LoadSingle(string sceneName, Action<bool> completed)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        if (load == null)
        {
            Debug.LogError($"[SceneLoader] LoadSceneAsync returned null for scene '{sceneName}'.");
            completed?.Invoke(false);
            yield break;
        }

        while (!load.isDone)
            yield return null;

        completed?.Invoke(true);
    }
}
