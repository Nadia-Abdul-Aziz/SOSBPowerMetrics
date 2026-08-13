using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int PollIntervalMs = 250;
    const int SamplesPerAverage = 20; // to modify the timeframe of the average, modify this
    const int FanColumns = 5;         // 4 motherboard fans + the GPU fan
    const string CsvPath = "averageFanRPM.csv";
    const string CsvHeader = "Date and time, fan 1, fan 2, fan 3, fan 4, gpu fan";

    static bool running = true;

    static void Main()
    {
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; running = false; };
        Logger.Always("Power metrics (Ctrl+C to stop)\n");

        var osc = new OscBroadcaster("127.0.0.1", 7000);
        var metrics = new Metrics();
        var averager = new FanAverager(SamplesPerAverage, FanColumns);
        var csv = new CsvLogger(CsvPath, CsvHeader);
        var monitor = new HardwareMonitor(osc);

        monitor.Open();

        var stopwatch = new Stopwatch();

        while (running)
        {
            stopwatch.Restart();

            metrics.Reset();
            float[] fanSample = averager.NewSample();
            monitor.Poll(metrics, fanSample);

            float[] averages = averager.AddSample(fanSample);
            if (averages != null)
                csv.Append(averages);

            // Broadcast: metrics now holds every reading from this cycle, -1 where a sensor didn't report.
            string json = metrics.ToJson();
            // TODO: push `json` to the connected websocket clients here.
            Logger.Log(json);

            Logger.Log($"\nRefreshing in {PollIntervalMs}ms\n" + new string('-', 40));

            int remaining = PollIntervalMs - (int)stopwatch.ElapsedMilliseconds; // hold the cadence no matter how long polling took
            if (remaining > 0)
                Thread.Sleep(remaining);
        }

        monitor.Close();
        Logger.Always("Closed.");
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