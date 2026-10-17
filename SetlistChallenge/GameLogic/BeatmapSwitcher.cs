using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using IPA.Utilities;
using UnityEngine;
using Zenject;

namespace SetlistChallenge.GameLogic
{
    /// <summary>
    /// 换谱核心：在正在进行的游戏场景里直接替换成下一首（Shaffuru 手法，MIT）
    /// 1.44.1 适配点：
    ///  - NJS/BPM 现在走 IVariableMovementDataProvider.Init(...)，BeatmapObjectSpawnMovementData.Init 只剩
    ///    (noteLinesCount, jumpOffsetYProvider, rightVec)
    ///  - spawnAheadTime 从 IVariableMovementDataProvider 取
    ///  - 当前关卡的 beatmapData 从 GameplayCoreSceneSetupData.transformedBeatmapData 拿
    /// </summary>
    public class BeatmapSwitcher : IDisposable
    {
        private static readonly FieldAccessor<BeatmapDataItem, float>.Accessor SetTime =
            FieldAccessor<BeatmapDataItem, float>.GetAccessor("<time>k__BackingField");
        private static readonly FieldAccessor<SliderData, float>.Accessor SetTailTime =
            FieldAccessor<SliderData, float>.GetAccessor("<tailTime>k__BackingField");
        private static readonly FieldAccessor<BeatmapDataCallbackWrapper, float>.Accessor SetAheadTime =
            FieldAccessor<BeatmapDataCallbackWrapper, float>.GetAccessor("aheadTime");

        private readonly GameplayCoreSceneSetupData _sceneSetupData;
        private readonly IVariableMovementDataProvider _variableMovementDataProvider;
        private readonly IReadonlyBeatmapData _readonlyBeatmapData;
        private readonly BeatmapObjectSpawnController _spawnController;
        private readonly BeatmapCallbacksController _callbacksController;
        private readonly AudioTimeWrapper _audioTimeWrapper;
        private readonly GameEnergyCounter _energyCounter;
        private readonly IJumpOffsetYProvider _jumpOffsetYProvider;

        private readonly BeatmapObjectSpawnMovementData _movementData;
        private readonly float _startBeatmapCallbackAheadTime;

        public readonly CustomAudioSource CustomAudioSource;

        public BeatmapSwitcher(
            GameplayCoreSceneSetupData sceneSetupData,
            AudioTimeWrapper audioTimeWrapper,
            CustomAudioSource customAudioSource,
            [InjectOptional] IVariableMovementDataProvider variableMovementDataProvider,
            [InjectOptional] IReadonlyBeatmapData readonlyBeatmapData,
            [InjectOptional] BeatmapObjectSpawnController spawnController,
            [InjectOptional] BeatmapCallbacksController callbacksController,
            [InjectOptional] GameEnergyCounter energyCounter,
            [InjectOptional] IJumpOffsetYProvider jumpOffsetYProvider)
        {
            _sceneSetupData = sceneSetupData;
            _audioTimeWrapper = audioTimeWrapper;
            CustomAudioSource = customAudioSource;
            _variableMovementDataProvider = variableMovementDataProvider;
            _readonlyBeatmapData = readonlyBeatmapData ?? sceneSetupData?.transformedBeatmapData;
            _spawnController = spawnController;
            _callbacksController = callbacksController;
            _energyCounter = energyCounter;
            _jumpOffsetYProvider = jumpOffsetYProvider;

            if (_spawnController != null)
            {
                _movementData = _spawnController.beatmapObjectSpawnMovementData;
                _startBeatmapCallbackAheadTime = _variableMovementDataProvider?.spawnAheadTime
                                                 ?? (_movementData != null ? 0f : 0f);
            }
        }

        private void UpdateMovementData(float bpm, float njs, float mapOffset, int noteLinesCount)
        {
            if (_movementData == null) return;

            var startHalfJump = _movementData.startHalfJumpDurationInBeats;
            var maxHalfJump = _movementData.maxHalfJumpDistance;
            var centerPos = _movementData.centerPos;

            _movementData.Init(noteLinesCount, _jumpOffsetYProvider, Vector3.right);

            BeatmapObjectSpawnControllerHelpers.GetNoteJumpValues(
                _sceneSetupData.playerSpecificSettings, mapOffset, out var njvType, out var njvValue);

            _variableMovementDataProvider?.Init(
                startHalfJump, maxHalfJump, njs, 0f, bpm, njvType, njvValue, centerPos, Vector3.forward);

            _callbacksController?.TriggerBeatmapEvent(new BPMChangeBeatmapEventData(0f, bpm));
        }

        private void SetSpawningEnabled(bool enable)
        {
            if (_spawnController != null) Refl.DisableSpawning?.SetValue(_spawnController, !enable);
        }

        /// <summary>把当前正在玩的谱面换成下一首（曲内无缝）</summary>
        public void SwitchToBeatmap(IReadonlyBeatmapData replacementBeatmapData, float bpm, float njs, float mapOffset, int noteLinesCount, float startTime, float secondsToInsert = 0f, UnityEngine.AudioClip nextClip = null)
        {
            CoroutineRunner.Run(Run());

            IEnumerator Run()
            {
                SetSpawningEnabled(false);

                var reactionTime = Mathf.Max(0.05f, Configuration.SetlistSettings.Instance.TransitionReactionTimeMs / 1000f);
                var dissolveTime = reactionTime * 0.6f;

                BeatmapObjectDissolver.DissolveAllAndEverything(dissolveTime);
                yield return new WaitForSecondsRealtime(dissolveTime * 0.4f);

                if (!_audioTimeWrapper.IsValid) yield break;

                var currentAudioTime = _audioTimeWrapper.SongTime;

                if (_readonlyBeatmapData != null && replacementBeatmapData != null)
                    InsertBeatmapData(_readonlyBeatmapData, replacementBeatmapData, startTime, currentAudioTime, reactionTime, secondsToInsert);

                UpdateMovementData(bpm, njs, mapOffset, noteLinesCount);

                if (_callbacksController != null)
                {
                    var dict = Refl.CallbacksInTimes?.GetValue(_callbacksController) as Dictionary<float, CallbacksInTime>;
                    if (dict != null)
                    foreach (var kv in dict)
                    {
                        var v = kv.Value;
                        var ahead = v.aheadTime;
                        if (Mathf.Approximately(kv.Key, _startBeatmapCallbackAheadTime))
                        {
                            ahead = _variableMovementDataProvider?.spawnAheadTime ?? ahead;
                            var cbs = Refl.CallbacksDict?.GetValue(v) as System.Collections.IDictionary;
                            if (cbs != null)
                            {
                                foreach (var listObj in cbs.Values)
                                {
                                    var en = (listObj as System.Collections.IEnumerable)?.GetEnumerator();
                                    if (en == null) continue;
                                    while (en.MoveNext())
                                    {
                                        var cb = en.Current as BeatmapDataCallbackWrapper;
                                        if (cb == null) continue;
                                        try { SetAheadTime(ref cb) = ahead; } catch { }
                                    }
                                }
                            }
                        }
                        while (v.lastProcessedNode?.Next != null && v.lastProcessedNode.Next.Value.time < currentAudioTime)
                            v.lastProcessedNode = v.lastProcessedNode.Next;
                    }
                }

                SetSpawningEnabled(true);

                var timePre = _audioTimeWrapper.SongTime;
                yield return new WaitUntil(() => !_audioTimeWrapper.IsValid ||
                                                _audioTimeWrapper.SongTime - timePre >= Mathf.Min(reactionTime * 0.5f, Mathf.Max(startTime, 0.05f)));

                if (!_audioTimeWrapper.IsValid) yield break;

                // 换音频：不动游戏的 AudioTimeSyncController，而是自己挂的 CustomAudioSource 播下一首，
                // 用 songTime 同步（Shaffuru 思路）。
                if (nextClip != null)
                {
                    CustomAudioSource.SetClip(nextClip);
                    CustomAudioSource.Play(startTime + (_audioTimeWrapper.SongTime - timePre));
                    // 让游戏时钟也认下一首的音轨 → songLength/songEndTime 延长，游戏不会在旧音轨末尾结束关卡
                    _audioTimeWrapper.ReplaceAudioClip(nextClip);
                }
                else Plugin.Log?.Warn("[SetlistChallenge] 换谱：拿不到下一首的音频 clip，画面已换但音乐会不对");

                yield return null;
            }
        }

        /// <summary>把 replacement 的物件按相对时间插进"活的" beatmapData（Shaffuru 式 splice：从最新已处理节点之后改写，保持链表有序）</summary>
        private void InsertBeatmapData(IReadonlyBeatmapData target, IReadonlyBeatmapData replacement,
            float startTime, float currentAudioTime, float reactionTime, float secondsToInsert)
        {
            // 找"最新的已处理节点"，从它之后开始改写
            LinkedListNode<BeatmapDataItem> prevNode = null;
            try
            {
                var dict = Refl.CallbacksInTimes?.GetValue(_callbacksController) as Dictionary<float, CallbacksInTime>;
                if (dict != null)
                {
                    foreach (var kv in dict)
                    {
                        var ln = kv.Value?.lastProcessedNode;
                        if (ln == null) continue;
                        if (prevNode == null || prevNode.Value.time < ln.Value.time) prevNode = ln.Next;
                    }
                }
            }
            catch { }
            if (prevNode == null) prevNode = target.allBeatmapDataItems.First;

            var inserted = 0;
            var firstRel = float.NaN;

            foreach (var x in replacement.allBeatmapDataItems)
            {
                var relativeSongTime = x.time - startTime;
                if (secondsToInsert > 0f && relativeSongTime > secondsToInsert + 1f) break;

                if (relativeSongTime < 0f)
                {
                    if (!(x is ObstacleData wall)) continue;
                    var newLength = wall.duration + relativeSongTime - reactionTime;
                    if (newLength < 0f) continue;
                    wall.UpdateDuration(newLength);
                    relativeSongTime = reactionTime;
                }

                if (x is BeatmapObjectData && relativeSongTime < reactionTime) continue;

                var item = x;
                var newTime = relativeSongTime + currentAudioTime;

                if (item is SliderData slider)
                    SetTailTime(ref slider) = newTime + slider.tailTime - slider.time;

                SetTime(ref item) = newTime;

                if (inserted == 0) firstRel = relativeSongTime;
                inserted++;

                // 复用旧节点（有序），没得复用就追加
                if (prevNode != null) prevNode.Value = item;
                else prevNode = target.allBeatmapDataItems.AddLast(item);
                prevNode = prevNode.Next;
            }

            // 剩下的旧物件（已用不到）推到很远，回调不会再放出来
            for (; prevNode != null; prevNode = prevNode.Next)
            {
                var iv = prevNode.Value;
                if (iv != null) SetTime(ref iv) = float.MaxValue;
            }

            try { Plugin.Log?.Info($"[SetlistChallenge] 插入诊断：repl={replacement.allBeatmapDataItems.Count} inserted={inserted} firstRel={firstRel} curAudio={currentAudioTime} react={reactionTime} targetCount={target.allBeatmapDataItems.Count}"); } catch { }
        }

        public void SetNoFail(bool noFail)
        {
            Refl.SetNoFail(_energyCounter, noFail);
        }

        public void Dispose() { }
    }
}
