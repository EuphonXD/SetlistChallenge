using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using SetlistChallenge.GameLogic;

namespace SetlistChallenge.Patches
{
    /// <summary>
    /// 挑战进行中 + 开了「跳过结算」→ 结果界面一出现就自动按掉（连播时不让它挡着）。
    /// 只在挑战还在继续时生效；最后一首打完（Current 已清空）会正常显示最终结算。
    /// </summary>
    [HarmonyPatch(typeof(ResultsViewController), "DidActivate")]
    internal static class SkipResultsPatch
    {
        private static readonly MethodInfo ContinueButtonPressed =
            AccessTools.Method(typeof(ResultsViewController), "ContinueButtonPressed");

        [HarmonyPostfix]
        private static void Postfix(ResultsViewController __instance, bool firstActivation)
        {
            try
            {
                if (!firstActivation) return;
                var run = ChallengeRun.Current;
                if (run == null || !run.SkipResults) return;
                if (__instance == null) return;
                __instance.StartCoroutine(SkipDelayed(__instance));
            }
            catch (Exception e) { Plugin.Log?.Warn("[SetlistChallenge] 跳过结算失败: " + e.Message); }
        }

        private static IEnumerator SkipDelayed(ResultsViewController vc)
        {
            yield return null;
            yield return null;
            try
            {
                ContinueButtonPressed?.Invoke(vc, null);
                Plugin.Log?.Info("[SetlistChallenge] 已跳过结果界面");
            }
            catch (Exception e) { Plugin.Log?.Warn("[SetlistChallenge] 跳过结算失败: " + e.Message); }
        }
    }
}
