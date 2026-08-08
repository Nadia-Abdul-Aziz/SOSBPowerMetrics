using LibreHardwareMonitor.Hardware;
using System.Text.Json;

public class fanReader{

public static void Main(string[] args)
{
Computer computer = new Computer
{
    IsMotherboardEnabled = true,
    IsCpuEnabled = true,
    IsGpuEnabled = true,
    IsControllerEnabled = true
};

computer.Open();

try
{
    while (true)
    {
        // Dictionary to store fan name/RPM value
        var fans = new Dictionary<string, float>();

        // Loop through hardware
        foreach (IHardware hardware in computer.Hardware)
        {
            // Read all
            ReadHardware(hardware, fans);
        }

        // Convert to JSON and print it.
        // One JSON obj printed every loop.
        Console.WriteLine(JsonSerializer.Serialize(fans));

        // Wait 250ms
        Thread.Sleep(250);
    }
}
finally
{
    computer.Close();
} }


//Half vibe coded, don't ask

static void ReadHardware(
    IHardware hardware,
    Dictionary<string, float> fans)
{
    // Refresh
    hardware.Update();

    // Go through all sensor attached to hardware
    foreach (ISensor sensor in hardware.Sensors)
    {
        // Only keep sensors with rpm + value
        if (sensor.SensorType == SensorType.Fan &&
            sensor.Value.HasValue)
        {
            //readable name
            string name = $"{hardware.Name} / {sensor.Name}";

            // Store RPM in dictionary
            // sensor.Value is float?
            // Value.Value extracts the actual float.
            fans[name] = sensor.Value.Value;
        }
    }

    // Recursively read each child, to not miss any
    foreach (IHardware subHardware in hardware.SubHardware)
    {
        ReadHardware(subHardware, fans);
    }
}}
