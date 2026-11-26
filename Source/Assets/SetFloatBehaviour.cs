using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SetFloatBehaviour : StateMachineBehaviour
{
    [TenHienThi("Tên tham số")] public string floatName;
    [TenHienThi("Đặt khi vào state")] public bool updateOnStateEnter;
    [TenHienThi("Đặt khi ra state")] public bool updateOnStateExit;
    [TenHienThi("Đặt khi vào nhóm state")] public bool updateOnStateMachineEnter;
    [TenHienThi("Đặt khi ra nhóm state")] public bool updateOnStateMachineExit;
    [TenHienThi("Giá trị khi vào")] public float valueOnEnter;
    [TenHienThi("Giá trị khi ra")] public float ValueOnExit;
    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
       if(updateOnStateEnter)
       {
           animator.SetFloat(floatName,valueOnEnter);
       }
     }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    //override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    
    //}

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if(updateOnStateExit)
        {
            animator.SetFloat(floatName,ValueOnExit);
        }
     }
    

    // OnStateMove is called right after Animator.OnAnimatorMove()
    //override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that processes and affects root motion
    //}

    // OnStateIK is called right after Animator.OnAnimatorIK()
    //override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that sets up animation IK (inverse kinematics)

    //}
    override public void OnStateMachineEnter(Animator animator, int stateMachinePathHash)
    {
        if(updateOnStateMachineEnter)
        {
            animator.SetFloat(floatName,valueOnEnter);
        }
     }
     override public void OnStateMachineExit(Animator animator, int stateMachinePathHash)
     {
        if(updateOnStateMachineExit)
        {
            animator.SetFloat(floatName,ValueOnExit);
        }
     }
}

