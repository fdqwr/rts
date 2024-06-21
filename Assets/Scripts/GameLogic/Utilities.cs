using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct CalculateDistance: IJobParallelFor
{
    [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<Vector2> Position;
    [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<int> Team;
    [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<int> ID;
    [NativeDisableParallelForRestriction] public NativeArray<float> d2;
    [NativeDisableParallelForRestriction] public NativeArray<float> Distance;
    [NativeDisableParallelForRestriction][WriteOnly] public NativeArray<int> ClosestID;
    public void Execute(int _index)
    {
        d2[_index] = 999999;
        Distance[_index] = 999999;
        for (int i = 0; i < Position.Length; i++)
        {
            if (Team[i] != Team[_index] &&Team[i] != 0)
            {
                d2[_index] = (Position[i] - Position[_index]).magnitude;
                if(d2[_index] < Distance[_index])
                {
                    ClosestID[_index] = i;
                    Distance[_index] = d2[_index];
                }
            }
        }
    }
}