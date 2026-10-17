using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using SetlistChallenge.Configuration;
using SetlistChallenge.GameLogic;
using TMPro;
using Zenject;

namespace SetlistChallenge.UI
{
    /// <summary>游戏内设置页（BSML）：挑战列表 + 失败行为等开关 + 开始按钮</summary>
    public class ChallengeSettingsView : IInitializable, IDisposable
    {
        private readonly ChallengeStarter _starter;
        private readonly BeatSaberMarkupLanguage.Settings.BSMLSettings _bsmlSettings;

        [UIComponent("lblStatus")]
        private TextMeshProUGUI _statusText;

        public ChallengeSettingsView(ChallengeStarter starter, [InjectOptional] BeatSaberMarkupLanguage.Settings.BSMLSettings bsmlSettings)
        {
            _starter = starter;
            _bsmlSettings = bsmlSettings;
        }

        public List<object> ChallengeNames { get; set; } = new List<object>();
        public string ChallengeName { get; set; }
        public bool FailEndsRun { get; set; }
        public bool SkipResults { get; set; }
        public bool ShowProgressHud { get; set; }
        public string Status { get; set; } = "　";

        private void SetStatus(string s)
        {
            Status = s;
            try { if (_statusText != null) _statusText.text = s; } catch { }
        }

        [UIAction("#post-parse")]
        public void OnPostParse()
        {
            SetStatus(Status);
        }

        public void Initialize()
        {
            var s = SetlistSettings.Instance;
            FailEndsRun = s.FailEndsRun;
            SkipResults = s.SkipResults;
            ShowProgressHud = s.ShowProgressHud;
            RefreshList();
            try
            {
                if (_bsmlSettings != null)
                {
                    _bsmlSettings.AddSettingsMenu("无缝挑战", "SetlistChallenge.UI.settings.bsml", this);
                    Plugin.Log?.Info("[SetlistChallenge] 已注册设置菜单（Mods 设置 → 无缝挑战）");
                }
                else Plugin.Log?.Warn("[SetlistChallenge] 拿不到 BSMLSettings，设置菜单未注册");
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[SetlistChallenge] 注册设置菜单失败: " + e);
            }
        }

        public void RefreshList()
        {
            ChallengeStore.Reload();
            ChallengeNames = ChallengeStore.Sets.Select(x => (object)x.Title).ToList();
            if (ChallengeNames.Count == 0) { ChallengeName = null; SetStatus("还没有挑战（去视频工具里新建）"); }
            else
            {
                if (string.IsNullOrEmpty(ChallengeName) || !ChallengeNames.Contains(ChallengeName)) ChallengeName = (string)ChallengeNames[0];
                var set = CurrentSet();
                SetStatus(set == null ? "　" : $"{(set.Mode == "random" ? "随机" : (set.Entries?.Count ?? 0) + " 首")} · {(set.FailEndsRun == true ? "失败即结束" : (set.FailEndsRun == false ? "NoFail 式" : "跟随设置"))}");
            }
        }

        private ChallengeSet CurrentSet()
        {
            if (string.IsNullOrEmpty(ChallengeName)) return null;
            return ChallengeStore.Sets.FirstOrDefault(x => x.Title == ChallengeName);
        }

        public void OnPick(object value)
        {
            var s = value as string;
            if (!string.IsNullOrEmpty(s)) ChallengeName = s;
            SetStatus("　");
            RefreshList();
        }

        public void OnToggle(object value)
        {
            var s = SetlistSettings.Instance;
            s.FailEndsRun = FailEndsRun;
            s.SkipResults = SkipResults;
            s.ShowProgressHud = ShowProgressHud;
            s.Save();
            SetStatus("设置已保存 ✓");
        }

        public void StartChallenge()
        {
            var set = CurrentSet();
            if (set == null) { SetStatus("❌ 请先选一个挑战"); return; }
            SetStatus("▶ 开始：" + set.Title);
            _starter.Start(set);
        }

        public void Dispose() { }
    }
}
