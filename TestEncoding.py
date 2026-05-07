from pathlib import Path
for f in Path.home().rglob("LibreHardwareMonitorLog-2026-05-07*.csv"):
    print(f)