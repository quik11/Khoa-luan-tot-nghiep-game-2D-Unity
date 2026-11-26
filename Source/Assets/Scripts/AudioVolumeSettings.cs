using UnityEngine;

// Lưu và áp dụng âm lượng (PlayerPrefs). Tổng = mọi âm thanh trong game, Nhạc nền = chỉ AudioSource có gắn MusicVolume.
// Không đặt tên AudioSettings vì Unity đã có class trùng tên.
public static class AudioVolumeSettings
{
    private const string masterKey = "BrandNEU_AmLuongTong";
    private const string musicKey = "BrandNEU_AmLuongNhac";

    // MusicVolume nghe sự kiện này để cập nhật ngay khi người chơi bấm + / -
    public static event System.Action changed;

    public static float Master
    {
        get { return PlayerPrefs.GetFloat(masterKey, 1f); }
        set
        {
            PlayerPrefs.SetFloat(masterKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            ApplyMaster();
            changed?.Invoke();
        }
    }

    public static float Music
    {
        get { return PlayerPrefs.GetFloat(musicKey, 0.7f); }
        set
        {
            PlayerPrefs.SetFloat(musicKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            changed?.Invoke();
        }
    }

    // áp âm lượng đã lưu ngay khi mở game, trước khi scene đầu tiên chạy
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyMaster()
    {
        AudioListener.volume = Master;
    }
}
