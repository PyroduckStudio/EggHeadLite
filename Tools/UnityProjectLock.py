"""Distinguish an active Unity lock from a leftover lock after a crash."""
import ctypes
import os

def is_open(project):
    lock = project/'Temp/UnityLockfile'
    if not lock.exists(): return False
    if os.name != 'nt': return True
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.CreateFileW.argtypes = [ctypes.c_wchar_p, ctypes.c_ulong, ctypes.c_ulong, ctypes.c_void_p, ctypes.c_ulong, ctypes.c_ulong, ctypes.c_void_p]
    kernel.CreateFileW.restype = ctypes.c_void_p
    handle = kernel.CreateFileW(str(lock), 0x80000000, 0, None, 3, 0x80, None)
    if handle == ctypes.c_void_p(-1).value:
        if ctypes.get_last_error() in (32, 33): return True
        raise OSError(ctypes.get_last_error(), 'Cannot inspect Unity project lock')
    kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    kernel.CloseHandle(handle)
    return False
