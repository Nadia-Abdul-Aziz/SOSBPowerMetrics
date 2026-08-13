using System;
using System.Threading;
using System.Net;
using System.Collections.Generic;
using System.IO;
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
    // variables in the class sections to be able to use in the functions for cleaner code
    static float[,] valueTable = new float [20,5]; // to modify the timeframe of the average, modify the first size of the array
    static int index = 0;

    //index references for the valueTable, to be able to store the values in the correct index of the array
    static int GPU_FAN_INDEX = 4; // index of the GPU fan in the valueTable
    static int CPU_FAN_INDEX = 0; // index of the CPU fan in the valueTable

    

    static void Main()
    {
        var sender = new UDPSender("127.0.0.1", 7000);


        Console.WriteLine("CPU Temp (Ctrl+C to stop)\n");

        Computer computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsMemoryEnabled = true,
        };

        computer.Open();

        while (true)
        {
            bool gpuSkipped = false;
            computer.Accept(new UpdateVisitor());
            int sensorIndex = 0;
            float[] temporaryArrayRPM = new float[valueTable.GetLength(1)];

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
                            var sensorValue = sensor.Value.Value;
                            if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Name == "Temperature #1")
                                Console.WriteLine($" Temp: {Math.Round(sensorValue, 1)}°C");
                            if (sensor.SensorType == SensorType.Voltage && sensor.Value.HasValue && sensor.Name.Contains("CMOS"))
                            {
                                Console.WriteLine($" CMOS Battery: {Math.Round(sensorValue, 3)}V");
                                sender.Send(new OscMessage("/voltage/CMOS", (float)sensorValue));
                            }
                            if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                            {
                                string label = sensor.Name switch
                                {
                                    "Fan #1" => "CPUFan",
                                    "Fan #3" => "CaseFanA",
                                    "Fan #5" => "CaseFanB",
                                    _ => sensor.Name
                                };

                                if (sensorIndex%valueTable.GetLength(1) == 0){
                                    sensorIndex = 0;
                                }
                                
                                temporaryArrayRPM[sensorIndex] = sensorValue;
                                sensorIndex++;
                                Console.WriteLine($" {label}: {Math.Round(sensorValue, 0)}RPM");
                                sender.Send(new OscMessage($"/fan/{label}", sensorValue));
                            }
                        }
                    }
                }

                // CPU sensors
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    Console.WriteLine("CPU");
                    foreach (var sensor in hardware.Sensors)
                    {
                        var sensorValue = sensor.Value.Value;
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Name.Contains("Tctl"))
                            Console.WriteLine($" Temperature: {Math.Round(sensorValue, 1)}°C");

                        if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && sensor.Name.Contains("Package"))
                        {
                            Console.WriteLine($" Package: {Math.Round(sensorValue, 1)}W");
                            sender.Send(new OscMessage("/power/CPU", (float)sensorValue));
                        }
                    }
                }

                // GPU sensors, works for either Nvidia or AMD GPUs
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
                        var sensorValue = sensor.Value.Value;
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Name.Contains("Core"))
                            Console.WriteLine($" Temperature: {Math.Round(sensorValue, 1)}°C");

                        if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && sensor.Name.Contains("Package"))
                        {
                            Console.WriteLine($" Package: {Math.Round(sensorValue, 1)}W");
                            sender.Send(new OscMessage("/power/GPU", (float)sensorValue));
                        }

                        if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                        {
                            Console.WriteLine($" {sensor.Name}: {Math.Round(sensorValue, 0)}RPM");
                            sender.Send(new OscMessage($"/fan/GPUFan", (float)sensorValue));
                            temporaryArrayRPM[GPU_FAN_INDEX] = sensorValue;
                        }
                    }
                }

                if (hardware.HardwareType == HardwareType.Memory)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        var sensorValue = sensor.Value.Value;
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Name.StartsWith("DIMM"))
                        {
                            string dimmLabel = sensor.Name.Replace(" ", ""); // "DIMM #1" -> "DIMM#1"
                            Console.WriteLine($"{sensor.Name}: {Math.Round(sensorValue, 1)}°C");
                            sender.Send(new OscMessage($"/ram/{dimmLabel}", (float)sensorValue));
                        }
                    }
                }
            }
            //averages the values in the table when table is filled
            if (index%valueTable.GetLength(0) == 0){
                index=0;
                writeValue(AverageValue());
            }
            //Console.WriteLine("table index: " + index);
            FillTable(temporaryArrayRPM);

            Console.WriteLine("\nRefreshing in 250ms\n" + new string('-', 40));
            Thread.Sleep(250);
        }
    }

    static void FillTable(float[] tempArray){
        for (int i=0; i<tempArray.Length; i++){
            //Console.WriteLine("indexes are " + index + ", " + i);
            valueTable[index,i] = tempArray[i];
        }
        index++;
    }

    static float[] AverageValue(){
        float[] total = new float [valueTable.GetLength(1)];
        for (int i=0; i<total.Length; i++){
            total[i] = 0;
        }
        for (int i=0; i<valueTable.GetLength(0); i++){
            for (int j=0; j<valueTable.GetLength(1); j++){
                total[j] += valueTable[i,j];
            }
        }
        
        for (int i=0; i<total.Length; i++){
            //Console.WriteLine(total[i] + "/" + valueTable.GetLength(0) + "=" + total[i]/valueTable.GetLength(0));
            total[i] = total[i]/valueTable.GetLength(0);
        }
        return total;
    }

    static void writeValue(float[] value){
        string filePath = "averageFanRPM.csv";

        var localTime = DateTime.Now;

        // DateTime now = DateTime.Now;
        // TimeSpan timeOfDay = date.TimeOfDay;
        if (value[0] == 0){
            return;
        }

        string[] items = {localTime.ToString(), value[0].ToString(), value[1].ToString(), value[2].ToString(), value[3].ToString(), value[4].ToString()};
        string line = string.Join(", ", items);

        using (StreamWriter writer = new StreamWriter(filePath, true)){
            writer.WriteLine(line);
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