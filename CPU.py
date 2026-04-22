#system-level operations like exiting the program
import sys
# pause execution between display refresh
import time
# Import csv module
import csv
# Import Path from pathlib for OS-independent file path handling stuff
from pathlib import Path
 
# Absolute path to the LibreHardwareMonitor CSV log file (Only works on my computer!!!!!)
LOG_FILE = r"C:\Users\nadia\Downloads\LibreHardwareMonitor\LibreHardwareMonitorLog-2026-04-19-1.csv"
 
# Reading data from libre
def read_libre_sensors():
    # puytting log path string in Path object for .exists() and open() support
    path = Path(LOG_FILE)
    try:
        # Open CSV file in read mode with UTF-8 encoding (LHM uses UTF-8)
        with open(path, newline='', encoding='utf-8') as f:
            # Create csv.reader that splits each line on commas
            reader = csv.reader(f)
            # Load every row into memory as a list of lists (???)
            rows = list(reader)
 
        # Layout as follows;
        #   Row 0 = sensor type (temp,power,load))
        #   Row 1  = sensor name
        #   Row 2 above = timestamped readings, last row most recent snapshot
        # Need at least 3 rows (2 header + 1 data) to proceed
        if len(rows) < 3:
            # too low
            return None
 
        # Assigning
        types = rows[0]
        headers = rows[1]
        latest = rows[-1]
 
        # Dictionary to accumulate results
        results = {}
 
        # Iterate over all three lists (Gemini help)
        for typ, header, value in zip(types, headers, latest):
            # Case sensitivity fix
            sensor_type = typ.strip().lower()
 
            # Skip
            if value.strip() in ("", "0"):
                continue
 
            try:
                # convert to float
                reading = float(value)
            except ValueError:
                # if not possible, skip
                continue
 
            # Only store positive readings, if not, skip
            if reading <= 0:
                continue
 
            # name + value to correct bucket in dict
            results[sensor_type].append((header.strip(), round(reading, 1)))
 
        # Return the populated dict
        return results if results else None

 
 
def get_cpu_temp_libre(sensors):
 
    # Pull the list of temperature readings from dict
    temp_readings = sensors.get("temperature")
 
    # Return the list if it has entries, otherwise return None to trigger a fallback
    return temp_readings if temp_readings else None
 
 
# Return temperature readings by querying Windows' ACPI thermal zones via WMI
def get_cpu_temp_wmi():
    # Import the wmi module (Windows-only; not bundled with Python, needs pip install wmi)
    import wmi
    try:
        # Connect to the root\wmi namespace where ACPI thermal objects live
        w = wmi.WMI(namespace="root\\wmi")
        # Query all MSAcpi_ThermalZoneTemperature instances (one per thermal zone)
        sensors = w.MSAcpi_ThermalZoneTemperature()
        # Accumulator list for (zone_name, celsius) pairs
        temps = []
        # Loop over each thermal zone object returned by WMI
        for sensor in sensors:
            # WMI reports temperature in tenths of kelvin; convert to Celsius
            celsius = (sensor.CurrentTemperature / 10.0) - 273.15
            # Append the zone's instance name and the rounded Celsius value
            temps.append((sensor.InstanceName, round(celsius, 1)))
        # Return the list only if it's non-empty; None triggers the next fallback
        return temps if temps else None
    except Exception:
        # WMI unavailable (non-Windows OS, missing driver, permission error, etc.)
        return None
 
 
# Return temperature readings using the cross-platform psutil library
def get_cpu_temp_psutil():
    # Import psutil (pip install psutil); available on Linux/macOS/Windows
    import psutil
    try:
        # sensors_temperatures() returns a dict of {chip_name: [shwtemp(label, current, …), …]}
        temps = psutil.sensors_temperatures()
        # On Windows psutil often returns an empty dict or raises AttributeError
        if not temps:
            # Empty dict means no temperature sensor support on this platform
            return None
        # Accumulator list for all discovered temperature readings
        results = []
        # Iterate over each chip name and its list of sensor entries
        for name, entries in temps.items():
            # Each entry is a named tuple with .label, .current, .high, .critical fields
            for entry in entries:
                # Compose a descriptive label: chip name + sensor label (default "Core")
                results.append((f"{name} - {entry.label or 'Core'}", round(entry.current, 1)))
        # Return the list if populated; None if no sensors reported any data
        return results if results else None
    except AttributeError:
        # AttributeError is raised on Windows where sensors_temperatures() doesn't exist
        return None
 
 
# ──────────────────────────────────────────────────────────
# POWER DRAW  — primary: LHM CSV,  fallbacks: WMI → psutil
# ──────────────────────────────────────────────────────────
 
# Return a list of (label, wattage_string) tuples using LibreHardwareMonitor as the source
def get_power_draw_libre(sensors):
    # sensors is the dict from read_libre_sensors(); None means LHM file unavailable
    if sensors is None:
        # No LHM data; signal the caller to fall back to WMI or psutil
        return None
 
    # Pull the power readings bucket from the shared sensor dict
    # LHM labels power sensors with type "Power" → lower-cased to "power"
    power_readings = sensors.get("power")
 
    # power_readings is a list of (sensor_name, watts_float) tuples or None
    if not power_readings:
        # LHM is available but recorded no power sensors; try the next source
        return None
 
    # Convert the raw float watts into formatted strings for display
    # e.g. ("CPU Package", 45.3) → ("CPU Package", "45.3 W")
    return [(name, f"{watts} W") for name, watts in power_readings]
 
 
# Return power draw by querying Windows' battery status object via WMI (BatteryStatus)
def get_power_draw_wmi():
    try:
        # Import wmi here so the function gracefully returns None on non-Windows systems
        import wmi
        # Connect to root\wmi; BatteryStatus lives here (different from Win32_Battery)
        w = wmi.WMI(namespace="root\\wmi")
        # Query all BatteryStatus instances (usually one per battery)
        battery_statuses = w.BatteryStatus()
        # Accumulator for (label, value_string) power entries
        results = []
        # Iterate over each battery object
        for b in battery_statuses:
            # ChargeRate is in milliwatts when the battery is charging; default 0 if absent
            charge_mw = getattr(b, "ChargeRate", 0) or 0
            # DischargeRate is in milliwatts when running on battery; default 0 if absent
            discharge_mw = getattr(b, "DischargeRate", 0) or 0
 
            # If a non-zero charge rate is present, the battery is actively charging
            if charge_mw:
                # Convert milliwatts → watts and format to one decimal place
                results.append(("Battery charging", f"{charge_mw / 1000:.1f} W"))
 
            # If a non-zero discharge rate is present, the system is drawing from the battery
            if discharge_mw:
                # Convert milliwatts → watts and format to one decimal place
                results.append(("Battery draw", f"{discharge_mw / 1000:.1f} W"))
 
            # If neither rate is available, at least report AC presence via PowerOnline flag
            ac = getattr(b, "PowerOnline", None)
            # Only add the AC status entry when no watt figures are already shown
            if ac is not None and not charge_mw and not discharge_mw:
                # Human-readable AC status string
                results.append(("AC power", "connected" if ac else "on battery (rate unavailable)"))
 
        # Return results if we got at least one entry; None triggers the next fallback
        if results:
            return results
    except Exception:
        # WMI not available or BatteryStatus query failed (desktop without a battery, etc.)
        pass
    # Fall through to the next source
    return None
 
 
# Return power draw using psutil's battery sensor (cross-platform, lower accuracy)
def get_power_draw_psutil():
    try:
        # Import psutil for the cross-platform battery API
        import psutil
        # sensors_battery() returns a named tuple or None if no battery is present
        batt = psutil.sensors_battery()
        # None means no battery (desktop PC or unsupported platform)
        if batt is not None:
            # Accumulator for power entries
            results = []
            # Some psutil builds on Linux expose a .watts attribute; Windows usually does not
            watts = getattr(batt, "watts", None)
            if watts is not None:
                # Determine charging direction for the label
                label = "Charging" if batt.power_plugged else "Discharging"
                # Format the wattage to one decimal place
                results.append((label, f"{watts:.1f} W"))
            else:
                # .watts not available; fall back to a plain status string
                status = "plugged in" if batt.power_plugged else f"on battery ({batt.percent:.0f}%)"
                # Report what we know even without an exact wattage figure
                results.append(("Battery", status))
            # Return the list; it will always have at least one entry here
            return results
    except Exception:
        # psutil unavailable or sensors_battery() raised an unexpected error
        pass
    # All sources exhausted; return None so the caller can print "no data"
    return None
 
 
# ──────────────────────────────────────────────
# PER-CORE CPU USAGE
# ──────────────────────────────────────────────
 
# Return a list of per-core CPU utilisation percentages using psutil
def get_cpu_usage():
    # Import psutil here (already imported transitively above, but kept explicit)
    import psutil
    # cpu_percent with percpu=True returns a list with one float per logical core
    # interval=1 means psutil blocks for 1 second to measure actual utilisation
    return psutil.cpu_percent(interval=1, percpu=True)
 
 
# ──────────────────────────────────────────────
# MAIN MONITORING LOOP
# ──────────────────────────────────────────────
 
def main():
    # Print a static header so the user knows what program is running
    print("CPU Temperature & Power Monitor — Windows")
    # Remind the user how to stop the infinite loop cleanly
    print("Press Ctrl+C to stop.\n")
 
    try:
        # Loop forever; Ctrl+C raises KeyboardInterrupt which is caught below
        while True:
            # ── Read the LHM CSV once per cycle and reuse the result for both
            #    temperature and power so we only open/parse the file a single time
            libre_sensors = read_libre_sensors()
 
            # ── TEMPERATURE: try LHM first, then WMI, then psutil ──────────────
            # Attempt to get temperatures from the already-parsed LHM sensor dict
            temps = get_cpu_temp_libre(libre_sensors)
            # If LHM had no temperature data, try the WMI ACPI thermal zones
            if temps is None:
                # WMI fallback for temperature
                temps = get_cpu_temp_wmi()
            # If WMI also failed, try psutil as the last resort
            if temps is None:
                # psutil fallback for temperature
                temps = get_cpu_temp_psutil()
 
            # ── CPU USAGE (always from psutil; no alternative source exists) ───
            # This call blocks for ~1 second while psutil measures CPU activity
            usage = get_cpu_usage()
 
            # ── POWER DRAW: try LHM first, then WMI, then psutil ───────────────
            # Attempt to get power readings from the already-parsed LHM sensor dict
            power = get_power_draw_libre(libre_sensors)
            # If LHM had no power data, try the WMI BatteryStatus object
            if power is None:
                # WMI fallback for power
                power = get_power_draw_wmi()
            # If WMI also failed, try psutil battery sensor as the last resort
            if power is None:
                # psutil fallback for power
                power = get_power_draw_psutil()
 
            # ── DISPLAY: TEMPERATURES ─────────────────────────────────────────
            # Print the temperature section header
            print("=== Temperature ===")
            # Check whether any temperature source returned data
            if temps:
                # Iterate over each (sensor_name, celsius) tuple in the list
                for name, temp in temps:
                    # Print each sensor on its own line with a degree-C suffix
                    print(f"  {name}: {temp}°C")
            else:
                # No source provided temperature data; inform the user
                print("  No temperature data available.")
 
            # ── DISPLAY: CPU USAGE PER CORE ───────────────────────────────────
            # Print the CPU usage section header
            print("\n=== CPU Usage ===")
            # enumerate() gives us a (0-based index, percentage) pair for each core
            for i, pct in enumerate(usage):
                # Print each logical core's utilisation percentage
                print(f"  Core {i}: {pct}%")
 
            # ── DISPLAY: POWER DRAW ───────────────────────────────────────────
            # Print the power draw section header
            print("\n=== Power Draw ===")
            # Check whether any power source returned data
            if power:
                # Iterate over each (label, value_string) tuple in the list
                for label, value in power:
                    # Print each power entry on its own line
                    print(f"  {label}: {value}")
            else:
                # No source provided power data; inform the user
                print("  No power data available.")
 
            # ── WAIT BEFORE NEXT CYCLE ────────────────────────────────────────
            # Print a separator and countdown message before sleeping
            print("\nRefreshing in 3 seconds...\n" + "-" * 40)
            # Pause for 3 seconds before reading all sensors again
            time.sleep(3)
 
    except KeyboardInterrupt:
        # User pressed Ctrl+C; exit the loop gracefully
        print("\n\nMonitor stopped.")
 
 
# ── ENTRY POINT ───────────────────────────────────────────────────────────────
# Only run main() when this script is executed directly, not when imported as a module
if __name__ == "__main__":
    # Call the main monitoring loop
    main()