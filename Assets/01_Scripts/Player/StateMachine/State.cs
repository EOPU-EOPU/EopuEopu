using UnityEngine;

public abstract class State
{
    public virtual void Enter()
    {

    }
    public virtual void Tick(float dt)
    {

    }
    public virtual void FixedTick(float fixedDt)
    {

    }
    public virtual void Exit()
    {

    }

    public virtual MoveConfig MoveConfig => default;
}
