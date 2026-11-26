using System.Collections;
using UnityEngine;

// Bảng cốt truyện đầu màn (chữ lấy từ Assets/Resources/CotTruyen.txt). Chỉ hiện lần đầu vào mỗi màn,
// lần sau vào thẳng; "Chơi mới" xóa dấu đã xem nên hiện lại. LevelStats tự gắn component này khi vào màn.
public class StoryIntro : MonoBehaviour
{
    private const string seenKeyPrefix = "BrandNEU_DaXemCotTruyen_Man";

    // chữ hướng dẫn chờ bảng cốt truyện đóng rồi mới hiện
    public static bool Finished { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Finished = false;
    }

    public static void ResetAll()
    {
        for (int level = 1; level <= GameProgress.LevelCount; level++)
        {
            PlayerPrefs.DeleteKey(seenKeyPrefix + level);
        }
    }

    private void Awake()
    {
        Finished = false;
    }

    private IEnumerator Start()
    {
        // chờ 2 khung hình cho PauseMenu, nhân vật... chuẩn bị xong (PauseMenu.Start mới tìm Player Input để khóa phím)
        yield return null;
        yield return null;

        int level = GameProgress.CurrentLevel;
        LevelTexts.Story story;
        bool seen = PlayerPrefs.GetInt(seenKeyPrefix + level, 0) == 1;

        if (seen || PauseMenu.Instance == null || PauseMenu.IsPaused || !LevelTexts.TryGetStory(level, out story))
        {
            Finished = true;
            yield break;
        }

        bool shown = PauseMenu.Instance.ShowTextPanel(story.title, story.body,
            new PauseMenu.PanelButton("BẮT ĐẦU", Close));
        if (!shown)
        {
            Finished = true;
            yield break;
        }

        PlayerPrefs.SetInt(seenKeyPrefix + level, 1);
        PlayerPrefs.Save();
    }

    private void Close()
    {
        if (PauseMenu.Instance != null) PauseMenu.Instance.CloseTextPanel();
        Finished = true;
    }
}
