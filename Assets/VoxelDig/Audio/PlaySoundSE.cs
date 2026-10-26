using UnityEngine;

namespace SleepingAnimals
{
    public class PlaySoundSE : MonoBehaviour
    {
        [SerializeField]
        private AudioSource audioSource;

        public void PlaySE()
        {
            audioSource.Play();
        }
    }
}
