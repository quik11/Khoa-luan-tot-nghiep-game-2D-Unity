using System;

// Gắn vào biến để Inspector hiện tên tiếng Việt thay cho tên biến tiếng Anh.
// Chỉ đổi chữ hiển thị, tên biến giữ nguyên nên số đã chỉnh trong scene/prefab không mất.
// Phần vẽ nằm ở Assets/Editor/TenHienThiEditor.cs
[AttributeUsage(AttributeTargets.Field)]
public class TenHienThiAttribute : Attribute
{
    public readonly string ten;

    public TenHienThiAttribute(string ten)
    {
        this.ten = ten;
    }
}
