using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using SetlistChallenge.Configuration;
using Zenject;

namespace SetlistChallenge.GameLogic
{
    /// <summary>一次挑战运行时的状态（当前挑战 / 队列 / 进度）</summary>
    public class ChallengeRun
    {
        public static ChallengeRun Current;

        public ChallengeSet Set;
        public List<ChallengeEntry> Entries = new List<ChallengeEntry>();
        public int Index = -1;                       // 当前正在玩第几首
        public bool FailEndsRun;
        public bool SkipResults;
        public bool ShowProgressHud;
        public List<string> History = new List<string>();
        public bool Finished;

        public ChallengeEntry CurrentEntry => (Index >= 0 && Index < Entries.Count) ? Entries[Index] : null;
        public ChallengeEntry NextEntry => (Index + 1 < Entries.Count) ? Entries[Index + 1] : null;
        public int Total => Entries.Count;

        public static void StartNew(ChallengeSet set, RandomPicker picker)
        {
            var s = SetlistSettings.Instance;
            var run = new ChallengeRun
            {
                Set = set,
                FailEndsRun = set.FailEndsRun ?? s.FailEndsRun,
                SkipResults = set.SkipResults ?? s.SkipResults,
                ShowProgressHud = s.ShowProgressHud
            };

            if (string.Equals(set.Mode, "random", StringComparison.OrdinalIgnoreCase))
                run.Entries = picker != null ? picker.Pick(set.Filter, set.Filter?.Count ?? 10) : new List<ChallengeEntry>();
            else
                run.Entries = new List<ChallengeEntry>(set.Entries ?? new List<ChallengeEntry>());

            if (set.Shuffle)
            {
                var rnd = new Random();
                run.Entries = run.Entries.OrderBy(_ => rnd.Next()).ToList();
            }

            Current = run;
            Plugin.Log?.Info($"[SetlistChallenge] 开始挑战「{set.Title}」：{run.Entries.Count} 首 · 失败结束={run.FailEndsRun} 跳过结算={run.SkipResults}");
        }

        public void Stop() { Finished = true; Current = null; }
    }

    /// <summary>把配置里的条目变成"能开局的"东西：levelId / BeatmapLevel / BeatmapKey / levelData</summary>
    public class LevelResolver
    {
        private readonly BeatmapLevelsModel _levels;
        private readonly BeatmapCharacteristicCollection _characteristics;

        public LevelResolver(BeatmapLevelsModel levels, [InjectOptional] BeatmapCharacteristicCollection characteristics)
        {
            _levels = levels;
            _characteristics = characteristics;
        }

        public class Resolved
        {
            public string LevelId;
            public BeatmapKey Key;
            public BeatmapLevel Level;
            public IBeatmapLevelData LevelData;
        }

        public async Task<Resolved> Resolve(ChallengeEntry entry)
        {
            if (entry == null) return null;

            var levelId = entry.LevelId;
            if (string.IsNullOrEmpty(levelId)) levelId = ResolveLevelIdFromFolder(entry.Folder);
            if (string.IsNullOrEmpty(levelId))
            {
                Plugin.Log?.Warn($"[SetlistChallenge] 解析不到关卡（没 levelId，按 folder 也找不到）：{entry.Folder}");
                return null;
            }

            var level = _levels.GetBeatmapLevel(levelId, true);
            if (level == null) { Plugin.Log?.Warn($"[SetlistChallenge] 找不到关卡：{levelId}"); return null; }

            var characteristic = ResolveCharacteristic(level, entry.Characteristic, _characteristics);
            var key = new BeatmapKey(levelId, characteristic, ParseDifficulty(entry.Difficulty));

            IBeatmapLevelData data = null;
            try { data = (await _levels.LoadBeatmapLevelDataAsync(levelId, BeatmapLevelDataVersion.Original, System.Threading.CancellationToken.None)).beatmapLevelData; }
            catch (Exception e) { Plugin.Log?.Warn($"[SetlistChallenge] 读等级数据失败 {levelId}: {e.Message}"); }

            return new Resolved { LevelId = levelId, Key = key, Level = level, LevelData = data };
        }

        /// <summary>按文件夹名在 SongCore 已加载的自定义关卡里找 levelId</summary>
        private static string ResolveLevelIdFromFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            try
            {
                foreach (var kv in SongCore.Loader.CustomLevels)
                {
                    var lvl = kv.Value;
                    if (lvl == null) continue;
                    // SongCore.Loader.CustomLevels 的键是文件夹路径（不是 levelId）
                    var key = kv.Key ?? "";
                    var hit = key.EndsWith("\\" + folder, StringComparison.OrdinalIgnoreCase)
                           || key.EndsWith("/" + folder, StringComparison.OrdinalIgnoreCase);
                    if (!hit)
                    {
                        var n = lvl.songName;
                        hit = !string.IsNullOrEmpty(n) && folder.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    if (!hit) continue;
                    var id = lvl.levelID;
                    if (!string.IsNullOrEmpty(id)) return id;
                }
            }
            catch (Exception e) { Plugin.Log?.Warn("[SetlistChallenge] 按文件夹找关卡失败: " + e.Message); }
            return null;
        }

        public static BeatmapDifficulty ParseDifficulty(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "easy": return BeatmapDifficulty.Easy;
                case "normal": return BeatmapDifficulty.Normal;
                case "hard": return BeatmapDifficulty.Hard;
                case "expert": return BeatmapDifficulty.Expert;
                case "expertplus":
                case "expert+":
                default: return BeatmapDifficulty.ExpertPlus;
            }
        }

        public static BeatmapCharacteristicSO ResolveCharacteristic(BeatmapLevel level, string wanted, BeatmapCharacteristicCollection collection)
        {
            if (collection != null)
            {
                if (!string.IsNullOrEmpty(wanted))
                {
                    var c = collection.GetBeatmapCharacteristicBySerializedName(wanted);
                    if (c != null) return c;
                }
                return collection.GetBeatmapCharacteristicBySerializedName("Standard");
            }
            return null;
        }
    }

    /// <summary>筛选随机（v0.1：从自定义关卡里随机抽 + 指定难度；NPS/歌单/特殊谱面筛选留待后续）</summary>
    public class RandomPicker
    {
        private readonly BeatmapLevelsModel _levels;

        public RandomPicker(BeatmapLevelsModel levels) { _levels = levels; }

        public List<ChallengeEntry> Pick(RandomFilter filter, int count)
        {
            var outList = new List<ChallengeEntry>();
            try
            {
                var pool = new List<BeatmapLevel>();
                foreach (var pack in _levels.GetAllPacks())
                {
                    if (pack == null) continue;
                    var id = pack.packID ?? "";
                    if (id.IndexOf("CustomLevels", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var lvls = pack.AllBeatmapLevels();
                    if (lvls == null) continue;
                    foreach (var lvl in lvls) if (lvl != null) pool.Add(lvl);
                }

                var rnd = new Random();
                var wantedDiff = string.IsNullOrEmpty(filter?.Difficulty) ? "ExpertPlus" : filter.Difficulty;
                var wantedChar = string.IsNullOrEmpty(filter?.Characteristic) ? "Standard" : filter.Characteristic;

                foreach (var lvl in pool.OrderBy(_ => rnd.Next()).Take(Math.Max(1, count)))
                    outList.Add(new ChallengeEntry { LevelId = lvl.levelID, Characteristic = wantedChar, Difficulty = wantedDiff });

                Plugin.Log?.Info($"[SetlistChallenge] 随机模式抽到 {outList.Count} 首（候选池 {pool.Count}）");
            }
            catch (Exception e) { Plugin.Log?.Error($"[SetlistChallenge] 随机抽歌失败: {e}"); }
            return outList;
        }
    }
}
