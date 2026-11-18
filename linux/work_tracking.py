"""Durable local work sessions and day-based totals (no network services)."""
from datetime import datetime, timedelta
import json
import os
from pathlib import Path
import uuid


def split_days(task, started, seconds):
    entries = []
    while seconds > 0.000001:
        local = datetime.fromtimestamp(started).astimezone()
        tomorrow = datetime.combine(local.date() + timedelta(days=1), datetime.min.time()).timestamp()
        part = min(seconds, tomorrow - started)
        entries.append({"Day": local.date().isoformat(), "Task": task, "Seconds": part})
        started += part
        seconds -= part
    return entries


class Journal:
    def __init__(self, folder):
        self.folder = Path(folder)

    def save(self, identity, task, started, seconds):
        self.folder.mkdir(parents=True, exist_ok=True)
        target = self.folder / (identity + ".json")
        if target.exists():
            return
        temporary = target.with_suffix(".tmp")
        with temporary.open("w", encoding="utf-8") as output:
            json.dump(split_days(task, started, seconds), output, ensure_ascii=False)
            output.flush()
            os.fsync(output.fileno())
        os.replace(temporary, target)

    def report(self, day):
        totals = {}
        for path in self.folder.glob("*.json"):
            for entry in json.loads(path.read_text(encoding="utf-8")):
                if entry["Day"] == day:
                    totals[entry["Task"]] = totals.get(entry["Task"], 0) + entry["Seconds"]
        return sorted(totals.items(), key=lambda row: -row[1])


def format_time(seconds):
    h, tail = divmod(int(seconds), 3600)
    m, s = divmod(tail, 60)
    return f"{h:02}:{m:02}:{s:02}"
