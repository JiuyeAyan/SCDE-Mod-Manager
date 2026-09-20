#pragma once

#include <Windows.h>

#ifdef SHCDESE_NATIVE_CRASH_HANDLER_EXPORTS
#define SHCDESE_CRASH_API extern "C" __declspec(dllexport)
#else
#define SHCDESE_CRASH_API extern "C" __declspec(dllimport)
#endif

// Installs a first-priority process-wide vectored exception handler. Both paths are copied by the native DLL and may be released by the caller as soon as this returns. 
// Repeated calls are idempotent.
// Returns ERROR_SUCCESS or a Win32 error code.
SHCDESE_CRASH_API DWORD WINAPI SHCDECrashHandler_Install(
    _In_z_ const wchar_t* bepInExLogPath,
    _In_z_ const wchar_t* dumpDirectory);

// Removes the vectored exception handler installed by SHCDECrashHandler_Install. 
// Repeated calls are idempotent.
// Returns ERROR_SUCCESS or a Win32 error code.
SHCDESE_CRASH_API DWORD WINAPI SHCDECrashHandler_Uninstall();
