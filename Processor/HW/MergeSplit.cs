using System.Collections.Generic;

namespace SpikingDSE;

public class MergeSplit : Actor
{
    public InPort[] FromMesh;
    public OutPort ToMesh = new();
    public InPort FromController = new();
    public OutPort ToController = new();

    public MergeSplit(int nrInputs, string name)
    {
        FromMesh = new InPort[nrInputs];
        for (int i = 0; i < nrInputs; i++)
        {
            FromMesh[i] = new();
        }
        Name = name;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        var allInputPorts = FromMesh.Concat(new InPort[] { FromController });
        var anyInputPort = Any.AnyOf<object>(env, allInputPorts);

        while (true)
        {
            yield return anyInputPort.RequestRead();
            var sel = anyInputPort.Read();
            anyInputPort.ReleaseRead();

            if (sel.Port == FromController)
            {
                yield return env.Send(ToMesh, sel.Message);
            }
            else
            {
                yield return env.Send(ToController, sel.Message);
            }
        }
    }
}