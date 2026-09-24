using System;
using UnityEngine;

public sealed class Transition
{
    public State fromState { get; }
    public State toState { get; }

    private readonly Func<bool> condition;

    public Transition(State previousState, State nextState, Func<bool> condition)
    {
        this.fromState = previousState;
        this.toState = nextState;
        this.condition = condition;
    }
    public bool CanTransition()
    {
        return this.condition();
    }
}
