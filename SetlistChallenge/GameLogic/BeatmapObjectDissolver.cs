using System;
using System.Collections.Generic;
using Zenject;

namespace SetlistChallenge.GameLogic
{
    /// <summary>换谱时把场上所有物件溶解掉（移植自 Shaffuru, MIT）</summary>
    public class BeatmapObjectDissolver : IInitializable, IDisposable
    {
        private readonly BeatmapObjectManager _beatmapObjectManager;

        private static event Action<float> DoDissolve;

        public static void DissolveAllAndEverything(float dissolveTime) => DoDissolve?.Invoke(dissolveTime);

        public BeatmapObjectDissolver(BeatmapObjectManager beatmapObjectManager)
        {
            _beatmapObjectManager = beatmapObjectManager;
        }

        public void DissolveAll(float dissolveTime)
        {
            var list = Refl.AllBeatmapObjects?.GetValue(_beatmapObjectManager) as System.Collections.IEnumerable;
            if (list == null) return;
            foreach (var obj in list)
            {
                if (obj == null) continue;
                try
                {
                    var m = obj.GetType().GetMethod("Dissolve", new[] { typeof(float) });
                    m?.Invoke(obj, new object[] { dissolveTime });
                }
                catch { }
            }
        }

        public void Initialize() => DoDissolve += DissolveAll;
        public void Dispose() => DoDissolve -= DissolveAll;
    }
}
