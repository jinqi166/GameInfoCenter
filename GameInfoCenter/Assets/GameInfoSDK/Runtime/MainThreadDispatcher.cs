using System;
using System.Collections.Generic;

namespace GameInfo.Runtime
{
    internal static class MainThreadDispatcher
    {
        private static readonly object Gate = new object();

        private static readonly List<Action> Pending = new List<Action>(64);

        public static void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }

            lock (Gate)
            {
                Pending.Add(action);
            }
        }

        public static void ExecutePending()
        {
            List<Action> copy;
            lock (Gate)
            {
                if (Pending.Count == 0)
                {
                    return;
                }

                copy = new List<Action>(Pending);
                Pending.Clear();
            }

            for (var i = 0; i < copy.Count; i++)
            {
                try
                {
                    copy[i]();
                }
                catch
                {
                    // swallow to keep dispatcher alive
                }
            }
        }
    }
}
