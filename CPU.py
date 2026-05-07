import time
import csv
from pathlib import Path

LOG_FILE = r"C:\Users\nadia\Downloads\LibreHardwareMonitorLog-2026-05-07.csv"

def get_temps():
    path = Path(LOG_FILE)
    with open(path, newline='', encoding='latin-1') as f:
        rows = list(csv.reader(f))
    if len(rows) < 3:
        return None
    results = []
    for typ, header, value in zip(rows[0], rows[1], rows[-1]):
        if "temperature" not in typ.strip().lower() or "core (tctl" not in header.strip().lower():
            continue
        if value.strip() in ("", "0"):
            continue
        try:
            reading = float(value)
        except ValueError:
            continue
        if reading > 0:
            results.append((header.strip(), round(reading, 1)))
    return results or None

def main():
    print("CPU Temp (Ctrl+C to stop)\n")
    while True:
        temps = get_temps()
        print("Temperature:")
        if temps:
            for name, temp in temps:
                print(f"CPU Core : {temp}°C")
        else:
            print("  No temp data")
        print("\nRefreshing in 3 seconds...\n" + "-" * 40)
        time.sleep(3)

if __name__ == "__main__":
    main()