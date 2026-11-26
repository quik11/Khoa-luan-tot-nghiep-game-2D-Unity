using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Dùng chung cho menu chính và menu tạm dừng: chọn sẵn nút để chơi bằng bàn phím (mũi tên + Enter)
public static class MenuSelection
{
    public static void Select(GameObject button)
    {
        if (button != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(button);
        }
    }

    // bấm chuột ra chỗ trống thì EventSystem bỏ chọn nút -> mũi tên/Enter hết tác dụng; bấm phím bất kỳ thì chọn lại nút
    public static void ReselectOnKeyPress(GameObject fallbackButton)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || Keyboard.current == null || !Keyboard.current.anyKey.wasPressedThisFrame) return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy)
        {
            Select(fallbackButton);
        }
    }
}
