using System;
using System.Threading.Tasks;
using SetlistChallenge.Configuration;
using Zenject;

namespace SetlistChallenge.GameLogic
{
    /// <summary>开局：按挑战的第一首启动一次标准关卡；之后由 QueueProcessor 在场景内换谱</summary>
    public class ChallengeStarter
    {
        private readonly MenuTransitionsHelper _transitions;
        private readonly LevelResolver _resolver;
        private readonly EnvironmentsListModel _environments;
        private readonly PlayerDataModel _playerData;
        private readonly RandomPicker _picker;

        public ChallengeStarter(MenuTransitionsHelper transitions, LevelResolver resolver,
            EnvironmentsListModel environments, PlayerDataModel playerData, RandomPicker picker)
        {
            _transitions = transitions;
            _resolver = resolver;
            _environments = environments;
            _playerData = playerData;
            _picker = picker;
        }

        public async void Start(ChallengeSet set)
        {
            try
            {
                if (set == null) { Plugin.Log?.Error("[SetlistChallenge] 没选挑战（null）"); return; }
                ChallengeRun.StartNew(set, _picker);
                var run = ChallengeRun.Current;
                if (run == null || run.Entries.Count == 0) { Plugin.Log?.Error("[SetlistChallenge] 挑战里没有可玩的曲目"); return; }
                run.Index = 0;
                await StartIndex(run, 0);
            }
            catch (Exception e)
            {
                Plugin.Log?.Error($"[SetlistChallenge] 开局失败: {e}");
            }
        }

        public async Task StartIndex(ChallengeRun run, int index)
        {
            if (run == null || index < 0 || index >= run.Entries.Count) return;
            var entry = run.Entries[index];
            var res = await _resolver.Resolve(entry);
            if (res == null || res.Level == null) { Plugin.Log?.Error($"[SetlistChallenge] 第 {index + 1} 首解析失败，跳过"); return; }

            run.Index = index;
            Plugin.Log?.Info($"[SetlistChallenge] ▶ 第 {index + 1}/{run.Total} 首：{res.Level.songName} ({entry.Difficulty})");

            var modifiers = _playerData?.playerData?.gameplayModifiers;
            if (modifiers != null)
            {
                // 失败不结束 = 打开游戏自带的 No Fail 修饰符
                modifiers = modifiers.CopyWith(noFailOn0Energy: !run.FailEndsRun);
            }
            var playerSettings = _playerData?.playerData?.playerSpecificSettings;

            var key = res.Key;
            _transitions.StartStandardLevel(
                "Solo",
                ref key,
                res.Level,
                null,
                null,
                false,
                modifiers,
                playerSettings,
                null,
                _environments,
                new GameplayAdditionalInformation("菜单"),
                null,
                null,
                (setupData, results) =>
                {
                    // 回到菜单/结算了（说明场景内换谱没接管）→ 兜底：接着开下一首
                    try
                    {
                        if (ChallengeRun.Current == null) return;
                        if (results != null && ChallengeRun.Current.FailEndsRun && !results.levelEndStateType.ToString().Contains("Cleared"))
                        {
                            Plugin.Log?.Info("[SetlistChallenge] 失败且设置为「失败即结束」，挑战结束");
                            ChallengeRun.Current.Stop();
                            return;
                        }
                        int next = ChallengeRun.Current.Index + 1;
                        if (next < ChallengeRun.Current.Total) StartIndex(ChallengeRun.Current, next);
                        else { Plugin.Log?.Info("[SetlistChallenge] 🏁 挑战完成"); ChallengeRun.Current.Stop(); }
                    }
                    catch (Exception e) { Plugin.Log?.Error("[SetlistChallenge] 结算回调出错: " + e.Message); }
                },
                null,
                // 注意：不能同时提供 beatmapLevelData 和游戏自带的 beatmapLevelsModel（否则 ctor 报 ArgumentException）→ 传 null 让游戏自己加载
                null,
                null);
        }
    }
}
