using UnityEngine;

// Gắn vào object phát nhạc nền: âm lượng thật = âm lượng đặt sẵn trên AudioSource x mức "Nhạc nền" trong Cài đặt
[RequireComponent(typeof(AudioSource))]
public class MusicVolume : MonoBehaviour
{
    private AudioSource source;
    private float baseVolume;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume; // nhớ mức gốc đã chỉnh trong Inspector
    }

    private void OnEnable()
    {
        AudioVolumeSettings.changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        AudioVolumeSettings.changed -= Apply;
    }

    private void Apply()
    {
        if (source != null)
        {
            source.volume = baseVolume * AudioVolumeSettings.Music;
        }
    }
}
