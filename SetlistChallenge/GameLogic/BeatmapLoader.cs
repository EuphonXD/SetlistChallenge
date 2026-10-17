using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zenject;

namespace SetlistChallenge.GameLogic
{
    /// <summary>
    /// 加载"下一首"的 BeatmapLevel / 难度数据 / 变换后的 beatmapData
    /// 移植自 Shaffuru（MIT），已按 1.44.1 API 改写：
    ///  - 取关卡：BeatmapLevelsModel.GetBeatmapLevel(levelId, ignoreCase)
    ///  - 取等级数据：BeatmapLevelsModel.LoadBeatmapLevelDataAsync(levelId, version, ct)
    ///  - 解析谱面：BeatmapDataLoader.LoadBeatmapDataAsync(...)
    ///  - 变换：BeatmapDataTransformHelper.CreateTransformedBeatmapData(..., ref Settings)
    /// </summary>
    public class BeatmapLoader
    {
        private static readonly System.Reflection.FieldInfo FieldSettingsManager =
            AccessTools.Field(typeof(GameplayCoreSceneSetupData), "_settingsManager");
        private static readonly System.Reflection.FieldInfo FieldSettings =
            AccessTools.Field(typeof(SettingsManager), "settings");

        private readonly GameplayCoreSceneSetupData _sceneSetupData;
        private readonly BeatmapLevelsModel _beatmapLevelsModel;

        public BeatmapLoader(GameplayCoreSceneSetupData sceneSetupData, BeatmapLevelsModel beatmapLevelsModel)
        {
            _sceneSetupData = sceneSetupData;
            _beatmapLevelsModel = beatmapLevelsModel;
        }

        public BeatmapLevel GetLevel(string levelId) => _beatmapLevelsModel?.GetBeatmapLevel(levelId, true);

        private AudioClipAsyncLoader _acl;

        /// <summary>
        /// 取下一首的音频 clip。1.44 的 IBeatmapLevelData 没有 audioClip、关卡对象也不实现 audio clip provider，
        /// 所以直接读歌曲文件夹里的 Info.dat 拿到音频文件名，再用 AudioClipAsyncLoader 从文件加载。
        /// </summary>
        public async Task<AudioClip> LoadAudioClip(string folder)
        {
            try
            {
                if (string.IsNullOrEmpty(folder)) return null;
                var dir = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels", folder);
                if (!Directory.Exists(dir)) { Plugin.Log?.Warn("[SetlistChallenge] 取音频：目录不存在 " + dir); return null; }
                var infoPath = Directory.GetFiles(dir, "info.dat", SearchOption.TopDirectoryOnly).FirstOrDefault()
                            ?? Directory.GetFiles(dir, "Info.dat", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (infoPath == null) { Plugin.Log?.Warn("[SetlistChallenge] 取音频：找不到 Info.dat in " + folder); return null; }
                var info = JObject.Parse(File.ReadAllText(infoPath));
                var songFile = (string)(info["_songFilename"] ?? info["songFilename"] ?? info["song"]?["filename"] ?? info["song"]?["_filename"] ?? info["audio"]?["songFilename"] ?? info["audio"]?["_songFilename"]);
                if (string.IsNullOrEmpty(songFile)) { Plugin.Log?.Warn("[SetlistChallenge] 取音频：Info.dat 没有 _songFilename"); return null; }
                var full = Path.Combine(dir, songFile);
                if (!File.Exists(full)) { Plugin.Log?.Warn("[SetlistChallenge] 取音频：音频文件不存在 " + full); return null; }
                if (_acl == null) _acl = AudioClipAsyncLoader.CreateDefault();
                var clip = await _acl.Load(full);
                if (clip == null) Plugin.Log?.Warn("[SetlistChallenge] 取音频：加载返回 null " + full);
                return clip;
            }
            catch (Exception e) { Plugin.Log?.Warn("[SetlistChallenge] 取下一首音频失败: " + e.Message); }
            return null;
        }

        public async Task<IBeatmapLevelData> LoadLevelData(string levelId)
        {
            var res = await _beatmapLevelsModel.LoadBeatmapLevelDataAsync(levelId, BeatmapLevelDataVersion.Original, CancellationToken.None);
            if (res.isError) throw new Exception("LoadBeatmapLevelDataAsync 失败: " + res.errorMessage);
            return res.beatmapLevelData;
        }

        /// <summary>读某个难度、按当前玩家的设置做变换（左手/环境效果等）</summary>
        public async Task<IReadonlyBeatmapData> LoadTransformedBeatmapData(BeatmapLevel level, IBeatmapLevelData levelData, BeatmapKey key)
        {
            var playerSpecificSettings = _sceneSetupData.playerSpecificSettings;
            var gameplayModifiers = _sceneSetupData.gameplayModifiers;

            var loader = Refl.SceneBeatmapDataLoader?.GetValue(_sceneSetupData) as BeatmapDataLoader;
            if (loader == null) throw new Exception("取不到 BeatmapDataLoader（_beatmapDataLoader）");

            var raw = await loader.LoadBeatmapDataAsync(
                levelData,
                key,
                level.beatsPerMinute,
                false,
                _sceneSetupData.targetEnvironmentInfo,
                _sceneSetupData.originalEnvironmentInfo,
                BeatmapLevelDataVersion.Original,
                gameplayModifiers,
                playerSpecificSettings,
                false);

            var envFilter = (key.difficulty == BeatmapDifficulty.ExpertPlus)
                ? playerSpecificSettings.environmentEffectsFilterExpertPlusPreset
                : playerSpecificSettings.environmentEffectsFilterDefaultPreset;

            var settingsManager = FieldSettingsManager?.GetValue(_sceneSetupData) as SettingsManager;            var settings = (BeatSaber.Settings.Settings)(FieldSettings != null && settingsManager != null
                ? FieldSettings.GetValue(settingsManager)
                : default(BeatSaber.Settings.Settings));

            return BeatmapDataTransformHelper.CreateTransformedBeatmapData(
                raw,
                level,
                gameplayModifiers,
                playerSpecificSettings.leftHanded,
                envFilter,
                _sceneSetupData.targetEnvironmentInfo.environmentIntensityReductionOptions,
                ref settings);
        }
    }
}
