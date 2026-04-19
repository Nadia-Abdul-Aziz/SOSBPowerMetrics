# for system-level operations like exiting the program
import sys
# pause program between updates
import time


# Reading temp with WMI
def get_cpu_temp_wmi():
    import wmi
    try:
        w = wmi.WMI(namespace="root\\wmi")
        sensors = w.MSAcpi_ThermalZoneTemperature()
        temps = []
        for sensor in sensors:
            celsius = (sensor.CurrentTemperature / 10.0) - 273.15
            temps.append((sensor.InstanceName, round(celsius, 1)))
        return temps
    except Exception:
        return None

# Fallbvasck with psutil
def get_cpu_temp_psutil():
    import psutil
    try:
        temps = psutil.sensors_temperatures()
        if not temps:
            return None
        results = []
        for name, entries in temps.items():
            for entry in entries:
                results.append((f"{name} - {entry.label or 'Core'}", round(entry.current, 1)))
        return results if results else None
    except AttributeError:
        return None


def get_cpu_usage():
    import psutil
    return psutil.cpu_percent(interval=1, percpu=True)

# Getting wattage, attempts 3 methods in order of accuracy: MSAcpi_BatteryStatus, psutil sensors_battery(), and finally Win32_Battery (which doesn't give watts, so we skip it). 
def get_power_draw():
    # MSAcpi
    try:
        import wmi
        w = wmi.WMI(namespace="root\\wmi")
        battery_statuses = w.BatteryStatus()
        results = []
        for b in battery_statuses:
            charge_mw = getattr(b, "ChargeRate", 0) or 0
            discharge_mw = getattr(b, "DischargeRate", 0) or 0

            if charge_mw:
                results.append(("Battery charging", f"{charge_mw / 1000:.1f} W"))
            if discharge_mw:
                results.append(("Battery draw", f"{discharge_mw / 1000:.1f} W"))
            ac = getattr(b, "PowerOnline", None)
            if ac is not None and not charge_mw and not discharge_mw:
                results.append(("AC power", "connected" if ac else "on battery (rate unavailable)"))

        if results:
            return results
    except Exception:
        pass

    # psutil
    try:
        import psutil
        batt = psutil.sensors_battery()
        if batt is not None:
            results = []
            watts = getattr(batt, "watts", None)
            if watts is not None:
                label = "Charging" if batt.power_plugged else "Discharging"
                results.append((label, f"{watts:.1f} W"))
            else:
                status = "plugged in" if batt.power_plugged else f"on battery ({batt.percent:.0f}%)"
                results.append(("Battery", status))
            return results
    except Exception:
        pass

    # none found
    return None


def main():
    print("CPU Temperature & Power Monitor — Windows")
    print("Press Ctrl+C to stop.\n")

    try:
        while True:
            temps = get_cpu_temp_wmi() or get_cpu_temp_psutil()
            usage = get_cpu_usage()
            power = get_power_draw()

            # temps
            print("=== Temperature ===")
            if temps:
                for name, temp in temps:
                    print(f"  {name}: {temp}°C")
            else:
                print("  No temperature data available.")

            # cpu per core
            print("\n=== CPU Usage ===")
            for i, pct in enumerate(usage):
                print(f"  Core {i}: {pct}%")

            # power
            print("\n=== Power Draw ===")
            if power:
                for label, value in power:
                    print(f"  {label}: {value}")
            else:
                print("  No power data available.")

            print("\nRefreshing in 3 seconds...\n" + "-" * 40)
            time.sleep(3)

    except KeyboardInterrupt:
        print("\n\nMonitor stopped.")


if __name__ == "__main__":
    main()