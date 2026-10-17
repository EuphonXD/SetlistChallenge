# SetlistChallenge · 连打挑战

> Beat Saber 的连打挑战 mod：按预设顺序（或筛选随机）连续玩多首曲子。
>
> ⚠️ **它不是真正的「无缝」。** 当前实现是「一首打完 → 自动按掉结算界面 → 立刻开下一首」，
> 两首之间仍会退出关卡并重新加载下一首（通常有 1~数秒的黑屏 / 加载时间）。
> 场景内实时换谱的代码保留在仓库里，但因 1.44 下不可靠而**默认关闭**，详见 [关于「无缝」](#关于无缝)。

**支持游戏版本：Beat Saber 1.44.1** ｜ mod 版本 `0.1.0`
## 配套工具

- **[beat-saber-video-tool](https://github.com/EuphonXD/beat-saber-video-tool)**（网页版）
  在浏览器里管理歌曲、补 YouTube 链接、批量下载视频，并**直接生成挑战配置**：
  写到 `UserData/SetlistChallenge/*.json`（本 mod 读取的格式），比手写 JSON 方便很多。
  它的 **🎬 无缝挑战** 页可以新建/编辑挑战、从播放列表导入、设置“失败即结束/跳过结算”。
- **[yt-pot-helper](https://github.com/EuphonXD/yt-pot-helper)**（可选）
  视频批量下载走 yt-dlp，遇到 YouTube “confirm you’re not a bot” 时用它起本地 PoToken 服务。

---


---

## 特性

1. **自动连播**：一首打完后自动跳过结算界面、直接开始下一首，不用回菜单手动选歌 —— 这是当前**唯一可用**的连播方式。
2. **NoFail 式继续**：血量归零不倒，继续把这首打完（可选「失败即结束」）。
3. **筛选随机**：从自定义曲库按条件（数量/难度）随机抽歌连打。
4. **跳过结算**：Results 界面激活后约 2 帧自动按下「继续」，所以人基本看不到它 —— 但**下一首仍要完整加载**，这不是无缝换谱。
5. **（实验性，默认关闭）场景内换谱**：代码在 QueueProcessor，SeamlessSwapEnabled = false，1.44 下未达到可用状态。

---

## 关于「无缝」

这个 mod 最早的目标是 Shaffuru 式的「关卡内直接换谱」：上一首快结束时溶解音符、改写活的谱面数据、接着播下一首，全程不退出关卡。

**这一点在 1.44 上没有做成。** 谱面数据能插进去，但游戏不会据此生成新的音符/物块 —— 结果只是音乐换了、谱面还是旧的。
所以 QueueProcessor 里的 SeamlessSwapEnabled 被设为 false，那条路径保留但不可用。

现在真正在跑的是另一条稳的流程：

1. 一首正常打完 → 游戏进入 Results（结算）界面；
2. SkipResultsPatch 在界面激活后约 2 帧自动触发「继续」，玩家几乎看不到结算；
3. StartStandardLevel 的回调拿到本首结果 → 立即 StartIndex 开下一首；
4. 下一首**重新加载整个关卡**（正常读谱、载入场景、重新起播）。

所以准确的叫法是「**自动跳过结算的连播**」，不是「无缝」：

- ✅ 不用回菜单、不用手点，歌与歌之间不需要任何人工操作
- ❌ 不是零间隔：每首之间仍有关卡加载，会有可见的停顿 / 黑屏（通常 1~数秒）
- ❌ 不是场景内换谱：音乐和谱面都是重新加载的

如果以后 1.44 的换谱问题解决，会打开 SeamlessSwapEnabled 并同步更新这一节。

---
## 安装

1. 游戏需为 **1.44.1**，已装 **BSIPA**。
2. 依赖 mod：
   - `BeatSaberMarkupLanguage ^1.12.0`
   - `SiraUtil ^3.0.0`
   - `SongCore ^3.14.0`
3. 把 `SetlistChallenge.dll` 放进：
   ```
   <Beat Saber>\Plugins\SetlistChallenge.dll
   ```

## 使用

1. 进游戏 → **设置 → Mods 设置 → 无缝挑战**
2. 选一个挑战 → 设定「失败即结束 / 跳过结算 / 进度 HUD」→ **▶ 开始挑战**

挑战定义读自 `UserData/SetlistChallenge/*.json`：

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

- `mode`：`setlist`（按 entries 顺序）｜`random`（按筛选随机）
- `failEndsRun`：`true` 失败即结束；`false` NoFail 式继续
- `skipResults`：换谱时跳过结算界面

---

## 构建

需要 **.NET SDK**（`net48` 目标）+ 游戏目录。

```bat
dotnet build SetlistChallenge\SetlistChallenge.csproj -c Release -p:BeatSaberDir="C:\Program Files (x86)\Steam\steamapps\common\Beat Saber"
:: 产物: SetlistChallengein\Release\SetlistChallenge.dll
:: 安装: 复制到 <Beat Saber>\Plugins```

`manifest.json` 里 `gameVersion` 已固定为 `1.44.1`。

---

## 目录结构

| 路径 | 说明 |
|---|---|
| `SetlistChallenge/` | mod 源码（C#） |
| `SetlistChallenge/GameLogic/` | 换谱核心：`BeatmapSwitcher` / `BeatmapObjectDissolver` / `CustomAudioSource` / `AudioTimeWrapper` / `QueueProcessor` / `ChallengeRun` / `ChallengeStarter` |
| `SetlistChallenge/Installers/` | Zenject 安装器（App / Menu / GameCore，仅挑战中生效） |
| `SetlistChallenge/UI/` | 游戏内设置页（BSML） |
| `SetlistChallenge/Configuration/` | 挑战/设置读写 |
| `SetlistChallenge/Patches/` | Harmony 补丁（跳过结算） |

---

## 已知问题

1. **（仅限实验性的场景内换谱）换谱后音频尚未接上**：1.44 的 IBeatmapLevelData 不再直接暴露 audioClip，需经 AudioClipAsyncLoader 取新歌 clip。该路径默认关闭，不影响当前的连播流程。
2. `StartStandardLevel` 的 `GameplayAdditionalInformation` 目前传 `default`。
3. 进度 HUD 开关已存在，显示尚未实现。
4. 随机模式目前仅支持「数量 + 难度」筛选。
5. 当前连播每首都走普通的 StartStandardLevel，成绩按游戏默认规则提交；若将来启用场景内换谱，成绩提交会被破坏。

---

## 致谢

- 换谱核心思路移植自 **[kinsi55/BeatSaber_Shaffuru](https://github.com/kinsi55/BeatSaber_Shaffuru)**（MIT）——感谢原作者。
- 依赖：[BSIPA](https://github.com/bsmg/BeatSaber-IPA-Reloaded)、[BSML](https://github.com/monkeymanboy/BeatSaberMarkupLanguage)、[SiraUtil](https://github.com/Auros/SiraUtil)、[SongCore](https://github.com/Kylemc1413/SongCore)。

## License

MIT（见 `LICENSE`）。移植部分遵循 Shaffuru 的 MIT 许可。
