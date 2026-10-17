# SetlistChallenge · 无缝连打挑战

> Beat Saber 的「无缝挑战」mod：按预设顺序（或筛选随机）连打多首曲子，**在游戏场景内直接换谱** —— 真无缝，不是打完一首跳回菜单再开下一首。

**支持游戏版本：Beat Saber 1.44.1** ｜ mod 版本 `0.1.0`

---

## 特性

1. **场景内换谱**：上一首快结束时，溶解现有音符/物块 → 改写「活的」谱面数据（新谱时间平移到当前曲目时间）→ 用挂载的 AudioSource 播下一首，全程不退出关卡。
2. **NoFail 式继续**：血量归零不倒，继续把这首打完（可选「失败即结束」）。
3. **筛选随机**：从自定义曲库按条件（数量/难度）随机抽歌连打。
4. **跳过结算**：换谱时跳过 Results 界面，衔接更顺。

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

1. **换谱后音频尚未接上**：1.44 的 `IBeatmapLevelData` 不再直接暴露 `audioClip`，需经 `AudioClipAsyncLoader` 取新歌 clip —— 不修的话「换了谱但音乐不跟着换」。
2. `StartStandardLevel` 的 `GameplayAdditionalInformation` 目前传 `default`。
3. 进度 HUD 开关已存在，显示尚未实现。
4. 随机模式目前仅支持「数量 + 难度」筛选。
5. 换谱会破坏成绩提交（预期行为，别在连打里刷分）。

---

## 致谢

- 换谱核心思路移植自 **[kinsi55/BeatSaber_Shaffuru](https://github.com/kinsi55/BeatSaber_Shaffuru)**（MIT）——感谢原作者。
- 依赖：[BSIPA](https://github.com/bsmg/BeatSaber-IPA-Reloaded)、[BSML](https://github.com/monkeymanboy/BeatSaberMarkupLanguage)、[SiraUtil](https://github.com/Auros/SiraUtil)、[SongCore](https://github.com/Kylemc1413/SongCore)。

## License

MIT（见 `LICENSE`）。移植部分遵循 Shaffuru 的 MIT 许可。
