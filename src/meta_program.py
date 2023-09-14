import os
from os import path

from watchdog.observers import Observer
from watchdog.events import LoggingEventHandler

def main():
    os.chdir(path.dirname(__file__))
    event_handler = LoggingEventHandler()
    observer = Observer()
    observer.schedule(event_handler, '.', recursive=True)
    observer.start()
    try:
        input()
    finally:
        observer.stop()
        observer.join()

main()
