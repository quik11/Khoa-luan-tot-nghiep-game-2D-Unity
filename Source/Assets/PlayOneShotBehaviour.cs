using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayOneShotBehavior : StateMachineBehaviour
{
    [TenHienThi("Âm thanh")] public AudioClip soundToPlay;
    [TenHienThi("Âm lượng")] public float volume = 1f;
    [TenHienThi("Phát khi vào")] public bool playOnEnter = true;
    [TenHienThi("Phát khi ra")] public bool playOnExit = false;
    [TenHienThi("Phát sau trễ")] public bool playAfterDelay = false;

    [TenHienThi("Độ trễ")] public float playDelay = 0.25f;
    private float timeSinceEntered = 0f;
    private bool hasDelayedSoundPlayed = false;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (playOnEnter)
        {
            AudioSource.PlayClipAtPoint(soundToPlay, animator.gameObject.transform.position, volume);
        }

        timeSinceEntered = 0f;
        hasDelayedSoundPlayed = false;
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (playAfterDelay && !hasDelayedSoundPlayed)
        {
            timeSinceEntered += Time.deltaTime;

            if (timeSinceEntered > playDelay)
            {
                AudioSource.PlayClipAtPoint(soundToPlay, animator.gameObject.transform.position, volume);
                hasDelayedSoundPlayed = true;
            }
        }
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (playOnExit)
        {
            AudioSource.PlayClipAtPoint(soundToPlay, animator.gameObject.transform.position, volume);
        }
    }
}