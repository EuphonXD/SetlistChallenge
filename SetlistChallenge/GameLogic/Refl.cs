using System.Reflection;
using HarmonyLib;

namespace SetlistChallenge.GameLogic
{
    /// <summary>1.44.1 里用到的私有字段/只读属性访问器（集中在这里，字段名以 api-dump 为准）</summary>
    internal static class Refl
    {
        internal static readonly FieldInfo CallbacksInTimes =
            AccessTools.Field(typeof(BeatmapCallbacksController), "_callbacksInTimes");
        internal static readonly FieldInfo CallbacksDict =
            AccessTools.Field(typeof(CallbacksInTime), "_callbacks");
        internal static readonly FieldInfo AllBeatmapObjects =
            AccessTools.Field(typeof(BeatmapObjectManager), "_allBeatmapObjects");
        internal static readonly FieldInfo DisableSpawning =
            AccessTools.Field(typeof(BeatmapObjectSpawnController), "_disableSpawning");
        internal static readonly FieldInfo SceneBeatmapDataLoader =
            AccessTools.Field(typeof(GameplayCoreSceneSetupData), "_beatmapDataLoader");
        internal static readonly FieldInfo EnergyNoFail =
            AccessTools.Field(typeof(GameEnergyCounter), "<noFail>k__BackingField");
        internal static readonly FieldInfo AudioClipAsyncLoader =
            AccessTools.Field(typeof(GameplayCoreSceneSetupData), "_audioClipAsyncLoader");

        internal static void SetNoFail(GameEnergyCounter counter, bool value)
        {
            if (counter == null) return;
            if (EnergyNoFail != null) { EnergyNoFail.SetValue(counter, value); return; }
            var prop = typeof(GameEnergyCounter).GetProperty("noFail", BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(counter, value);
        }
    }
}
