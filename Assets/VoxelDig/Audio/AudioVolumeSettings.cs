using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

namespace SleepingAnimals
{
    /// <summary>
    /// 音量設定
    /// </summary>
    public class AudioVolumeSettings : MonoBehaviour
    {
        //Audioミキサーを入れる
        [SerializeField] AudioMixer audioMixer;

        //それぞれのスライダーを入れる
        [SerializeField] Slider BGMSlider;
        [SerializeField] Slider SESlider;

        private void Start()
        {
            //ミキサーのvolumeにスライダーのvolumeを入れる
            //BGM
            audioMixer.GetFloat("BGM", out float bgmVolume);
            BGMSlider.value = bgmVolume;
            //SE
            audioMixer.GetFloat("SE", out float seVolume);
            SESlider.value = seVolume;
        }

        public void SetBGM(float volume)
        {
            audioMixer.SetFloat("BGM", volume);
        }

        public void SetSE(float volume)
        {
            audioMixer.SetFloat("SE", volume);
        }
    }
}
