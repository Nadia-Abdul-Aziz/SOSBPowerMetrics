using System;
using System.Threading;
using LibreHardwareMonitor.Hardware;

public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);
    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("CPU Temp (Ctrl+C to stop)\n");

        Computer computer = new Computer
{
    IsCpuEnabled = true,
    // IsGpuEnabled = true,
    // IsMemoryEnabled = true,
    // IsMotherboardEnabled = true,
    // IsControllerEnabled = true,
    // IsNetworkEnabled = true,
    // IsStorageEnabled = true
};

        computer.Open();

        while (true)
        {
            computer.Accept(new UpdateVisitor());
            Console.WriteLine("Temperature:");
            foreach (var hardware in computer.Hardware)
            {
                hardware.Update();
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Name.Contains("Core"))
                    {
                        Console.WriteLine($" CPU Core: {Math.Round(sensor.Value.Value, 1)}°C");
                    }
                    //{sensor.Name} instead of text
                }
            }
            Console.WriteLine("\nRefreshing in 3 seconds...\n" + new string('-', 40));
            Thread.Sleep(3000);
        }

        computer.Close();
    }
}


/* full sample code 

Computer computer = new Computer
{
    IsCpuEnabled = true,
    IsGpuEnabled = true,
    IsMemoryEnabled = true,
    IsMotherboardEnabled = true,
    IsControllerEnabled = true,
    IsNetworkEnabled = true,
    IsStorageEnabled = true
};

computer.Open();
computer.Accept(new UpdateVisitor());

foreach (IHardware hardware in computer.Hardware)
{
    Console.WriteLine("Hardware: {0}", hardware.Name);
    
    foreach (IHardware subhardware in hardware.SubHardware)
    {
        Console.WriteLine("\tSubhardware: {0}", subhardware.Name);
        
        foreach (ISensor sensor in subhardware.Sensors)
            Console.WriteLine("\t\tSensor: {0}, value: {1}", sensor.Name, sensor.Value);
    }

    foreach (ISensor sensor in hardware.Sensors)
        Console.WriteLine("\tSensor: {0}, value: {1}", sensor.Name, sensor.Value);
}

computer.Close();

public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }

    public void VisitSensor(ISensor sensor) { }

    public void VisitParameter(IParameter parameter) { }
} */