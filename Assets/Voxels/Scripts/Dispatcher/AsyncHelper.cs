using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Voxels.Scripts.Dispatcher
{
    public class AsyncHelper : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> _actionQueue = new ();
        private static readonly List<IAsyncHandler> _asyncHandlers = new ();

        private int frameTimer = 0;

        private interface IAsyncHandler
        {
            bool IsComplete();
            void Handle();
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

        public static void RunOnMainThreadWhenComplete(IEnumerable<JobHandle> handles, Action action)
        {
            if (action == null || handles == null) return;
            _asyncHandlers.Add(new MultiJobAsyncHandler(handles, action));
        }

        private void Update()
        {
            while (_actionQueue.TryDequeue(out var action))
            {
                action();
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

        private class MultiJobAsyncHandler : IAsyncHandler
        {
            private readonly IEnumerable<JobHandle> _handles;
            private readonly Action _action;

            public MultiJobAsyncHandler(IEnumerable<JobHandle> handles, Action action)
            {
                _handles = handles;
                _action = action;
            }

            public bool IsComplete()
            {
                foreach (var jobHandle in _handles)
                {
                    if (!jobHandle.IsCompleted) return false;
                }
                return true;
            }

            public void Handle()
            {
                foreach (var jobHandle in _handles)
                {
                    jobHandle.Complete();
                }
                _action?.Invoke();
            }
        }
    }
}