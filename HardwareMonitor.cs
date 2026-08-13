using System;
using LibreHardwareMonitor.Hardware;

// owns the LibreHardwareMonitor session. one Poll turns a hardware sweep into a
// filled Metrics snapshot plus a fan RPM sample, publishing over OSC as it reads.
class HardwareMonitor
{
    readonly Computer _computer;
    readonly OscBroadcaster _osc;
    IHardware _targetGpu;

    public HardwareMonitor(OscBroadcaster osc)
    {
        _osc = osc;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsMemoryEnabled = true,
        };
    }

    public void Open()
    {
        _computer.Open();
        _targetGpu = SelectGpu();
        Logger.Always(_targetGpu != null ? $"GPU: {_targetGpu.Name}\n" : "GPU: none detected\n");
    }

    public void Close() => _computer.Close();

    // pick the one GPU to report on: the discrete NVIDIA card if there is one,
    // otherwise whatever GPU is present. replaces the old "skip the first GPU"
    // trick, which reported nothing at all on a single-GPU machine.
    IHardware SelectGpu()
    {
        IHardware fallback = null;
        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType == HardwareType.GpuNvidia)
                return hardware;
            if (hardware.HardwareType == HardwareType.GpuAmd && fallback == null)
                fallback = hardware;
        }
        return fallback;
    }

    // reads every sensor once, fills `metrics`, and writes fan RPMs into `fanSample`:
    // columns 0..n-2 are motherboard fans in enumeration order, the last is the GPU fan.
    public void Poll(Metrics metrics, float[] fanSample)
    {
        _computer.Accept(new UpdateVisitor());

        int fanSlot = 0;
        int dimmIndex = 0;
        int motherboardFanSlots = fanSample.Length - 1; // last column is reserved for the GPU fan

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();

            if (hardware.HardwareType == HardwareType.Motherboard)
                ReadMotherboard(hardware, metrics, fanSample, motherboardFanSlots, ref fanSlot);

            if (hardware.HardwareType == HardwareType.Cpu)
                ReadCpu(hardware, metrics);

            if (hardware == _targetGpu)
                ReadGpu(hardware, metrics, fanSample);

            if (hardware.HardwareType == HardwareType.Memory)
                ReadMemory(hardware, metrics, ref dimmIndex);
        }
    }

    void ReadMotherboard(IHardware hardware, Metrics metrics, float[] fanSample, int fanSlots, ref int fanSlot)
    {
        Logger.Log("Motherboard");
        foreach (var sub in hardware.SubHardware)
        {
            sub.Update();
            foreach (var sensor in sub.Sensors)
            {
                if (!sensor.Value.HasValue)
                    continue;

                if (sensor.SensorType == SensorType.Temperature && sensor.Name == "Temperature #1")
                {
                    metrics.Set("MOBO_Temp", Math.Round(sensor.Value.Value, 1));
                    // Logger.Log($" Temp: {Math.Round(sensor.Value.Value, 1)}°C");
                }

                if (sensor.SensorType == SensorType.Voltage && sensor.Name.Contains("CMOS"))
                {
                    metrics.Set("MOBO_CMOS", Math.Round(sensor.Value.Value, 3));
                    Logger.Log($" CMOS Battery: {Math.Round(sensor.Value.Value, 3)}V");
                    _osc.Voltage("CMOS", (float)sensor.Value.Value);
                }

                if (sensor.SensorType == SensorType.Fan)
                {
                    string label = sensor.Name switch
                    {
                        "Fan #1" => "CPUFan",
                        "Fan #3" => "CaseFanA",
                        "Fan #5" => "CaseFanB",
                        _ => sensor.Name
                    };
                    float rpm = (float)sensor.Value.Value;

                    if (fanSlot < fanSlots) // extra fans still go out over OSC, they just aren't averaged
                    {
                        fanSample[fanSlot] = rpm;
                        metrics.Set($"FAN{fanSlot}", rpm); // FAN0..FAN3 follow enumeration order, same as the averaging columns
                        fanSlot++;
                    }
                    if (label == "CPUFan")
                        metrics.Set("CPU_Fan", rpm);

                    Logger.Log($" {label}: {Math.Round(rpm, 0)}RPM");
                    _osc.Fan(label, rpm);
                }
            }
        }
    }

    void ReadCpu(IHardware hardware, Metrics metrics)
    {
        Logger.Log("CPU");
        foreach (var sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue)
                continue;

            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Tctl"))
            {
                metrics.Set("CPU_Temp", Math.Round(sensor.Value.Value, 1));
                Logger.Log($" Temperature: {Math.Round(sensor.Value.Value, 1)}°C");
            }

            if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("Package"))
            {
                metrics.Set("CPU_Power", Math.Round(sensor.Value.Value, 1));
                Logger.Log($" Package: {Math.Round(sensor.Value.Value, 1)}W");
                _osc.Power("CPU", (float)sensor.Value.Value);
            }
        }
    }

    void ReadGpu(IHardware hardware, Metrics metrics, float[] fanSample)
    {
        Logger.Log("GPU");
        foreach (var sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue)
                continue;

            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Core"))
            {
                metrics.Set("GPU_Temp", Math.Round(sensor.Value.Value, 1));
                Logger.Log($" Temperature: {Math.Round(sensor.Value.Value, 1)}°C");
            }

            if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("Package"))
            {
                metrics.Set("GPU_Power", Math.Round(sensor.Value.Value, 1));
                Logger.Log($" Package: {Math.Round(sensor.Value.Value, 1)}W");
                _osc.Power("GPU", (float)sensor.Value.Value);
            }

            if (sensor.SensorType == SensorType.Fan)
            {
                float rpm = (float)sensor.Value.Value;
                metrics.Set("GPU_Fan", rpm);
                fanSample[fanSample.Length - 1] = rpm; // GPU fan lives in the last averaging column
                Logger.Log($" {sensor.Name}: {Math.Round(rpm, 0)}RPM");
                _osc.Fan("GPUFan", rpm);
            }
        }
    }

    void ReadMemory(IHardware hardware, Metrics metrics, ref int dimmIndex)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue)
                continue;

            if (sensor.SensorType == SensorType.Temperature && sensor.Name.StartsWith("DIMM"))
            {
                string dimmLabel = sensor.Name.Replace(" ", ""); // "DIMM #1" -> "DIMM#1"
                if (dimmIndex < 4) // RAM0..RAM3 follow enumeration order
                {
                    metrics.Set($"RAM{dimmIndex}_TEMP", Math.Round(sensor.Value.Value, 1));
                    dimmIndex++;
                }
                Logger.Log($"{sensor.Name}: {Math.Round(sensor.Value.Value, 1)}°C");
                _osc.Ram(dimmLabel, (float)sensor.Value.Value);
            }
        }
    }
}
