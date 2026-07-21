using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
#if WGE_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

namespace WorldGraphEditor
{
    internal sealed class SceneLoader : IDisposable
    {
        private bool _disposed;

#if WGE_ADDRESSABLES
        private AsyncOperationHandle<SceneInstance> _currentAddressableHandle;
        private bool _hasCurrentHandle;
#endif

        public async Task LoadAsync(RuntimeTransitionData data, CancellationToken token)
        {
            ThrowIfDisposed();

#if WGE_ADDRESSABLES
            var address = data.GetTargetSceneAddress();
            if (!string.IsNullOrEmpty(address))
            {
                await LoadAddressableSceneAsync(address, token);
                return;
            }
#endif

            var buildIndex = data.GetTargetSceneBuildIndex();
            if (buildIndex < 0)
                throw new InvalidOperationException(
                    $"RuntimeTransitionData has no valid scene reference. BuildIndex={buildIndex}.");

#if WGE_ADDRESSABLES
            var hadPrev = _hasCurrentHandle;
            var prevHandle = _currentAddressableHandle;
#endif

            await AwaitUtility.LoadSceneAsync(buildIndex, token);

#if WGE_ADDRESSABLES
            if (hadPrev)
            {
                if (prevHandle.IsValid())
                    Addressables.Release(prevHandle);

                _hasCurrentHandle = false;
                _currentAddressableHandle = default;
            }
#endif
        }

#if WGE_ADDRESSABLES
        private Task LoadAddressableSceneAsync(string address, CancellationToken token)
        {
            var hadPrev = _hasCurrentHandle;
            var prevHandle = _currentAddressableHandle;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handle = Addressables.LoadSceneAsync(address, LoadSceneMode.Single);

            var registration = token.Register(() =>
            {
                if (handle.IsValid() && !handle.IsDone)
                    Addressables.Release(handle);

                tcs.TrySetCanceled(token);
            });

            handle.Completed += op =>
            {
                registration.Dispose();

                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    if (hadPrev && prevHandle.IsValid())
                        Addressables.Release(prevHandle);

                    _currentAddressableHandle = handle;
                    _hasCurrentHandle = true;
                    tcs.TrySetResult(true);
                    return;
                }
                
                if (handle.IsValid())
                    Addressables.Release(handle);

                tcs.TrySetException(op.OperationException ?? new Exception(
                    $"Addressables.LoadSceneAsync failed for address '{address}'."));
            };

            return tcs.Task;
        }
#endif

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

#if WGE_ADDRESSABLES
            if (_hasCurrentHandle)
            {
                if (_currentAddressableHandle.IsValid())
                    Addressables.Release(_currentAddressableHandle);

                _hasCurrentHandle = false;
                _currentAddressableHandle = default;
            }
#endif
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SceneLoader));
        }
    }
}
