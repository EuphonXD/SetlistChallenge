using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Zenject;

namespace SetlistChallenge.GameLogic
{
    /// <summary>
    /// 把 AudioTimeSyncController + 它内部的 AudioSource 包成统一接口（换谱时要 Pause/Resume/songTime/换 clip）
    /// 移植自 Shaffuru（MIT, kinsi55）
    /// </summary>
    public interface IUnifiedAudioSource
    {
        float SongTime { get; }
        float SongLength { get; }
        bool IsValid { get; }
        void Pause();
        void Resume();
        /// <summary>把游戏时钟认的"当前音轨"换成下一首（延长 songLength/songEndTime，避免游戏在第一首末尾就结束关卡）</summary>
        void ReplaceAudioClip(AudioClip clip);
    }

    public class UnifiedATSC : IUnifiedAudioSource
    {
        private static readonly FieldInfo FieldAudioSource = AccessTools.Field(typeof(AudioTimeSyncController), "_audioSource");
        private static readonly FieldInfo FieldInitData = AccessTools.Field(typeof(AudioTimeSyncController), "_initData");
        private static readonly FieldInfo FieldInitClip = AccessTools.Field(typeof(AudioTimeSyncController.InitData), "audioClip");

        private readonly AudioTimeSyncController _controller;
        private readonly AudioSource _source;

        public UnifiedATSC(AudioTimeSyncController controller, AudioTimeWrapper wrapper)
        {
            _controller = controller;
            _source = FieldAudioSource != null ? (AudioSource)FieldAudioSource.GetValue(controller) : controller.GetComponent<AudioSource>();
            wrapper.SetAudioSource(this);
        }

        public bool IsValid => _controller != null;
        public float SongTime => _controller.songTime;
        public float SongLength => _controller != null ? _controller.songLength : 0f;
        public void Pause() { if (_source != null) _source.Pause(); }
        public void Resume() { if (_source != null) _source.Play(); }

        public void ReplaceAudioClip(AudioClip clip)
        {
            if (_controller == null || clip == null) return;
            try
            {
                var init = FieldInitData?.GetValue(_controller);
                if (init != null) FieldInitClip?.SetValue(init, clip);
                // 旧音轨播到头后，让时钟纯按 dsp 继续走（否则会停/回跳）
                _controller.forcedNoAudioSync = true;
            }
            catch (Exception e) { Plugin.Log?.Warn("[SetlistChallenge] 换音频 clip 失败: " + e.Message); }
        }
    }

    public class AudioTimeWrapper
    {
        private IUnifiedAudioSource _source;

        public AudioTimeWrapper(GameplayCoreSceneSetupData sceneSetupData) { }

        public void SetAudioSource(IUnifiedAudioSource src) => _source = src;

        public bool IsValid => _source != null && _source.IsValid;
        public float SongLength => _source != null ? _source.SongLength : 0f;
        public float SongTime => _source?.SongTime ?? 0f;
        public void Pause() => _source?.Pause();
        public void Resume() => _source?.Resume();
        public void ReplaceAudioClip(AudioClip clip) => _source?.ReplaceAudioClip(clip);
    }
}
