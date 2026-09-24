using UnityEngine;

public sealed class SwimState : State
{
    public override MoveConfig MoveConfig => new MoveConfig
    {
        speedMultiplier = 1f,
        acclerationMultiplier = 1f,
        inputLocked = false
    };
}
