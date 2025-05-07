using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class BackgroundMusic : MonoBehaviour
{
    static public BackgroundMusic instance;
    private AudioSource audioSource;

    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.volume = 0f;
        StartCoroutine(Fade(true, audioSource, 2f, 0.3f));
        StartCoroutine(Fade(false, audioSource, 2f, 0.3f));
    }
    
    void Update()
    {
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
            StartCoroutine(Fade(true, audioSource, 2f, 0.3f));
            StartCoroutine(Fade(false, audioSource, 2f, 0.3f));
        }    
    }

    public IEnumerator Fade(bool fadeIn, AudioSource audioSource, float duration, float targetVolume)
    {
        if (!fadeIn)
        {
            double lengthOfSource = (double)audioSource.clip.samples / audioSource.clip.frequency;
            yield return new WaitForSecondsRealtime((float)(lengthOfSource-duration));
        }

        float time = 0f;
        float startVolume = audioSource.volume;
        while (time < duration)
        {
            string fadeSituation = fadeIn ? "FadeIn" : "FadeOut";
            time += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, time / duration);
            yield return null;
        }

        yield break;
    }

    public void ChangeTrack(AudioClip audioClip)
    {
        StopAllCoroutines();
        audioSource.clip = audioClip;
        audioSource.volume = 0f;
        StartCoroutine(Fade(true, audioSource, 2f, 0.3f));
        StartCoroutine(Fade(false, audioSource, 2f, 0.3f));
    }

    public AudioClip GetTrack()
    {
        return audioSource.clip;
    }
}
