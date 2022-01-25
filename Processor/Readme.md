# Experiments
- ForkJoin: Producer -> Fanout -> Fanin -> Consumer largely useful for testing whether the select operation of the simulator worked.
- MeshTest: Tests whether the router actually works.
- PC: Simple performance test. Send constant message from producer to consumer.
- ResPerf: Similar to PC in that it tests performance. It consists of Producer -> Buffer -> Consumer. The buffer uses the resource construct of the simulator. When compared PC you can kind of see the overhead of resources.
- ToyProblem: This was a toy problem made for the purpose of showing how the simulator worked. Consisted of two producer with one output port merging into one consumer with two input ports.
- SingleOdin: Functional model of ODIN. The delay timings and the actual calculations are exact.
- XYRouterTest: Test the ProtoXYRouter which itself is an evolution of the SimpleXYRouter. The SimpleXYRouter could not deal with a core that can not receive at some times.
- ProtoMultiCoreTest: A multi-core setup of the ProtoCore. Setup consists of a mesh with one core being a controller and the other cores being actually calculating. This setup should be able to simulate SRNNs.
- ProtoMultiCoreAccuracy: Evolution of ProtoMultiCoreTest. Runs many input traces and checks the final prediction of the output layer. This prediction is then used to calculate the accuracy.

# HW
Explain all the hardware versions here

# SNN
Explain all the layers here

# Reporter
Explain all the reporters here