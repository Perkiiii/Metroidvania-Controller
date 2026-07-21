using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldGraphEditor
{
    public static class AwaitUtility
    {
        public static Task LoadSceneAsync(int buildIndex, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>();
            var operation = SceneManager.LoadSceneAsync(buildIndex)!;

            token.Register(() =>
            {
                operation.allowSceneActivation = false;
                tcs.TrySetCanceled(token);
            });

            operation.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }

        public static Task AwaitByCoroutine(TransitionDelayData delayData, MonoBehaviour runner, CancellationToken token)
        {
            if (delayData.DelayType == DelayType.ThisFrame)
                return Task.CompletedTask;
            
            var tcs = new TaskCompletionSource<bool>();

            token.Register(() => tcs.TrySetCanceled(), false);
            runner.StartCoroutine(WaitCoroutine(delayData, tcs, token));
            return tcs.Task;
        }
        
        public static Task AwaitByCoroutine(float delay, MonoBehaviour runner, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>();

            token.Register(() => tcs.TrySetCanceled(), false);
            runner.StartCoroutine(WaitCoroutine(delay, tcs, token));
            return tcs.Task;
        }

        private static IEnumerator WaitCoroutine(float delay, TaskCompletionSource<bool> tcs, CancellationToken token)
        {
            yield return new WaitForSeconds(delay);
            
            if (token.IsCancellationRequested)
            {
                tcs.TrySetCanceled();
                yield break;
            }

            tcs.TrySetResult(true);
        }

        private static IEnumerator WaitCoroutine(TransitionDelayData delayData, TaskCompletionSource<bool> tcs, CancellationToken token)
        {
            switch (delayData.DelayType)
            {
                case DelayType.NextFrame:
                    yield return new WaitForEndOfFrame();
                    break;
                case DelayType.PhysicsUpdate:
                    yield return new WaitForFixedUpdate();
                    break;
                case DelayType.CustomDelay:
                    yield return new WaitForSeconds(delayData.Delay);
                    break;
            }

            if (token.IsCancellationRequested)
            {
                tcs.TrySetCanceled();
                yield break;
            }

            tcs.TrySetResult(true);
        }
    }
}