using System;
using System.Collections.Generic;

namespace SpikingDSE;

public sealed class SpikeSourceTrace : Actor
{
    public Action<SpikeSourceTrace, long, int> SpikeSent;

    public OutPort output = new OutPort();

    private long startTime;
    private IEnumerable<int> spikeTrace;

    public SpikeSourceTrace(IEnumerable<int> spikeTrace, long startTime = 0, string name = null)
    {
        this.spikeTrace = spikeTrace;
        this.Name = name;
        this.startTime = startTime;
    }

    public override IEnumerable<Event> Run(Environment env)
    {
        yield return env.SleepUntil(startTime);
        foreach (var neuron in spikeTrace)
        {
            var spike = new ODINSpikeEvent(null, neuron);
            yield return env.Send(output, spike);
            SpikeSent?.Invoke(this, env.Now, neuron);
        }
    }
}

public sealed class SpikeSink : Actor
{
    public Action<SpikeSink, long, int> SpikeReceived;

    public InPort input = new();

    public SpikeSink(string name = null)
    {
        this.Name = name;
    }

    public override IEnumerable<Event> Run(Environment env)
    {
        while (true)
        {
            var rcv = env.Receive(input);
            yield return rcv;
            var spike = (ODINSpikeEvent)rcv.Message;
            SpikeReceived?.Invoke(this, env.Now, spike.neuron);
        }
    }
}

public class ODINSingleCore : Experiment
{
    private TraceReporter reporter;

    public override void Setup()
    {
        reporter = new TraceReporter("res/odin/result.trace");
        var input = sim.AddActor(new SpikeSourceTrace(EventTraceReader.ReadInputs("res/odin/validation.trace"), startTime: 4521));
        input.SpikeSent += (_, time, neuron) => reporter.OutputSpike(neuron, time);
        var output = sim.AddActor(new SpikeSink());
        output.SpikeReceived += (_, time, neuron) => reporter.InputSpike(neuron, time);

        var delayModel = new ODINDelayModel
        {
            InputTime = 7,
            ComputeTime = 2,
            OutputTime = 8
        };
        var core1 = sim.AddActor(new ODINCore(null, 256, delayModel, name: "odin1"));
        var weights = WeigthsUtil.ReadFromCSVInt("res/odin/weights_256.csv");
        var layer = new LIFLayer(weights, threshold: 30, refractory: false, name: "hidden");
        core1.AddLayer(layer);

        sim.AddChannel(input.output, core1.input);
        sim.AddChannel(core1.output, output.input);
    }

    public override void Cleanup()
    {
        reporter.Finish();
    }
}
