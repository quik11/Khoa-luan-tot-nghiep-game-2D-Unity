using System.Collections.Generic;
using UnityEngine;

// Đọc 2 file chữ sửa tay được: Assets/Resources/CotTruyen.txt (cốt truyện + tên màn)
// và Assets/Resources/HuongDan.txt (chữ hướng dẫn trong lúc chơi). Đọc 1 lần rồi nhớ lại.
public static class LevelTexts
{
    public struct Story
    {
        public string title;
        public string body;
    }

    public struct Hint
    {
        public int level;
        public string condition;
        public string text;
    }

    private static Dictionary<int, Story> stories;
    private static List<Hint> hints;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        stories = null;
        hints = null;
    }

    // không có cốt truyện cho màn đó thì trả về false
    public static bool TryGetStory(int level, out Story story)
    {
        if (stories == null) LoadStories();
        return stories.TryGetValue(level, out story);
    }

    // tên màn cho màn hình chờ: lấy tiêu đề cốt truyện, không có thì "MÀN N"
    public static string LevelTitle(int level)
    {
        Story story;
        return TryGetStory(level, out story) && !string.IsNullOrEmpty(story.title) ? story.title : "MÀN " + level;
    }

    public static List<Hint> HintsFor(int level)
    {
        if (hints == null) LoadHints();
        List<Hint> result = new List<Hint>();
        foreach (Hint hint in hints)
        {
            if (hint.level == level) result.Add(hint);
        }
        return result;
    }

    private static string[] ReadLines(string resourceName)
    {
        TextAsset asset = Resources.Load<TextAsset>(resourceName);
        if (asset == null)
        {
            Debug.LogWarning("LevelTexts: không thấy file Assets/Resources/" + resourceName + ".txt");
            return new string[0];
        }
        return asset.text.Replace("\r", "").Split('\n');
    }

    private static void LoadStories()
    {
        stories = new Dictionary<int, Story>();
        int level = -1;
        string title = null;
        List<string> body = new List<string>();

        foreach (string raw in ReadLines("CotTruyen"))
        {
            string line = raw.Trim();
            if (line.StartsWith("//")) continue;

            if (line.StartsWith("===") && line.EndsWith("==="))
            {
                SaveStory(level, title, body);
                int.TryParse(line.Trim('=', ' '), out level);
                title = null;
                body.Clear();
                continue;
            }

            if (level < 0) continue;
            if (title == null)
            {
                if (line.Length > 0) title = line;
                continue;
            }
            body.Add(line);
        }
        SaveStory(level, title, body);
    }

    private static void SaveStory(int level, string title, List<string> body)
    {
        if (level <= 0 || title == null) return;
        // bỏ dòng trống thừa ở cuối, dòng trống giữa các đoạn giữ lại
        int end = body.Count;
        while (end > 0 && body[end - 1].Length == 0) end--;
        stories[level] = new Story { title = title, body = string.Join("\n", body.GetRange(0, end)) };
    }

    private static void LoadHints()
    {
        hints = new List<Hint>();
        foreach (string raw in ReadLines("HuongDan"))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;

            string[] parts = line.Split(new[] { '|' }, 3);
            if (parts.Length < 3) continue;

            int level;
            if (!int.TryParse(parts[0].Trim(), out level)) continue;
            hints.Add(new Hint { level = level, condition = parts[1].Trim().ToLowerInvariant(), text = parts[2].Trim() });
        }
    }
}
