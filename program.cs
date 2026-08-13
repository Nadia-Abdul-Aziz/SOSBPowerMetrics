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
    Dictionary<string, float> _data = new Dictionary<string, float>{
        {"CPU_Temp", -1.0f },
        {"CPU_Fan", -1.0f },
        {"CPU_Power", -1.0f },
        
        {"MOBO_Temp", -1.0f },
        {"MOBO_CMOS", -1.0f },
        
        {"FAN0", -1.0f },
        {"FAN1", -1.0f },
        {"FAN2", -1.0f },
        {"FAN3", -1.0f },
        
        {"GPU_Temp", -1.0f },
        {"GPU_Fan", -1.0f },
        {"GPU_Power", -1.0f },
        
        {"RAM0_TEMP", -1.0f },
        {"RAM1_TEMP", -1.0f },
        {"RAM2_TEMP", -1.0f },
        {"RAM3_TEMP", -1.0f },
        };

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

        while (true) // fuck... 
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
                            if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Name == "Temperature #1")
                                
                                _data["MOBO_Temp"] = Math.Round(sensor.Value.Value, 1);

                                // Console.WriteLine($" Temp: {Math.Round(sensor.Value.Value, 1)}°C");
                            if (sensor.SensorType == SensorType.Voltage && sensor.Value.HasValue && sensor.Name.Contains("CMOS"))
                            {
                                Console.WriteLine($" CMOS Battery: {Math.Round(sensor.Value.Value, 3)}V");
                                sender.Send(new OscMessage("/voltage/CMOS", (float)sensor.Value.Value));
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
                                float fanValue = (float)sensor.Value.Value;
                                if (sensorIndex%valueTable.GetLength(1) == 0){
                                    sensorIndex = 0;
                                }
                                
                                temporaryArrayRPM[sensorIndex] = fanValue;
                                sensorIndex++;
                                Console.WriteLine($" {label}: {Math.Round(fanValue, 0)}RPM");
                                sender.Send(new OscMessage($"/fan/{label}", fanValue));
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
                        {
                            Console.WriteLine($" Package: {Math.Round(sensor.Value.Value, 1)}W");
                            sender.Send(new OscMessage("/power/CPU", (float)sensor.Value.Value));
                        }
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
                        {
                            Console.WriteLine($" Package: {Math.Round(sensor.Value.Value, 1)}W");
                            sender.Send(new OscMessage("/power/GPU", (float)sensor.Value.Value));
                        }

                        if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                        {
                            Console.WriteLine($" {sensor.Name}: {Math.Round(sensor.Value.Value, 0)}RPM");
                            sender.Send(new OscMessage($"/fan/GPUFan", (float)sensor.Value.Value));
                            temporaryArrayRPM[valueTable.GetLength(1) - 1] = sensor.Value.Value; // store GPU fan RPM in the last index of the temporary array
                        }
                    }
                }

                if (hardware.HardwareType == HardwareType.Memory)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Name.StartsWith("DIMM"))
                        {
                            string dimmLabel = sensor.Name.Replace(" ", ""); // "DIMM #1" -> "DIMM#1"
                            Console.WriteLine($"{sensor.Name}: {Math.Round(sensor.Value.Value, 1)}°C");
                            sender.Send(new OscMessage($"/ram/{dimmLabel}", (float)sensor.Value.Value));
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

            // Broadcast
            // 1. Convert the _data dict to a JSON 
            // 2. broadcast over socket/http/etc. 



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