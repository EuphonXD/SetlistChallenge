# SetlistChallenge（无缝挑战模式）— 开发路线

the requester 2026-09-28 定的三个设计点：
1. **换谱方式 = Shaffuru 式"游戏场景内直接换谱"**（真无缝，不是"自动进下一首"）
2. **失败不结束 = NoFail 式**（倒下后继续打完这首，对应 `GameEnergyCounter.noFail`）
3. 另外支持 **"按筛选随机"模式**

## 参考实现（可直接移植，MIT 许可）
- `kinsi55/BeatSaber_Shaffuru`（Endless Mode evolved，MIT）
  - `GameLogic/BeatmapSwitcher.cs`（279 行）= 换谱核心：临时关生成 → 溶解现有物件 → 暂停 →
    改**活的** `IReadonlyBeatmapData`（把新谱物件时间平移到当前曲目时间、旧物件推到 float.MaxValue）→
    更新 spawn movement（BPM/NJS/lines）与 callbacks 的 aheadTime → 恢复生成 →
    用自己挂的 `CustomAudioSource` 播下一首的音频（同步到 `startTime + (songTime - timePre)`）
  - `GameLogic/AudioTimeWrapper.cs` / `CustomAudioSource.cs` / `BeatmapObjectDissolver.cs` /
    `GameLogic/SongQueueManager.cs` / `GameLogic/QueueProcessor.cs` / `MenuLogic/UI/FlowCoordinator.cs`
  - 依赖注入：`GameplayCoreSceneSetupData`、`IReadonlyBeatmapData`、`BeatmapObjectSpawnController`、
    `BeatmapCallbacksController`、`GameEnergyCounter`、`IJumpOffsetYProvider`（SiraUtil/Zenject）
  - 注意：它用 `IPA.Utilities.FieldAccessor` 改 `<time>k__BackingField` 等私有字段 → 1.44.1 上要核对字段名

## 状态
- ✅ 项目骨架已建 + **能在 249 上编译通过**（`dotnet 8 SDK` + 游戏引用 + 内嵌 manifest）
  - 位置：NAS `workspace/beatsaber-mod-setlist/SetlistChallenge/`；249 `C:\Users\98097\setlist-build\`
  - 产物 `SetlistChallenge.dll`（0 warning / 0 error）
  - 构建：`dotnet build -c Release -p:BeatSaberDir="<BeatSaberDir>"`
- ✅ 配置模型：`ChallengeSet`（title/mode=setlist|random/failEndsRun/skipResults/shuffle/entries[]/filter）
  读 `UserData/SetlistChallenge/*.json`；设置 `settings.json`（failEndsRun/skipResults/showProgressHud/过渡毫秒）
- ✅ **换谱核心已移植并编译通过**（2026-09-28 凌晨；见 PORT-NOTES.md §7）
- ✅ mod 已装进游戏 + 挑战编辑器完成（2026-09-28 凌晨，见 PORT-NOTES §8）
- ⬜ 明天联调待修（按优先级）：
  0. **换谱后音频尚未接上**（要经 AudioClipAsyncLoader 取新歌 clip）
  0b. 开游戏实测 + 看 [SetlistChallenge] 日志
- ⬜ 其它待做：
  1. ~~移植换谱核心~~ ✅ 已完成
  1b. Zenject 安装器 + 进入挑战的入口（待定：占位关卡 or 直接开局）
  2. 队列：顺序播放 + 自动换谱 + NoFail 语义 + 失败结束开关
  3. UI：主菜单入口（BSML+SiraUtil）→ 挑战列表 / 游戏内设置页 / 进度 HUD
  4. 随机模式（筛选：难度/NPS/Noodle/Chroma/歌单/时长）
  5. 视频工具侧：挑战编辑器（选歌+难度+排序+保存成 json）

## 工具侧配置格式（工具写、游戏读）
```json
{
  "title": "周末挑战",
  "mode": "setlist",
  "failEndsRun": false,
  "skipResults": true,
  "shuffle": false,
  "entries": [
    { "folder": "13b80 (Emir - Hitchcock)", "characteristic": "Standard", "difficulty": "ExpertPlus" }
  ]
}
```
