using System.Collections.Generic;
using System.Text.Json;

// the snapshot of every sensor reading for one poll cycle, ready to serialise.
// NoReading (-1) means "nothing reported this cycle", so a sensor that drops out
// doesn't leave a stale reading sitting in the feed.
class Metrics
{
    public const float NoReading = -1.0f;

    readonly Dictionary<string, float> _data = new Dictionary<string, float>{
        {"CPU_Temp", NoReading },
        {"CPU_Fan", NoReading },
        {"CPU_Power", NoReading },

        {"MOBO_Temp", NoReading },
        {"MOBO_CMOS", NoReading },

        {"FAN0", NoReading },
        {"FAN1", NoReading },
        {"FAN2", NoReading },
        {"FAN3", NoReading },

        {"GPU_Temp", NoReading },
        {"GPU_Fan", NoReading },
        {"GPU_Power", NoReading },

        {"RAM0_TEMP", NoReading },
        {"RAM1_TEMP", NoReading },
        {"RAM2_TEMP", NoReading },
        {"RAM3_TEMP", NoReading },
        };

    public float this[string key] => _data.TryGetValue(key, out var value) ? value : NoReading;

    // takes a double so Math.Round results don't need a cast at every call site
    public void Set(string key, double value) => _data[key] = (float)value;

    public void Reset()
    {
        foreach (var key in new List<string>(_data.Keys))
            _data[key] = NoReading;
    }

    public string ToJson() => JsonSerializer.Serialize(_data);
}
