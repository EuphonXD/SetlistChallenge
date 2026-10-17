using System;
using System.Threading.Tasks;
using Zenject;

namespace SetlistChallenge.GameLogic
{
    /// <summary>
    /// 在游戏场景里盯着：当前这首快结束时，在场景内直接换到挑战的下一首（Shaffuru 式无缝）
    /// </summary>
    public class QueueProcessor : ITickable
    {
        private readonly AudioTimeWrapper _audio;
        private readonly LevelResolver _resolver;
        private readonly BeatmapLoader _loader;
        private readonly BeatmapSwitcher _switcher;

        private bool _busy;
        private float _curSongStartTime;   // 当前这首歌在（连续）songTime 里的起点，换歌不归零所以要记

        public QueueProcessor(AudioTimeWrapper audio, LevelResolver resolver, BeatmapLoader loader, BeatmapSwitcher switcher)
        {
            _audio = audio;
            _resolver = resolver;
            _loader = loader;
            _switcher = switcher;
        }

        // 【临时】场景内无缝换谱暂不可靠（谱面数据能插入，但游戏不生成它们），
        // 改用"一首播完 → 游戏正常结算 → levelFinished 回调自动开下一首"的稳方案（the requester实测那条完全正常）。
        // 想继续啃无缝就把这个改回 true。
        private const bool SeamlessSwapEnabled = false;

        public void Tick()
        {
            if (!SeamlessSwapEnabled) return;   // 见上：暂时走"一首完自动开下一首"
            try
            {
                var run = ChallengeRun.Current;
                if (run == null || run.Finished || _busy) return;
                if (_audio == null || !_audio.IsValid) return;

                var t = _audio.SongTime;
                var len = _audio.SongLength;
                if (len <= 1f) return;
                // 注：songTime 是连续的（跳歌不归零），所以要按"本首歌的起点"算结尾，否则会连环误触发
                if (t < 1f || t < _curSongStartTime + len - 1.0f) return;

                var next = run.Index + 1;
                if (next >= run.Total)
                {
                    // 最后一首：交给正常结算流程
                    return;
                }

                _busy = true;
                _ = SwapTo(run, next);
            }
            catch (Exception e)
            {
                _busy = false;
                Plugin.Log?.Error("[SetlistChallenge] Tick 出错: " + e.Message);
            }
        }

        private async Task SwapTo(ChallengeRun run, int index)
        {
            try
            {
                var entry = run.Entries[index];
                var res = await _resolver.Resolve(entry);
                if (res == null || res.Level == null || res.LevelData == null)
                {
                    Plugin.Log?.Warn($"[SetlistChallenge] 第 {index + 1} 首换谱失败（解析不到），跳过");
                    return;
                }

                var data = await _loader.LoadTransformedBeatmapData(res.Level, res.LevelData, res.Key);
                if (data == null) { Plugin.Log?.Warn("[SetlistChallenge] 换谱：变换谱面为 null"); return; }
                try { Plugin.Log?.Info($"[SetlistChallenge] 换谱诊断：diff={res.Key.difficulty} char={res.Key.beatmapCharacteristic?.serializedName} levelData={(res.LevelData != null)} replItems={data.allBeatmapDataItems?.Count ?? -1}"); } catch { }

                // 下一首的音频 clip（用歌曲文件夹路径直接加载）
                var clip = await _loader.LoadAudioClip(entry.Folder);

                var basic = res.Level.GetDifficultyBeatmapData(res.Key.beatmapCharacteristic, res.Key.difficulty);
                var njs = basic?.noteJumpMovementSpeed ?? 0f;
                var offset = basic?.noteJumpStartBeatOffset ?? 0f;

                run.Index = index;
                _switcher.SetNoFail(!run.FailEndsRun);
                var songStartAt = _audio.SongTime;
                _switcher.SwitchToBeatmap(data, res.Level.beatsPerMinute, njs, offset, data.numberOfLines, 0f, 0f, clip);
                // 记下新歌的起点，下次要到"起点 + 本首歌长度"才再换
                _curSongStartTime = songStartAt;

                Plugin.Log?.Info($"[SetlistChallenge] ↔ 换到第 {index + 1}/{run.Total} 首：{res.Level.songName} ({entry.Difficulty})");
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[SetlistChallenge] 换谱异常: " + e);
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
