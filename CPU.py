import time
import clr
clr.AddReference(r"C:\Users\nadia\Documents\GitHub\SOSBPowerMetrics\librehardwaremonitorlib.0.9.6\runtimes\win-x64\lib\netstandard2.0\LibreHardwareMonitorLib.dll")
clr.AddReference(r"C:\Users\nadia\Documents\GitHub\SOSBPowerMetrics\system.memory.4.6.3/lib/netstandard2.0/System.Memory.dll")
from LibreHardwareMonitor.Hardware import Computer

def get_temps():
    c = Computer()
    c.IsCpuEnabled = True
    c.Open()
    results = []
    for hardware in c.Hardware:
        hardware.Update()
        for sensor in hardware.Sensors:
            if sensor.SensorType.ToString() == "Temperature" and sensor.Value is not None:
                results.append((sensor.Name, round(float(sensor.Value), 1)))
    c.Close()
    return results or None

def main():
    print("CPU Temp (Ctrl+C to stop)\n")
    while True:
        temps = get_temps()
        print("Temperature:")
        if temps:
            for name, temp in temps:
                print(f"  {name}: {temp}°C")
        else:
            print("  No temp data")
        print("\nRefreshing in 3 seconds...\n" + "-" * 40)
        time.sleep(3)

if __name__ == "__main__":
    main()