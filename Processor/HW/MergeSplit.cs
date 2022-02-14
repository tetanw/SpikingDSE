using System.Collections.Generic;

namespace SpikingDSE;

public class MergeSplit : Actor
{
    public InPort[] FromMesh;
    public OutPort ToMesh = new();
    public InPort FromController = new();
    public OutPort ToController = new();

    public MergeSplit(int nrInputs)
    {
        FromMesh = new InPort[nrInputs];
        for (int i = 0; i < nrInputs; i++)
        {
            FromMesh[i] = new();
        }
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        var allInports = FromMesh.Concat(new InPort[] { FromController });
        while (true)
        {
            var sel = env.Select(allInports);
            yield return sel;

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