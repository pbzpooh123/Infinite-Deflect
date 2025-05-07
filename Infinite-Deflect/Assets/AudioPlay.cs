using UnityEngine;

public class AudioPlay : MonoBehaviour
{
    [SerializeField] private AudioClip swingSFX;
    [SerializeField] private AudioClip jumpSFX;
    [SerializeField] private AudioClip[] runGrassSFX;
    
    AudioSource audioSource;
    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Swing()
    {
        audioSource.PlayOneShot(swingSFX);
    }

    private void Jump()
    {
        audioSource.PlayOneShot(jumpSFX, 0.3f);
    }

    private void Run()
    {
        AudioClip clip = GotRandomAudioClip();
        audioSource.PlayOneShot(clip, 0.3f);
    }

    private AudioClip GotRandomAudioClip()
    {
        return runGrassSFX[UnityEngine.Random.Range(0,runGrassSFX.Length)];
    }
    
}
