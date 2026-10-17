# SetlistChallenge — 移植笔记（1.44.1 适配 Shaffuru 换谱核心）

> 写于 2026-09-28 凌晨（今晚继续移植，怕明天忘）
> 项目：`workspace/beatsaber-mod-setlist/`；构建机：249（Windows）
> 目标 mod：**无缝挑战模式**（预设顺序/难度连打 + 失败不结束(NoFail式) + 按筛选随机）

---

## 0. 一句话现状
- ✅ 骨架能编译（`SetlistChallenge.dll`，内嵌 manifest gameVersion=1.44.1）
- ✅ **1.44.1 API 已 dump 出来**：`api-dump-1.44.1.txt`（61.5KB，用 `apidump` 工具生成，见 §4）
- ⬜ 换谱核心尚未移植（下面的 API 差异表就是移植时的对照表）

## 1. 参考实现（MIT，可直接移植）
`kinsi55/BeatSaber_Shaffuru`（Endless Mode evolved）—— 本地副本 `/tmp/shaffuru/BeatSaber_Shaffuru-master`（NAS 重启会丢，必要时重新拉）

核心文件与职责：
| 文件 | 行数 | 作用 |
|---|---|---|
| `GameLogic/BeatmapSwitcher.cs` | 279 | **换谱核心**：溶解旧物件 → 暂停 → 改写活的 beatmapData（时间平移）→ 更新 spawn 参数/aheadTime → 用自定义 AudioSource 接新音频 |
| `GameLogic/AudioTimeWrapper.cs` | 61 | 包 `AudioTimeSyncController` + 它的 `_audioSource`（Pause/Resume/songTime） |
| `GameLogic/CustomAudioSource.cs` | 85 | 自己挂一个 AudioSource（输出到 Music mixer），专门播下一首的 clip |
| `GameLogic/BeatmapObjectDissolver.cs` | 33 | 遍历 `BeatmapObjectManager._allBeatmapObjects` 逐个 `Dissolve(t)` |
| `GameLogic/BeatmapLoader.cs` | 69 | `BeatmapLevelsModel` 取等级 → `TransformDifficulty()` 做 transformed beatmapData |
| `GameLogic/QueueProcessor.cs` | 177 | Tick 里判断"该换谱了"→ 选下一首 → 异步加载 → 触发 `switchedToNewSong` |
| `GameLogic/SongQueueManager.cs` | 68 | 队列/去重/随机挑（历史记录防重复） |
| `Installers/GameInstaller.cs` | — | **关键**：只在"我们自己造的关卡"里 Activate（用 levelID 前缀判断），Zenject 绑定上面这些 |

**换谱时序**（BeatmapSwitcher.Switcher）：
1. 关生成（`_disableSpawning = true`）
2. `BeatmapObjectDissolver.DissolveAllAndEverything(dissolveTime)`
3. （若剩余>30s）暂停音频 → 换 clip → 清内存 → 恢复
4. 拿当前 `songTime`，把新谱的 item 按 `relativeSongTime + currentAudioTime` 写入**活的** `IReadonlyBeatmapData.allBeatmapDataItems`（旧的推到 `float.MaxValue`）；arc/slider 要同步改 `tailTime`
5. 更新 `BeatmapObjectSpawnMovementData`（BPM/NJS/轨道数）与 callbacks 的 `aheadTime`
6. 恢复生成；等一小段；`customAudioSource.SetClip(...)` + `Play(startTime + (songTime - timePre))`
7. 过渡宽限期里 `gameEnergyCounter.noFail = true`（防止换谱时被撞死）

---

## 2. 🔴 1.44.1 与旧 API 的差异对照表（移植关键！）

数据来源：`api-dump-1.44.1.txt`（对 249 上真实游戏程序集 dump）

### 2.1 `GameplayCoreSceneSetupData`（Main.dll）——**改动最大**
旧（Shaffuru）：有 `difficultyBeatmap`、`environmentInfo`、`mainSettingsModel`
新（1.44.1）字段：
```
BeatmapKey beatmapKey;  BeatmapBasicData beatmapBasicData;  BeatmapLevel beatmapLevel;
GameplayModifiers gameplayModifiers;  PlayerSpecificSettings playerSpecificSettings;
PracticeSettings practiceSettings;  EnvironmentInfoSO targetEnvironmentInfo;
EnvironmentInfoSO originalEnvironmentInfo;  ColorScheme colorScheme;
Nullable`1 recordingToolData;  EnvironmentsListModel environmentsListModel;
AudioClip <songAudioClip>k__BackingField;  IBeatmapLevelData <beatmapLevelData>k__BackingField;
IReadonlyBeatmapData <transformedBeatmapData>k__BackingField;
SettingsManager _settingsManager;  BeatmapLevelsModel _beatmapLevelsModel;
BeatmapLevelsEntitlementModel _beatmapLevelsEntitlementModel;
AudioClipAsyncLoader _audioClipAsyncLoader;  BeatmapDataLoader _beatmapDataLoader;
Boolean _allowNullBeatmapLevelData;  Boolean _enableBeatmapDataCaching;
```
- ❌ 没有 `difficultyBeatmap`（→ 用 `beatmapKey`/`beatmapLevel`/`beatmapBasicData`）
- ❌ 没有 `mainSettingsModel`
- ✅ `playerSpecificSettings.leftHanded` / `.environmentEffectsFilterDefaultPreset` / `.environmentEffectsFilterExpertPlusPreset` 都在
- ✅ `gameplayModifiers.songSpeedMul` 在
- 🎁 `transformedBeatmapData` 直接躺在 setupData 上（当前关卡的已变换数据）

### 2.2 `BeatmapObjectSpawnController`（Main.dll）
```
field BeatmapObjectSpawnMovementData _beatmapObjectSpawnMovementData;   // 注意下划线
field BeatmapCallbacksController _beatmapCallbacksController;
field IJumpOffsetYProvider _jumpOffsetYProvider;  field IReadonlyBeatmapData _beatmapData;
field Boolean _disableSpawning;   prop BeatmapObjectSpawnMovementData beatmapObjectSpawnMovementData;
prop Int32 noteLinesCount;  prop Single noteLinesDistance;  prop Single verticalLayerDistance;
```
✅ `beatmapObjectSpawnMovementData` 属性在、`_disableSpawning` 在

### 2.3 🔴 `BeatmapObjectSpawnMovementData.Init()` **签名变了**
- 旧：`Init(noteLinesCount, njs, bpm, noteJumpValueType, noteJumpValue, jumpOffsetYProvider, rightVec, forwardVec)`
- 新：**`void Init(Int32 noteLinesCount, IJumpOffsetYProvider jumpOffsetYProvider, Vector3 rightVec)`**
  → njs/bpm/noteJumpValue **不再从这个方法进**！移植时要找新的设置途径（候选：`IVariableMovementDataProvider`、`BeatmapBasicData`、或 `_variableMovementDataProvider`/`NoteJumpValue` 相关成员）。**这是移植时第一个要解决的坑。**
- 字段/props：`_noteLinesCount`、`_jumpOffsetYProvider`、`_rightVec`、`noteLinesCount`、`noteLinesDistance`、`verticalLayersDistance`、`startHalfJumpDurationInBeats`、`maxHalfJumpDistance`
- 待查：`spawnAheadTime`、`GetJumpingNoteSpawnData(NoteData)`

### 2.4 `BeatmapCallbacksController` / `CallbacksInTime` / `BeatmapDataCallbackWrapper`（Main.dll）——**基本没变** ✓
```
CallbacksController: field Dictionary`2 _callbacksInTimes;  field IReadonlyBeatmapData _beatmapData;
                     method BeatmapDataCallbackWrapper AddBeatmapCallback(Single aheadTime, BeatmapDataCallback`1)
CallbacksInTime: field LinkedListNode`1 lastProcessedNode;  field Single aheadTime;
                 field Dictionary`2 _callbacks;  field Dictionary`2 _callbacksWithSubtypeIdentifier
Wrapper: field Single aheadTime;  field Type BasicBeatmapEventType;  field Int32[] subtypeIdentifiers
```

### 2.5 `GameEnergyCounter`（Main.dll）✅
- `prop Boolean noFail`（backing `<noFail>k__BackingField`）→ **失败不结束 = `noFail = true`** ✓
- 还有 `instaFail`、`failOnSaberClash`、`gameEnergyDidReach0Event`

### 2.6 `AudioTimeSyncController`（Main.dll）✅
- `field AudioSource _audioSource` ✓
- `prop Single songTime / songLength / startSongTime / songEndTime / songTimeOffset` ✓
- `prop State state`

### 2.7 `BeatmapDataTransformHelper`（DataModels.dll）
`IReadonlyBeatmapData CreateTransformedBeatmapData(IReadonlyBeatmapData beatmapData, BeatmapLevel beatmapLevel, GameplayModifiers gameplayModifiers, Boolean leftHanded, EnvironmentEffectsFilterPreset environmentEffectsFilterPreset, EnvironmentIntensityReductionOptions environmentIntensityReductionOptions, Settings& settings)`
→ 末参是 **`Settings&`**（旧版传的是 `MainSettingsModelSO`）→ 要从 `SettingsManager` 拿 `Settings`

### 2.8 `BeatmapLevelsModel`（DataModels.dll）——**取关卡的方式变了**
- ❌ 没看到 `_loadedPreviewBeatmapLevels`
- ✅ 可用的：`GetBeatmapLevel(String levelId, Boolean ignoreCase)`、`LoadBeatmapLevelDataAsync(String levelID, BeatmapLevelDataVersion, CancellationToken)`、`ReloadCustomLevelPackCollectionAsync(CancellationToken)`、`GetLevelPackForLevelId`、`ClearLoadedBeatmapLevelsCaches`、`ReloadAllBeatmapLevelPacks`
- 旧 `GetBeatmapLevelAsync(levelId, ct)` → 改用上面两个组合

### 2.9 其它
- `BeatmapDataItem`：`<time>k__BackingField` ✓ 不变；`BeatmapObjectData` → `SliderData.<tailTime>k__BackingField` ✓ 在
- `GameplayModifiers.songSpeedMul` ✓
- `BeatmapCharacteristicSO.serializedName` ✓（判断 Standard 用这个）

### 2.10 ❓还没 dump 到的（明天补）
`IDifficultyBeatmap`（BeatmapCore）、`IReadonlyBeatmapData`（成员：`allBeatmapDataItems`/`numberOfLines`）、`BeatmapKey`、`BeatmapBasicData`、`IBeatmapLevelData`、`BeatmapLevel`、`SettingsManager`/`Settings`、`PauseMenuManager.InitData`（嵌套类没抓到）、`BeatmapObjectSpawnMovementData` 全部成员、`BeatmapObjectManager._allBeatmapObjects`、**`SongCore.dll` / `BeatSaberPlaylistsLib.dll`（在 Plugins/，要加进扫描目录）**

---

## 3. 移植顺序（明天照着做）
1. **补 dump**（把 §2.10 的类型 + Plugins 目录加进去，重跑 §4 的命令）
2. 先移植"稳定的三个"：`AudioTimeWrapper` → `CustomAudioSource` → `BeatmapObjectDissolver`（API 基本没变）
3. 攻克 §2.3 的 `Init()` 新签名 + §2.8 的关卡加载方式 → 写 `BeatmapLoader`
4. 移植 `BeatmapSwitcher`（把 §2.1/2.2/2.4 的差异全部对进去）
5. 队列：`SetlistQueue`（读 `ChallengeSet` → 顺序/随机） + `QueueProcessor`（Tick 判断换谱时机）
6. NoFail：`gameEnergyCounter.noFail = true`（`settings.json` 的 `FailEndsRun=false` 时）
7. UI：主菜单按钮（BSML + SiraUtil `Zenjector`）→ 挑战列表 / 设置页 / 进度 HUD
8. 游戏内联调（要the requester开游戏）→ 再回到工具侧做编辑器

## 4. 构建 / 工具
```bat
:: 249 上构建 mod（源码：C:\Users\98097\setlist-build\SetlistChallenge）
set G=<BeatSaberDir>
C:\Users\98097\dotnet8\dotnet.exe build -c Release -p:BeatSaberDir="%G%"
```
- SDK：`C:\Users\98097\dotnet8\dotnet.exe`（.NET 8，用户级装，见 `get_sdk.cmd`）
- 构建机命令行脚本：`C:\Users\98097\build_setlist.cmd`（构建 SetlistChallenge）、`run_apidump.cmd`（dump API）
- **API dump 工具**：`workspace/beatsaber-mod-setlist/apidump/`（Cecil 读程序集，不运行游戏）
  ```
  dotnet run -c Release -- "%G%\Beat Saber_Data\Managed" "%G%\Libs" out.txt <类型名...>
  ```
  ⚠️ `Libs` 只扫了游戏库，**Plugins/*.dll（SongCore 等）要单独加目录**才能 dump 到
- mod 清单/依赖用仓库自带的 `manifest.json`（`LogicalName=SetlistChallenge.manifest.json` 内嵌）✓

## 5. 配置格式（工具写 / 游戏读）
`UserData/SetlistChallenge/<挑战名>.json`：
```json
{ "title": "周末挑战", "mode": "setlist", "failEndsRun": false, "skipResults": true, "shuffle": false,
  "entries": [ { "folder": "13b80 (Emir - Hitchcock)", "characteristic": "Standard", "difficulty": "ExpertPlus" } ] }
```
`UserData/SetlistChallenge/settings.json`（游戏内设置，也可由工具写）：
```json
{ "FailEndsRun": false, "SkipResults": true, "ShowProgressHud": true,
  "TransitionReactionTimeMs": 500, "TransitionGraceMs": 800 }
```
随机模式：`"mode": "random"` + `"filter": { "difficulty": "...", "count": 10, "durationMinutes": 0, "minNps": ..., "maxNps": ..., "playlist": "...", "requireNoodle": false, "requireChroma": false }`

## 6. 风险 / 注意
- 换谱会**破坏成绩提交**（Shaffuru 明说 not gonna happen；ScoreSaber/BeatLeader 在这种模式里会乱）→ 挑战模式里应禁用提交
- 有些 mod 在这种"换谱"场景会崩（Shaffuru 提到 PlayFirst、Replay 之类）
- `Mapping Extensions` 的图 Shaffuru 支持，其它 requirement（Noodle/Chroma）它不支持 —— 我们已装 Heck/Chroma/Noodle，可以试着支持
- 移植时所有私有字段访问都用 `AccessTools.Field(...)` + `IPA.Utilities.FieldAccessor`（BSIPA 自带），字段名以 dump 为准

---

## 7. 🌙 今晚（2026-09-28 03:30）进展：换谱核心已移植并编译通过 ✅

### 已完成
- **API dump 工具有了**：`apidump/`（Mono.Cecil 读程序集，不跑游戏）
  - dump 产物（都存本目录）：`api-dump-1.44.1.txt`（第一轮）、`-2.txt`、`-3.txt`、`-4.txt`
  - 运行方式：见 §4；**支持分号分隔多个目录**（Managed;Libs;Plugins）
- **GameLogic 换谱核心已写出并编译通过**（`EXIT=0`，2 个无害警告）：
  | 文件 | 说明 | 1.44.1 适配要点 |
  |---|---|---|
  | `GameLogic/AudioTimeWrapper.cs` | 包 AudioTimeSyncController + `_audioSource` | `songLength` 改从 `sceneSetupData.songAudioClip.length` 取 |
  | `GameLogic/CustomAudioSource.cs` | 自挂 AudioSource 播下一首 | `gameplayModifiers.songSpeedMul` ✓；`_failAudioPitchGainEffect` ✓；**音频延迟暂填 0（TODO）** |
  | `GameLogic/BeatmapObjectDissolver.cs` | 溶解场上物件 | 私有 `_allBeatmapObjects` + 元素 `Dissolve(float)` 都走**反射**（元素类型名未 dump 到） |
  | `GameLogic/BeatmapLoader.cs` | 取关卡/等级数据/变换谱面 | `LoadBeatmapLevelDataAsync` 返回 **`LoadBeatmapLevelDataResult`**（`.beatmapLevelData`/`.isError`）；`BeatmapDataLoader` 从 `sceneSetupData._beatmapDataLoader` 反射取；`CreateTransformedBeatmapData(..., ref Settings)`，Settings 从 `SettingsManager.settings` 反射取 |
  | `GameLogic/BeatmapSwitcher.cs` | **换谱核心** | NJS/BPM 改走 `IVariableMovementDataProvider.Init(startHalfJump, maxHalfJump, njs, 0f, bpm, njvType, njvValue, centerPos, forward)`（`GetNoteJumpValues` 取 njv）；`spawnMovementData.Init(noteLinesCount, jumpOffsetYProvider, rightVec)`；`spawnAheadTime` 从 provider 取；`allBeatmapDataItems` ✓；`time`/`tailTime`/`aheadTime` 三个 `FieldAccessor` 名字仍有效 ✓ |
  | `GameLogic/Refl.cs` | 私有成员集中处 | `_callbacksInTimes` / `_callbacks` / `_allBeatmapObjects` / `_disableSpawning` / `_beatmapDataLoader` / `GameEnergyCounter.<noFail>k__BackingField` |
  | `GameLogic/CoroutineRunner.cs` | 自己跑协程 | **1.44.1 已经没有 `SharedCoroutineStarter`** |
- 必需的额外引用（csproj，除游戏主体外）：`Zenject.dll`、`Zenject-usage.dll`、`BeatSaber.GameSettings.dll`、`BeatSaber.Settings.dll`、`AdditionalContentModel.Interfaces.dll`、`BGLib.AppFlow.dll`、`BGLib.UnityExtension.dll`、`MediaLoader.dll`

### ⬜ 明天从这里继续
1. **Zenject 安装器**（照 Shaffuru `Installers/GameInstaller.cs`）：只在"挑战自造关卡"里绑定
   `AudioTimeWrapper` / `CustomAudioSource` / `BeatmapObjectDissolver` / `BeatmapSwitcher` / 队列 + `UnifiedATSC`
   ⚠️ 还需要：一个"入口"关卡（Shaffuru 用假 levelID 前缀判断）→ 我们得决定怎么进入挑战（主菜单按钮直接开局？还是造一个占位关卡）
2. **队列 + 换谱触发**（照 `QueueProcessor`）：Tick 判断"到换谱时间了" → 选下一首 → 异步加载 → `SwitchToBeatmap(...)`；衔接点：`songTime` 到 `songLength - 某值` 时换，或用每首配置的时长
3. **难度/关卡解析**：配置里写的是 `folder` → 需要用 **SongCore** 映射到 levelId（`SongCore.Loader.CustomLevelsById` / `SongCore.Collections.hashForLevelID`；还差 `CustomPreviewBeatmapLevel` 的路径字段名没 dump）
4. **失败语义**：`FailEndsRun=false` → `Refl.SetNoFail(energy, true)`（已在 BeatmapSwitcher 里留了 `SetNoFail`）+ 换谱后宽限无敌
5. **UI**：主菜单按钮（BSML + SiraUtil `Zenjector`）、挑战列表、设置页、进度 HUD
6. **随机模式**（筛选：难度/NPS/Noodle/Chroma/歌单/时长）→ 复用 Shaffuru `MapPool.cs`（314 行）思路
7. 装进游戏 + 联调（要the requester开游戏）；之后再做**视频工具侧的挑战编辑器**

### ⚠️ 已知坑/风险（明天注意）
- `GameplayCoreSceneSetupData` 没有 `difficultyBeatmap` 了：新代码里没有"IDifficultyBeatmap"这个概念，用的是 `BeatmapKey` + `BeatmapLevel` + `BeatmapBasicData`
- `noFail` 属性是只读 → 必须写 backing field（`Refl.SetNoFail`）
- 换谱会破坏成绩提交 → 挑战模式里考虑禁用
- `Object.FindObjectsOfType` 在 Unity 6 已弃用（警告，暂不处理）

---

## 8. 🌙 今晚续（2026-09-28 04:00）：mod 装进游戏 + 挑战编辑器完成 ✅

### 状态
- **mod 完整编译通过**（`EXIT=0`）并**已装进游戏**：`Plugins\SetlistChallenge.dll`（49152B，内嵌 manifest + BSML 都在）
- **测试挑战已写好**：`UserData\SetlistChallenge\测试连打.json`（第1首 `30325 (39 Music! - Skeelie)` Expert；第2首 `50bf3 (Bubble - Emiru)` ExpertPlus）
- **工具侧 v1.18-web 已部署 249**：新增「🎬 无缝挑战」页 = 挑战编辑器（新建/编辑/删除/复制、setlist/random 两模式、每首难度下拉、↑↓排序、游戏内设置）
  - 接口：`/api/challenges`（列表/详情/保存/删除/设置/get/set/openfolder）；`/api/songs` 现在带 `levelId`（v2/v3 谱面 = `custom_level_` + 大写哈希 ✓ 已用 9 个真实谱面验证 7 个命中，v4 暂空）
  - 备份：`server.js.bak_20260928c`、`public\index.html.bak_20260928c`

### mod 结构（今晚新加）
| 文件 | 作用 |
|---|---|
| `GameLogic/ChallengeRun.cs` | 运行时状态（队列/进度/失败语义）+ `LevelResolver`（entry→levelId/BeatmapKey/BeatmapLevel/levelData）+ `RandomPicker`（从自定义歌随机抽，暂时只按数量） |
| `GameLogic/ChallengeStarter.cs` | 开局：`MenuTransitionsHelper.StartStandardLevel(...)` 开第 1 首；`levelFinished` 回调里**兜底**接着开下一首 |
| `GameLogic/QueueProcessor.cs` | `ITickable`：结尾前 0.65s 触发 `BeatmapSwitcher.SwitchToBeatmap(...)` 在场景内换下一首 |
| `Installers/Installers.cs` | App / Menu / GameCore 三个 Zenject 安装器（GameCore 只在 `ChallengeRun.Current != null` 时绑定，不介入普通游玩） |
| `UI/ChallengeSettingsView.cs` + `UI/settings.bsml` | 游戏内设置页（Mods 设置 → 无缝挑战）：挑战下拉 + 失败即结束/跳过结算/进度HUD 开关 + 「▶ 开始挑战」 |
| `Plugin.cs` | 改为 `Plugin(Logger, PluginMetadata, Zenjector)`，注册 3 个安装器（`Location.App/Menu/GameCore`） |

### 👉 明天第一步（联调）
1. 开游戏 → 看 `Logs\<最新>.log` 里 `SetlistChallenge` 的行：应有「插件载入」「安装器已注册」「已注册设置菜单」「就绪：N 个挑战」
2. 设置 → **Mods 设置 → 无缝挑战** → 选「测试连打」→ **▶ 开始挑战**
3. 预期：第1首（39 Music!）正常开，快结束时**场景内无缝换到第2首（Bubble）**
4. 把日志里 `[SetlistChallenge]` 的行发我（或让我直接看）

### ⚠️ 已知待修（明天优先）
1. **换谱后的音频还没接上**：1.44 的 `IBeatmapLevelData` 里没有 `audioClip`，新歌的 clip 要经 `AudioClipAsyncLoader` 取 → 现在换了谱面但音乐不会跟着换（要先把这个补上，否则"换了个寂寞"）
2. `StartStandardLevel` 的 `GameplayAdditionalInformation` 传了 `default`，可能要按实际赋值
3. 进度 HUD 开关有了但**显示还没做**
4. 随机模式筛选只做了「数量+难度」（NPS/歌单/Noodle/Chroma/时长未做）
5. 换谱会破坏成绩提交（预期，别在连打里刷分）
6. **SSH 会话里拉不起游戏**（写不出日志）→ 只能由the requester手动开游戏验证

### 构建/部署速查
```bat
:: mod（249）
set G=<BeatSaberDir>
C:\Users\98097\dotnet8\dotnet.exe build C:\Users\98097\setlist-build\SetlistChallenge\SetlistChallenge.csproj -c Release -p:BeatSaberDir="%G%"
:: 或 C:\Users\98097\build_setlist.cmd；装：copy bin\Release\SetlistChallenge.dll "%G%\Plugins\"
:: API dump：C:\Users\98097\run_apidump*.cmd（或 apidump/ 源码）
```
