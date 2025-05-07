using System;
using UnityEngine;

public class MatchMusic : MonoBehaviour
{
    [SerializeField] private AudioClip theme, previousTheme;

    private void OnTriggerEnter(Collider other)
    {
        previousTheme = BackgroundMusic.instance.GetTrack();
        if (other.CompareTag("Player"))
        {
            BackgroundMusic.instance.ChangeTrack(theme);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            BackgroundMusic.instance.ChangeTrack(previousTheme);
        }
    }
}
