using UnityEngine;

public sealed class IdleState : State
{
    public override MoveConfig MoveConfig => new MoveConfig
    {
        speedMultiplier = 0f,
        acclerationMultiplier = 1f,
        inputLocked = false
    };
}
