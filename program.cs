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
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
        };

        computer.Open();

        while (true)
        {
            bool gpuSkipped = false;
            computer.Accept(new UpdateVisitor());

            foreach (var hardware in computer.Hardware)
            {
                hardware.Update();

                if (hardware.HardwareType == HardwareType.Motherboard)
                {
                    Console.WriteLine("Motherboard");
                    foreach (var sub in hardware.SubHardware)
                    {
                        sub.Update();
                        foreach (var sensor in sub.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Name == "Temperature #1")
                                Console.WriteLine($" Temp: {Math.Round(sensor.Value.Value, 1)}°C");

                            if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                            {
                                string label = sensor.Name switch
                                {
                                    "Fan #1" => "CPU Fan",
                                    "Fan #2" => "GPU Fan",
                                    "Fan #3" => "Case Fan A",
                                    "Fan #5" => "Case Fan B",
                                    _ => sensor.Name
                                };
                                Console.WriteLine($" {label}: {Math.Round(sensor.Value.Value, 0)}RPM");
                            }
                        }
                    }
                }

                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    Console.WriteLine("CPU");
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Name.Contains("Tctl"))
                            Console.WriteLine($" Temperature: {Math.Round(sensor.Value.Value, 1)}°C");

                        if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && sensor.Name.Contains("Package"))
                            Console.WriteLine($" Package: {Math.Round(sensor.Value.Value, 1)}W");
                    }
                }

                if (hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuAmd)
                {
                    if (!gpuSkipped)
                    {
                        gpuSkipped = true;
                        continue;
                    }
                    Console.WriteLine("GPU");
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Name.Contains("Core"))
                            Console.WriteLine($" Temperature: {Math.Round(sensor.Value.Value, 1)}°C");

                        if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && sensor.Name.Contains("Package"))
                            Console.WriteLine($" Package: {Math.Round(sensor.Value.Value, 1)}W");

                        if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                            Console.WriteLine($" {sensor.Name}: {Math.Round(sensor.Value.Value, 0)}RPM");
                    }
                }
            }

            Console.WriteLine("\nRefreshing in 3 seconds...\n" + new string('-', 40));
            Thread.Sleep(3000);
        }
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