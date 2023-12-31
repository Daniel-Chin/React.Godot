from __future__ import annotations

from typing import *
import os
from os import path
from time import sleep
from threading import Thread, Lock, Semaphore
from io import StringIO
from enum import Enum
import traceback

from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler, FileModifiedEvent
from tqdm import tqdm

QUEUE_MAX = 8
DONT_DELETE = "// Don't delete this line. "

class Stage(Enum):
    FIRST_LINE = 0
    WAITING_FOR_STATE_ATTRIBUTE = 1
    AT_STATE_ATTRIBUTE = 2
    WAITING_FOR_FORMAL_PARAM = 3
    AT_FORMAL_PARAM = 4
    WAITING_FOR_PROP_DIFF = 5
    AT_PROP_DIFF = 6
    TAIL = 7

class Prop:
    def __init__(self, type_: str, name: str) -> None:
        self.type = type_
        self.name = name
        if type_ in (
            'int', 'long', 'float', 'double', 'bool', 'string', 
        ):
            return
        if type_.startswith('State<') and type_.endswith('>.SetterType'):
            return
        if type_.startswith('CallbackType.'):
            return
        if type_.startswith('Action'):
            return
        if type_.startswith('Enum') or type_.endswith('Enum'):
            return
        print()
        print('Hint: Please name your enum types with "Enum" prefix or suffix.')
        print('Warning: Not a known immutable type:', type_)
    
    def asFormalParam(self):
        return f'{self.type} {self.name}_'
    
    def __str__(self):
        return self.name

class State:
    def __init__(self, type_: str, name: str) -> None:
        self.type = type_
        self.name = name

PRIVATE = 'private '
STATE = 'State<'
def validate(filename: str):
    if path.splitext(filename)[1].lower().strip().lstrip('.') != 'cs':
        return
    print('Validating:', filename)
    buf = StringIO()
    props: List[Prop] = []
    states: List[State] = []
    file_modified = False
    with open(filename, 'r', encoding='utf-8') as f:
        stage: Stage = Stage.FIRST_LINE
        def nextStage():
            nonlocal stage
            stage = Stage(stage.value + 1)
        do_expect_prop = False
        do_expect_state = False
        for line in f:
            if stage is Stage.FIRST_LINE:
                if not line.startswith('// META_PROGRAM: react.godot script.'):
                    return
                buf.write(line)
                nextStage()
            elif stage in (
                Stage.WAITING_FOR_STATE_ATTRIBUTE, 
                Stage.WAITING_FOR_FORMAL_PARAM, 
                Stage.WAITING_FOR_PROP_DIFF, 
                Stage.TAIL, 
            ):
                buf.write(line)
                line_stripped = line.strip()
                if do_expect_prop:
                    do_expect_prop = False
                    assert line_stripped.startswith(PRIVATE)
                    line_stripped = line_stripped[len(PRIVATE):]
                    type_, name = line_stripped.split(';', 1)[0].split(' ')
                    props.append(Prop(type_, name))
                    continue
                if do_expect_state:
                    do_expect_state = False
                    assert line_stripped.startswith(PRIVATE)
                    line_stripped = line_stripped[len(PRIVATE):]
                    state_type, name = line_stripped.split(';', 1)[0].split(' ')
                    assert state_type.startswith(STATE)
                    type_ = state_type[len(STATE):].rstrip('>')
                    states.append(State(type_, name))
                    continue
                if line_stripped.startswith("// Don't edit! Generated by meta programming."):
                    nextStage()
                elif line_stripped == '[ReactProp]':
                    do_expect_prop = True
                elif line_stripped == '[ReactState]':
                    do_expect_state = True
            elif stage is Stage.AT_STATE_ATTRIBUTE:
                lineBuf = StringIO()
                lineBuf.write(' ' * 4)
                for state in states:
                    lineBuf.write(PRIVATE)
                    lineBuf.write(state.type)
                    lineBuf.write(' P_')
                    lineBuf.write(state.name)
                    lineBuf.write(' { get => ')
                    lineBuf.write(state.name)
                    lineBuf.write('.Get(police); set { ')
                    lineBuf.write(state.name)
                    lineBuf.write('.Set(value); } } ')
                if not states:
                    lineBuf.write(DONT_DELETE)
                lineBuf.write('\n')
                lineBuf.seek(0)
                new_line = lineBuf.read()
                file_modified = file_modified or (new_line != line)
                buf.write(new_line)
                nextStage()
            elif stage is Stage.AT_FORMAL_PARAM:
                lineBuf = StringIO()
                lineBuf.write(' ' * 8)
                lineBuf.write(', '.join([x.asFormalParam() for x in props]))
                if not props:
                    lineBuf.write(DONT_DELETE)
                lineBuf.write('\n')
                lineBuf.seek(0)
                new_line = lineBuf.read()
                file_modified = file_modified or (new_line != line)
                buf.write(new_line)
                nextStage()
            elif stage is Stage.AT_PROP_DIFF:
                lineBuf = StringIO()
                lineBuf.write(' ' * 8)
                for prop in props:
                    lineBuf.write(f'if ({prop} != {prop}_) {{ {prop} = {prop}_; need_react = true; }} ')
                if not props:
                    lineBuf.write(DONT_DELETE)
                lineBuf.write('\n')
                lineBuf.seek(0)
                new_line = lineBuf.read()
                file_modified = file_modified or (new_line != line)
                buf.write(new_line)
                nextStage()
    assert stage is Stage.TAIL
    if not file_modified:
        return
    buf.seek(0)
    with open(filename, 'w', encoding='utf-8') as f:
        f.write(buf.read())

class Worker(Thread):
    def __init__(self, group: None = None, target: Callable[..., object] | None = None, name: str | None = None, args: Iterable[Any] = ..., kwargs: Mapping[str, Any] | None = None, *, daemon: bool | None = None) -> None:
        super().__init__(group, target, name, args, kwargs, daemon=daemon)
        self.todo: Set[str] = set()
        self.lock = Lock()
        self.barrier = Semaphore(QUEUE_MAX)
        for _ in range(QUEUE_MAX):
            self.barrier.acquire()
        self.do_stop = False
    
    def eat(self, filename: str):
        with self.lock:
            if filename not in self.todo:
                self.todo.add(filename)
                self.barrier.release()

    def run(self):
        while True:
            self.barrier.acquire()
            if self.do_stop:
                return
            sleep(.1)
            with self.lock:
                filename = self.todo.pop()
            try:
                validate(filename)
            except:
                traceback.print_exc()
    
    def stop(self):
        self.do_stop = True
        self.barrier.release()

class FileModifiedHandler(FileSystemEventHandler):
    def __init__(self) -> None:
        super().__init__()
        self.worker = Worker()
        self.worker.start()

    def on_modified(self, event):
        if type(event) is not FileModifiedEvent:
            return
        print('Modified:', event.src_path)
        self.worker.eat(event.src_path)

def main():
    os.chdir(path.dirname(path.abspath(__file__)))
    for filename in tqdm(os.listdir(), desc='Validating all'):
        validate(filename)
    fileModifiedHandler = FileModifiedHandler()
    observer = Observer()
    observer.schedule(fileModifiedHandler, '.', recursive=True)
    observer.start()
    print('Enter Ctrl+Z to quit.')
    print('Watchdog watching!')
    try:
        while True:
            input()
    except (KeyboardInterrupt, EOFError):
        pass
    finally:
        observer.stop()
        fileModifiedHandler.worker.stop()
        observer.join()
        fileModifiedHandler.worker.join()
        print('cleanup ok')

main()
