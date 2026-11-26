using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    [TenHienThi("Chữ sát thương")] public GameObject damageTextPrefab;
    [TenHienThi("Chữ hồi máu")] public GameObject healthTextPrefab;
    [TenHienThi("Canvas")] public Canvas gameCanvas;

    [Header("Thoát màn (Esc)")]
    [TenHienThi("Màn thoát")] public string quitSceneName = "QuitScene";

    private void Awake()
    {
        gameCanvas = FindGameCanvas();
    }

    // Lấy Canvas gốc đầu tiên trong chính scene này, không thuộc GameHUD (HUD có Canvas riêng, lấy nhầm thì số sát thương
    // bị menu tạm dừng che). Phải lọc theo scene: lúc vào màn qua màn hình chờ, Canvas của màn hình chờ (nằm ngoài scene,
    // tự xóa sau khi mờ hết) cũng là Canvas gốc. Lấy nhầm nó thì sau đó mỗi đòn trúng đều báo lỗi giữa chừng
    // -> tơ đạn xuyên qua quái, không đếm được phát trói (lỗi lúc có lúc không vì Unity trả Canvas theo thứ tự bất kỳ).
    private Canvas FindGameCanvas()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.isRootCanvas && canvas.gameObject.scene == gameObject.scene && canvas.GetComponent<GameHUD>() == null)
            {
                return canvas;
            }
        }
        return null;
    }

    // số bay chỉ để trang trí: thiếu Canvas/camera thì bỏ qua, tuyệt đối không được báo lỗi
    // (hàm này chạy ngay giữa lúc tính đòn trúng, lỗi ở đây làm hỏng luôn phần trói/tơ đạn phía sau)
    private bool CanShowText()
    {
        if (gameCanvas == null) gameCanvas = FindGameCanvas();
        return gameCanvas != null && Camera.main != null;
    }

    private void OnEnable()
    {
        CharacterEvents.characterDamaged += CharacterTookDamage;
        CharacterEvents.characterHealed += CharacterHealed;
    }

    private void OnDisable()
    {
        CharacterEvents.characterDamaged -= CharacterTookDamage;
        CharacterEvents.characterHealed -= CharacterHealed;
    }

    public void CharacterTookDamage(GameObject character, int damageReceived)
    {
        if (!CanShowText() || damageTextPrefab == null || character == null) return;

        Vector3 spawnPosition = Camera.main.WorldToScreenPoint(character.transform.position);
        TMP_Text tmpText = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity, gameCanvas.transform).GetComponent<TMP_Text>();
        tmpText.text = damageReceived.ToString();
    }

    public void CharacterHealed(GameObject character, int healthRestored)
    {
        if (!CanShowText() || healthTextPrefab == null || character == null) return;

        Vector3 spawnPosition = Camera.main.WorldToScreenPoint(character.transform.position);
        TMP_Text tmpText = Instantiate(healthTextPrefab, spawnPosition, Quaternion.identity, gameCanvas.transform).GetComponent<TMP_Text>();
        tmpText.text = healthRestored.ToString();
    }

    // Đã nối sẵn trong UIManager.prefab: Player Input (map UI) -> Events -> Escape -> OnExitGame
    public void OnExitGame(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            // màn chơi có GameHUD thì Esc mở/đóng menu tạm dừng, không có thì sang QuitScene như cũ
            if (!PauseMenu.HandleEscape())
            {
                ExitToQuitScene();
            }
        }
    }

    // Esc ở scene không có menu tạm dừng -> sang QuitScene (màn hình hỏi thoát game hay về menu)
    private void ExitToQuitScene()
    {
        #if (UNITY_EDITOR || DEVELOPMENT_BUILD)
        Debug.Log(this.name + " : " + this.GetType() + " : " + System.Reflection.MethodBase.GetCurrentMethod().Name);
        #endif

        if (!Application.CanStreamedLevelBeLoaded(quitSceneName))
        {
            Debug.LogWarning("UIManager: không tìm thấy scene '" + quitSceneName + "'. Vào File > Build Profiles > Scene List, kéo scene này vào danh sách rồi thử lại.");
            return;
        }

        Time.timeScale = 1f; // lỡ bấm Esc đúng lúc đang khựng hình khi trúng đòn (timeScale = 0) thì trả lại bình thường
        UnityEngine.SceneManagement.SceneManager.LoadScene(quitSceneName);
    }
}