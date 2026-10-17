using System;
using System.IO;
using Newtonsoft.Json;

namespace SetlistChallenge.Configuration
{
    /// <summary>游戏内可改的设置（纯 JSON，存 UserData/SetlistChallenge/settings.json；由工具或游戏内 UI 改）</summary>
    public class SetlistSettings
    {
        /// <summary>失败是否直接结束整场挑战（false = NoFail 式，倒下后继续打完这首）</summary>
        public bool FailEndsRun { get; set; } = false;

        /// <summary>换谱时是否跳过结算界面（无缝连打）</summary>
        public bool SkipResults { get; set; } = true;

        /// <summary>是否显示进度 HUD（第 N/M 首 · 下一首：XXX）</summary>
        public bool ShowProgressHud { get; set; } = true;

        /// <summary>换谱过渡反应时间（毫秒）</summary>
        public int TransitionReactionTimeMs { get; set; } = 500;

        /// <summary>换谱后一段"无敌"宽限（毫秒）</summary>
        public int TransitionGraceMs { get; set; } = 800;

        // ---- 运行时 ----
        [JsonIgnore] private static SetlistSettings _instance;

        public static SetlistSettings Instance => _instance ?? (_instance = Load());

        public static string Path => System.IO.Path.Combine(ChallengeStore.Directory, "settings.json");

        public static SetlistSettings Load()
        {
            try
            {
                if (File.Exists(Path))
                {
                    var s = JsonConvert.DeserializeObject<SetlistSettings>(File.ReadAllText(Path));
                    if (s != null) return s;
                }
            }
            catch (Exception e)
            {
                LoggerHolder.Get()?.Warn($"[SetlistChallenge] 读取 settings.json 失败：{e.Message}");
            }
            var fresh = new SetlistSettings();
            fresh.Save();
            return fresh;
        }

        public void Save()
        {
            try
            {
                System.IO.Directory.CreateDirectory(ChallengeStore.Directory);
                File.WriteAllText(Path, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception e)
            {
                LoggerHolder.Get()?.Warn($"[SetlistChallenge] 写 settings.json 失败：{e.Message}");
            }
        }
    }
}
