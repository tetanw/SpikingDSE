namespace NewSimulator;

public class Port
{
    public Channel? Channel;
    public bool IsBound;
    public string Name { get; set; } = "";

    public override string ToString()
    {
        return Name;
    }
}

public class InPort : Port
{

}

public class OutPort : Port
{

}
