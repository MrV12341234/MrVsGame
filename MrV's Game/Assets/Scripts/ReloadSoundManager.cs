using UnityEngine;


public class ReloadSoundManager : MonoBehaviour
{
    public AudioSource magAudioSource;
    [Space] public AudioClip magInSound;
    public AudioClip magOutSound;
    public AudioClip slideSound;

    public void PlayMagInSound()
    {
        magAudioSource.PlayOneShot(magInSound);
    }
    
    
    public void PlayMagOutSound()
    {
        magAudioSource.PlayOneShot(magOutSound);
    }
    
    
    public void PlaySlideSound()
    {
        magAudioSource.PlayOneShot(slideSound);
    }
}
