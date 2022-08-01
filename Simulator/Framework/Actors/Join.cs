using System.Collections.Generic;

namespace SpikingDSE;

public sealed class Join : Actor
{
    public InPort in1 = new();
    public InPort in2 = new();
    public InPort in3 = new();
    public OutPort output = new();

    public override IEnumerable<Event> Run(Simulator env)
    {
        List<object> bundle = new();
        var anyInput = Any.AnyOf<object>(env, in1, in2, in3);

        while (true)
        {
            yield return anyInput.RequestRead();
            var message = anyInput.Read().Message;
            anyInput.ReleaseRead();

            bundle.Add(message);
            if (bundle.Count == 3)
            {
                yield return env.Send(output, bundle.ToArray());
                bundle.Clear();
            }
        }
    }
}