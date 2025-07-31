using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Voxels.Scripts.Dispatcher
{
    public class AsyncHelper : MonoBehaviour
    {
        private const int MaxTasksSpawned = 4;
        
        private static readonly ConcurrentQueue<Action> _actionQueue = new ();
        private static readonly List<IAsyncHandler> _asyncHandlers = new ();
        private static readonly ConcurrentQueue<Action> _queuedTasks = new ();
        private static int spawnedTasks = 0;

        private static ConcurrentDictionary<IDisposable, byte> nativeObjects = new();
        

        private int frameTimer = 0;

        private interface IAsyncHandler
        {
            bool IsComplete();
            void Handle();
        }

        private static T TrackNativeObject<T>(T obj) where T : IDisposable
        {
            nativeObjects[obj] = 0;
            return obj;
        }
        
        public static NativeArray<T> CreatePersistentNativeArray<T>(NativeArray<T> existing) where T : struct
        {
            return TrackNativeObject(new NativeArray<T>(existing, Allocator.Persistent));
        }
        public static NativeArray<T> CreatePersistentNativeArray<T>(T[] existing) where T : struct
        {
            return TrackNativeObject(new NativeArray<T>(existing, Allocator.Persistent));
        }
        public static NativeArray<T> CreatePersistentNativeArray<T>(
            int initialCapacity,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory
        ) where T : struct {
            return TrackNativeObject(new NativeArray<T>(initialCapacity, Allocator.Persistent, options));
        }
        public static NativeList<T> CreatePersistentNativeList<T>() where T : unmanaged
        {
            return TrackNativeObject(new NativeList<T>(Allocator.Persistent));
        }
        public static NativeList<T> CreatePersistentNativeList<T>(int initialCapacity) where T : unmanaged
        {
            return TrackNativeObject(new NativeList<T>(initialCapacity, Allocator.Persistent));
        }

        public static void DisposeNativeObject(IDisposable disposable)
        {
            if (disposable == null) return;
            if (!nativeObjects.TryRemove(disposable, out _)) return;
            disposable.Dispose();
        }
        
        private void OnDestroy()
        {
            foreach (var nativeObject in nativeObjects.Keys)
            {
                nativeObject.Dispose();
            }
        }

        public static void QueueTask(Action action)
        {
            _queuedTasks.Enqueue(() =>
            {
                action();
                Interlocked.Decrement(ref spawnedTasks);
            });
        }

        public static void QueueTask(Action<Action> action)
        {
            _queuedTasks.Enqueue(() =>
            {
                action(() => Interlocked.Decrement(ref spawnedTasks));
            });
        }
        
        public static void RunOnMainThread(Action action)
        {
            if (action == null) return;
            _actionQueue.Enqueue(action);
        }

        public static void RunOnMainThreadWhenComplete(JobHandle handle, Action action)
        {
            if (action == null) return;
            _asyncHandlers.Add(new SingleJobAsyncHandler(handle, action));
        }

        private void Update()
        {
            while (_actionQueue.TryDequeue(out var action))
            {
                action();
            }

            while (spawnedTasks < MaxTasksSpawned && _queuedTasks.TryDequeue(out var action))
            {
                Task.Run(action);
                Interlocked.Increment(ref spawnedTasks);
            }
            
            frameTimer++;
            if (frameTimer < 4) return;
            
            frameTimer = 0;
            int lastIndex = _asyncHandlers.Count - 1;
            for (int i = lastIndex; i >= 0; i--)
            {
                IAsyncHandler handler = _asyncHandlers[i];
                if (!handler.IsComplete()) continue;

                if (i != lastIndex)
                {
                    // Removing from the middle of a list requires the items to the right to be shifted which can be an
                    // O(n) operation, instead we replace the current item we with to delete with the item currently at
                    // the end of the list, then remove the last item. This is then O(1), we can do this since we do not
                    // care about the order of elements for this list.
                    _asyncHandlers[i] = _asyncHandlers[lastIndex];
                }
                _asyncHandlers.RemoveAt(lastIndex--);
                handler.Handle();
            }
        }

        private class SingleJobAsyncHandler : IAsyncHandler
        {
            private JobHandle _handle;
            private readonly Action _action;

            public SingleJobAsyncHandler(JobHandle handle, Action action)
            {
                _handle = handle;
                _action = action;
            }

            public bool IsComplete()
            {
                return _handle.IsCompleted;
            }

            public void Handle()
            {
                _handle.Complete();
                _action?.Invoke();
            }
        }
    }
}