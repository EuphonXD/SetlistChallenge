using System;
using System.Reflection;
using HarmonyLib;
using IPA;
using IPA.Loader;
using IPA.Logging;
using SetlistChallenge.Configuration;
using SiraUtil.Zenject;

namespace SetlistChallenge
{
    /// <summary>
    /// 无缝挑战模式（Setlist Challenge）
    /// - 预设顺序/难度连打（配置由 BeatSaber 视频工具写入 UserData/SetlistChallenge/*.json）
    /// - Shaffuru 式的"游戏场景内直接换谱"（GameLogic 里实现）
    /// - 失败不结束（NoFail 式）、可切换"按筛选随机"模式
    /// </summary>
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static Logger Log { get; private set; }
        internal static PluginMetadata Metadata { get; private set; }
        internal static Harmony Harmony { get; private set; }

        [Init]
        public Plugin(Logger logger, PluginMetadata metadata, Zenjector zenjector)
        {
            Log = logger;
            LoggerHolder.Set(logger);
            Metadata = metadata;
            Instance = this;
            Log.Info("[SetlistChallenge] 插件载入中…");
            try
            {
                zenjector.Install<Installers.AppInstaller>(Location.App);
                zenjector.Install<Installers.MenuInstaller>(Location.Menu);
                zenjector.Install<Installers.GameInstaller>(Location.GameCore);
                Log.Info("[SetlistChallenge] 安装器已注册（App / Menu / GameCore）");
            }
            catch (Exception e)
            {
                Log.Error("[SetlistChallenge] 注册安装器失败: " + e);
            }
        }

        [OnStart]
        public void OnApplicationStart()
        {
            Harmony = new Harmony("yofeng.beatsaber.setlistchallenge");
            Harmony.PatchAll(Assembly.GetExecutingAssembly());

            ChallengeStore.Reload();
            var s = SetlistSettings.Instance;
            Log.Info($"[SetlistChallenge] 就绪：{ChallengeStore.Sets.Count} 个挑战 · 目录 {ChallengeStore.Directory}");
            Log.Info($"[SetlistChallenge] 设置：失败结束={s.FailEndsRun} 跳过结算={s.SkipResults} 进度HUD={s.ShowProgressHud} 过渡={s.TransitionReactionTimeMs}ms");
        }

        [OnExit]
        public void OnApplicationQuit()
        {
            try { Harmony?.UnpatchSelf(); } catch { }
        }
    }

    /// <summary>给静态上下文用的 Logger 兜底</summary>
    internal static class LoggerHolder
    {
        private static Logger _logger;
        public static void Set(Logger logger) => _logger = logger;
        public static Logger Get() => _logger;
    }
}
