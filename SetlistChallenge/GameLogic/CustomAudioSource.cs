using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;

namespace SetlistChallenge.GameLogic
{
    /// <summary>
    /// 自己挂一个 AudioSource（输出到 "Music" mixer），专门播下一首的音频。
    /// 移植自 Shaffuru（MIT, kinsi55）
    /// </summary>
    public class CustomAudioSource
    {
        private static readonly FieldInfo FieldFailAudioPitchGainEffect =
            AccessTools.Field(typeof(GameSongController), "_failAudioPitchGainEffect");

        private readonly AudioTimeWrapper _audioTimeWrapper;
        private readonly AudioSource _source;
        private readonly float _defaultPitch;
        private readonly float _audioLatency;

        public CustomAudioSource(GameplayCoreSceneSetupData setupData, AudioTimeWrapper wrapper)
        {
            _audioTimeWrapper = wrapper;

            var mixer = Object.FindObjectsOfType<AudioSource>()
                .Where(x => x.outputAudioMixerGroup != null && x.outputAudioMixerGroup.name == "Music")
                .Select(x => x.outputAudioMixerGroup)
                .FirstOrDefault();

            _source = new GameObject("SetlistChallenge_CAS", typeof(AudioSource)).GetComponent<AudioSource>();
            _source.outputAudioMixerGroup = mixer;

            _defaultPitch = setupData?.gameplayModifiers?.songSpeedMul ?? 1f;
            _audioLatency = 0f; // TODO(1.44.1): 音频延迟现在在 SettingsManager.settings.audio 里，需要时再取
        }

        public AudioSource Source => _source;

        public void SetClip(AudioClip clip)
        {
            if (clip == null || _source.clip == clip) return;
            _source.Stop();
            _source.time = 0f;
            _source.clip = clip;
        }

        private float _lastSetTime;

        public void Play(float songStart = 0f)
        {
            if (_source.clip == null) return;
            _source.time = songStart + _audioLatency;
            _lastSetTime = _source.time - _audioTimeWrapper.SongTime;
            _source.Play();
        }

        public void YoinkFailEffect(GameSongController controller)
        {
            if (controller == null) return;
            _source.pitch = _defaultPitch;
            _source.volume = 1f;
            var effect = FieldFailAudioPitchGainEffect?.GetValue(controller) as AudioPitchGainEffect;
            effect?.SetAudioSource(_source);
        }

        public void ResetFailEffect()
        {
            _source.pitch = _defaultPitch;
            _source.volume = 1f;
            Play(_lastSetTime + _audioTimeWrapper.SongTime);
        }

        public void Dispose()
        {
            try { if (_source != null) Object.Destroy(_source.gameObject); } catch { }
        }
    }
}
