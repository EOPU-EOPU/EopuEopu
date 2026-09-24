using System;
using System.Collections.Generic;

public sealed class StateMachine
{
    private State currentState;
    private readonly List<Transition> transitions = new();

    public State CurrentState => currentState;
    public MoveConfig CurrentConfig => currentState != null ? currentState.MoveConfig : default;

    public void SetInitialState(State state)
    {
        currentState?.Exit();
        currentState = state;
        currentState?.Enter();
    }

    public void AddTransition(State from, State to, Func<bool> condition)
    {
        transitions.Add(new Transition(from, to, condition));
    }

    public void Tick(float deltaTime)
    {
        TryTransition();
        currentState?.Tick(deltaTime);
    }

    public void FixedTick(float fixedDeltaTime)
    {
        currentState?.FixedTick(fixedDeltaTime);
    }

    public void ExitCurrentState()
    {
        currentState?.Exit();
        currentState = null;
    }

    private void TryTransition()
    {
        if (currentState == null) return;

        foreach (Transition transition in transitions)
        {
            if (transition.fromState != currentState)
            {
                continue;
            }

            if (transition.toState == null || transition.toState == currentState)
            {
                continue;
            }

            if (!transition.CanTransition())
            {
                continue;
            }

            ChangeState(transition.toState);
            break;
        }
    }

    private void ChangeState(State nextState)
    {
        if (nextState == null)
        {
            return;
        }

        if (nextState == currentState)
        {
            return;
        }

        currentState?.Exit();
        currentState = nextState;
        currentState.Enter();
    }
}
