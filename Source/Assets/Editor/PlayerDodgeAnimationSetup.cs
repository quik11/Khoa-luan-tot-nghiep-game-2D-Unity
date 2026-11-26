using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tự gắn animation lộn né (ảnh Assets/Art/NEUNEU/dodge/neudodge.png, 7 khung 70x60) vào Animator của nhân vật:
// tạo clip player_dodge, thêm state player_dodge ở Base Layer, mũi tên Any State -> player_dodge (trigger dodge + còn sống),
// hết clip thì về GroundStates (đang đứng đất) hoặc AirStates (lộn ra khỏi mép vực).
// Mở Unity là tự chạy 1 lần (Animator chưa có state player_dodge mới làm). Muốn làm lại: menu Brand NEU day > Gắn animation lộn né.
[InitializeOnLoad]
public static class PlayerDodgeAnimationSetup
{
    private const string sheetPath = "Assets/Art/NEUNEU/dodge/neudodge.png";
    private const string clipPath = "Assets/Characters/Player/player_dodge.anim";
    private const string controllerPath = "Assets/Characters/Player/AC_Player.controller";
    private const string stateName = "player_dodge";

    // tên tham số Animator, phải trùng AnimationStrings (lớp đó là internal của phần game nên script Editor không đọc được)
    private const string dodgeParam = "dodge";
    private const string isAliveParam = "isAlive";
    private const string isGroundedParam = "isGrounded";

    // 7 khung trong 0.25 giây = đúng thời gian lộn né mặc định (dodgeDuration trên Player)
    private const float clipFrameRate = 28f;

    static PlayerDodgeAnimationSetup()
    {
        // chờ Unity nạp xong asset rồi mới làm, đang Play thì thôi
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller != null && FindState(controller.layers[0].stateMachine, stateName) == null)
            {
                Setup();
            }
        };
    }

    [MenuItem("Brand NEU day/Gắn animation lộn né vào Animator nhân vật")]
    public static void Setup()
    {
        Sprite[] sprites = LoadSprites();
        if (sprites.Length == 0)
        {
            Debug.LogError("Gắn lộn né: không thấy khung hình nào trong " + sheetPath + " (ảnh phải được cắt thành nhiều sprite).");
            return;
        }

        AnimationClip clip = CreateOrUpdateClip(sprites);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError("Gắn lộn né: không thấy " + controllerPath);
            return;
        }

        AddParameterIfMissing(controller, dodgeParam, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorStateMachine groundStates = FindChildMachine(root, "GroundStates");
        AnimatorStateMachine airStates = FindChildMachine(root, "AirStates");
        if (groundStates == null || airStates == null)
        {
            Debug.LogError("Gắn lộn né: Animator thiếu nhóm GroundStates hoặc AirStates - không biết lộn xong thì về đâu.");
            return;
        }

        AnimatorState dodge = FindState(root, stateName);
        if (dodge == null)
        {
            dodge = root.AddState(stateName, new Vector3(250f, -150f, 0f));
        }
        dodge.motion = clip;
        dodge.writeDefaultValues = true;
        dodge.speed = 1f;

        // Any State -> lộn né: bấm Shift (trigger dodge) và còn sống. Mũi tên trúng đòn/chết có sẵn nằm trên nên vẫn ưu tiên hơn
        bool hasEntry = false;
        foreach (AnimatorStateTransition transition in root.anyStateTransitions)
        {
            if (transition.destinationState == dodge) hasEntry = true;
        }
        if (!hasEntry)
        {
            AnimatorStateTransition enter = root.AddAnyStateTransition(dodge);
            enter.AddCondition(AnimatorConditionMode.If, 0f, dodgeParam);
            enter.AddCondition(AnimatorConditionMode.If, 0f, isAliveParam);
            enter.hasExitTime = false;
            enter.duration = 0f;
            enter.canTransitionToSelf = false;
        }

        // lộn xong (hết clip) -> về nhóm dưới đất hoặc trên không. Xóa mũi tên ra cũ rồi tạo lại cho chắc đúng điều kiện
        foreach (AnimatorStateTransition old in dodge.transitions)
        {
            dodge.RemoveTransition(old);
        }
        AddExit(dodge, groundStates, AnimatorConditionMode.If);
        AddExit(dodge, airStates, AnimatorConditionMode.IfNot);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Gắn lộn né: xong - clip " + clipPath + " (" + sprites.Length + " khung, " + (sprites.Length / clipFrameRate).ToString("0.00")
                  + " giây), state player_dodge trong " + controllerPath + ".");
    }

    private static void AddExit(AnimatorState from, AnimatorStateMachine to, AnimatorConditionMode groundedMode)
    {
        AnimatorStateTransition exit = from.AddTransition(to);
        exit.AddCondition(groundedMode, 0f, isGroundedParam);
        exit.hasExitTime = true;
        exit.exitTime = 1f; // chạy hết clip mới ra
        exit.duration = 0f;
    }

    private static Sprite[] LoadSprites()
    {
        List<Sprite> sprites = new List<Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null) sprites.Add(sprite);
        }
        sprites.Sort((a, b) => a.rect.x.CompareTo(b.rect.x)); // trái sang phải đúng thứ tự khung
        return sprites.ToArray();
    }

    private static AnimationClip CreateOrUpdateClip(Sprite[] sprites)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        bool isNew = clip == null;
        if (isNew) clip = new AnimationClip();

        clip.frameRate = clipFrameRate;

        // thêm 1 khóa cuối lặp lại khung cuối để khung cuối cũng được hiện đủ 1/28 giây
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe { time = i / clipFrameRate, value = sprites[i] };
        }
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / clipFrameRate, value = sprites[sprites.Length - 1] };

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false; // lộn 1 lần rồi thôi
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNew) AssetDatabase.CreateAsset(clip, clipPath);
        else EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void AddParameterIfMissing(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == name) return;
        }
        controller.AddParameter(name, type);
    }

    private static AnimatorStateMachine FindChildMachine(AnimatorStateMachine root, string name)
    {
        foreach (ChildAnimatorStateMachine child in root.stateMachines)
        {
            if (child.stateMachine.name == name) return child.stateMachine;
        }
        return null;
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name == name) return child.state;
        }
        return null;
    }
}
