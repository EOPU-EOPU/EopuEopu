using System;
using UnityEngine;

public sealed class AliveState : State
{
    private readonly StateMachine movementStateMachine;

    private readonly IdleState idleState;
    private readonly SwimState swimState;

    public AliveState(Func<bool> hasMoveInput,Func<bool> dashPressed,float dashDuration)
    {
        movementStateMachine = new StateMachine();

        idleState = new IdleState();
        swimState = new SwimState();

        //ConfigureTransitions(hasMoveInput,dashPressed);
    }

    //private void ConfigureTransitions(
    //    Func<bool> hasMoveInput,
    //    Func<bool> dashPressed)
    //{
    //    movementStateMachine.AddTransition(
    //        idleState,
    //        swimState,
    //        hasMoveInput
    //    );

    //    movementStateMachine.AddTransition(
    //        swimState,
    //        idleState,
    //        () => !hasMoveInput()
    //    );

    //    movementStateMachine.AddTransition(
    //        idleState,
    //        dashState,
    //        dashPressed
    //    );

    //    movementStateMachine.AddTransition(
    //        swimState,
    //        dashState,
    //        dashPressed
    //    );

    //    movementStateMachine.AddTransition(
    //        dashState,
    //        swimState,
    //        () =>
    //            dashState.IsFinished &&
    //            hasMoveInput()
    //    );

    //    movementStateMachine.AddTransition(
    //        dashState,
    //        idleState,
    //        () =>
    //            dashState.IsFinished &&
    //            !hasMoveInput()
    //    );
    //}

    public override void Enter()
    {
        movementStateMachine.SetInitialState(idleState);
    }

    public override void Tick(float deltaTime)
    {
        movementStateMachine.Tick(deltaTime);
    }

    public override void FixedTick(float fixedDeltaTime)
    {
        movementStateMachine.FixedTick(fixedDeltaTime);
    }

    public override void Exit()
    {
        movementStateMachine.ExitCurrentState();
    }

    public override MoveConfig MoveConfig =>movementStateMachine.CurrentState?.MoveConfig?? default;
}
