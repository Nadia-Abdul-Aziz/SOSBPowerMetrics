using System;
using System.Threading;
using System.Net;
using LibreHardwareMonitor.Hardware;
using SharpOSC;

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
        var sender = new UDPSender("127.0.0.1", 7000);

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

                            // GPU fan removed from here - it's read from the actual GPU hardware below instead
                            if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                            {
                                string label = sensor.Name switch
                                {
                                    "Fan #1" => "CPUFan",
                                    "Fan #3" => "CaseFanA",
                                    "Fan #5" => "CaseFanB",
                                    _ => sensor.Name
                                };
                                Console.WriteLine($" {label}: {Math.Round(sensor.Value.Value, 0)}RPM");
                                sender.Send(new OscMessage($"/fan/{label}", (float)sensor.Value.Value));
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

                        // Actual GPU fan sensor - the real reading, now sent over OSC too
                        if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                        {
                            Console.WriteLine($" {sensor.Name}: {Math.Round(sensor.Value.Value, 0)}RPM");
                            sender.Send(new OscMessage($"/fan/GPUFan", (float)sensor.Value.Value));
                        }
                    }
                }
            }

            Console.WriteLine("\nRefreshing in 250ms\n" + new string('-', 40));
            Thread.Sleep(250);
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