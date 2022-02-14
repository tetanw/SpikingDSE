using System.Diagnostics;

using NewSimulator;

class Program
{
    public static void Main(string[] args)
    {
        var program = args[0];
        if (program == "BufferTest1")
        {
            new BufferTest1().Run();
        }
        else if (program == "BufferTest2")
        {
            new BufferTest2().Run();
        }
        else if (program == "ResourceTest")
        {
            new ResourceTest().Run();
        }
        else if (program == "ChannelTest")
        {
            new ChannelTest().Run();
        }
        else if (program == "ProcessTest")
        {
            new ProcessTest().Run();
        }
        else if (program == "SelectTest")
        {
            new SelectTest().Run();
        }
    }
}