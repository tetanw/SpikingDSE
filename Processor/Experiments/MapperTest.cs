using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class MapperTest
{
    public void Run()
    {
        var mapper = new Mapper2();

        const int HIDDEN = 0;
        const int INPUT = 1;
        const int OUTPUT = 2;

        var core1 = new MapCore()
        {
            Value = "core1",
            Name = "core1",
            MaxNrNeurons = 64,
            AcceptedTypes = new() { HIDDEN }
        };
        mapper.AddCore(core1);

        var core2 = new MapCore()
        {
            Value = "core2",
            Name = "core2",
            MaxNrNeurons = 64,
            AcceptedTypes = new() { HIDDEN }
        };
        mapper.AddCore(core2);

        var core3 = new MapCore()
        {
            Value = "core3",
            Name = "core3",
            MaxNrNeurons = 64,
            AcceptedTypes = new() { HIDDEN }
        };
        mapper.AddCore(core3);

        var core4 = new MapCore()
        {
            Value = "core4",
            Name = "core4",
            MaxNrNeurons = 64,
            AcceptedTypes = new() { HIDDEN }
        };
        mapper.AddCore(core4);

        var controller = new MapCore()
        {
            Value = "controller",
            Name = "controller",
            MaxNrNeurons = int.MaxValue,
            AcceptedTypes = new() { INPUT, OUTPUT }
        };
        mapper.AddCore(controller);

        var hidden1 = new MapLayer()
        {
            Value = "hidden1",
            Name = "hidden1",
            NrNeurons = 128,
            Splittable = true,
            Type = HIDDEN
        };
        mapper.AddLayer(hidden1);

        var hidden2 = new MapLayer()
        {
            Value = "hidden2",
            Name = "hidden2",
            NrNeurons = 128,
            Splittable = true,
            Type = HIDDEN
        };
        mapper.AddLayer(hidden2);

        var input = new MapLayer()
        {
            Value = "input",
            Name = "input",
            NrNeurons = 700,
            Splittable = false,
            Type = INPUT
        };
        mapper.AddLayer(input);

        var output = new MapLayer()
        {
            Value = "output",
            Name = "output",
            NrNeurons = 20,
            Splittable = false,
            Type = OUTPUT
        };
        mapper.AddLayer(output);


        Console.WriteLine("Mappings:");
        mapper.MappingFound += (layer, core, partial, start, end) =>
        {

            if (partial)
            {
                Console.WriteLine($"  {layer.Name} from {start} to {end} -> {core.Name}");
            }
            else
            {
                Console.WriteLine($"  {layer.Name} -> {core.Name}");
            }
        };
        var unmapped = mapper.Run();
        Console.WriteLine("Unmapped:");
        foreach (var layer in unmapped)
        {
            Console.WriteLine($"  {layer.Name}");
        }
    }
}