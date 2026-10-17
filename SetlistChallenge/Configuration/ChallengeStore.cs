using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SetlistChallenge.Configuration
{
    /// <summary>一条挑战曲目（由视频工具写入）</summary>
    public class ChallengeEntry
    {
        /// <summary>歌曲文件夹名（如 "13b80 (Emir - Hitchcock)"），用于在本地曲库定位</summary>
        public string Folder { get; set; }

        /// <summary>可选：等级 levelId（custom_level_XXXX），优先于 Folder</summary>
        public string LevelId { get; set; }

        /// <summary>玩法特征（Standard / OneSaber / 90Degree / 360Degree / NoArrows）</summary>
        public string Characteristic { get; set; } = "Standard";

        /// <summary>难度（Easy / Normal / Hard / Expert / ExpertPlus）</summary>
        public string Difficulty { get; set; } = "ExpertPlus";

        /// <summary>可选：本首单独指定失败是否结束</summary>
        public bool? FailEndsRun { get; set; }
    }

    /// <summary>随机模式的筛选条件</summary>
    public class RandomFilter
    {
        public string Difficulty { get; set; } = "ExpertPlus";
        public string Characteristic { get; set; } = "Standard";
        /// <summary>限定歌单（.bplist 文件名），留空=全部</summary>
        public string Playlist { get; set; }
        public float? MinNps { get; set; }
        public float? MaxNps { get; set; }
        /// <summary>随机模式要抽多少首（0=按时长）</summary>
        public int Count { get; set; } = 10;
        /// <summary>随机模式目标时长（分钟），>0 时优先按时长</summary>
        public int DurationMinutes { get; set; } = 0;
        /// <summary>只要用了 NoodleExtensions 的图</summary>
        public bool RequireNoodle { get; set; } = false;
        /// <summary>只要用了 Chroma 的图</summary>
        public bool RequireChroma { get; set; } = false;
    }

    /// <summary>一个挑战（setlist=固定顺序 / random=按筛选随机）</summary>
    public class ChallengeSet
    {
        public string Title { get; set; } = "未命名挑战";
        /// <summary>setlist | random</summary>
        public string Mode { get; set; } = "setlist";
        /// <summary>失败是否结束（null = 用游戏内设置）</summary>
        public bool? FailEndsRun { get; set; }
        /// <summary>是否跳过结算界面（null = 用游戏内设置）</summary>
        public bool? SkipResults { get; set; }
        /// <summary>是否打乱顺序（setlist 模式可选）</summary>
        public bool Shuffle { get; set; } = false;
        public RandomFilter Filter { get; set; }
        public List<ChallengeEntry> Entries { get; set; } = new List<ChallengeEntry>();

        // ---- 运行时（不写盘）----
        [JsonIgnore] public string FileName { get; set; }
        [JsonIgnore] public int ResolvedCount { get; set; }
    }

    /// <summary>读取/保存 UserData/SetlistChallenge/*.json（视频工具写，游戏内读）</summary>
    public static class ChallengeStore
    {
        public static readonly List<ChallengeSet> Sets = new List<ChallengeSet>();

        public static string Directory =>
            Path.Combine(Environment.CurrentDirectory, "UserData", "SetlistChallenge");

        public static void Reload()
        {
            Sets.Clear();
            try
            {
                if (!System.IO.Directory.Exists(Directory)) System.IO.Directory.CreateDirectory(Directory);
                foreach (var file in System.IO.Directory.GetFiles(Directory, "*.json"))
                {
                    try
                    {
                        // 跳过设置文件（settings.json 不是挑战）
                        if (string.Equals(Path.GetFileNameWithoutExtension(file), "settings", StringComparison.OrdinalIgnoreCase)) continue;
                        var json = File.ReadAllText(file);
                        var probe = Newtonsoft.Json.Linq.JObject.Parse(json);
                        if (probe["mode"] == null && probe["entries"] == null) continue; // 不像挑战
                        var set = JsonConvert.DeserializeObject<ChallengeSet>(json);
                        if (set == null) continue;
                        set.FileName = Path.GetFileNameWithoutExtension(file);
                        if (string.IsNullOrEmpty(set.Title)) set.Title = set.FileName;
                        if (string.Equals(set.Mode, "random", StringComparison.OrdinalIgnoreCase) && set.Filter == null)
                            set.Filter = new RandomFilter();
                        Sets.Add(set);
                    }
                    catch (Exception e)
                    {
                        LoggerHolder.Get()?.Warn($"[SetlistChallenge] 解析挑战失败 {Path.GetFileName(file)}: {e.Message}");
                    }
                }
                Sets.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
            }
            catch (Exception e)
            {
                LoggerHolder.Get()?.Error($"[SetlistChallenge] 读取挑战目录失败: {e}");
            }
        }
    }
}
