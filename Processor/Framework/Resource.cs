using System.Collections.Generic;

namespace SpikingDSE;

public sealed class Resource
{
    public List<(ResReqEvent, Process)> Waiting;
    public int Amount;
}