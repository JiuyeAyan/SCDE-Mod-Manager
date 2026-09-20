#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include "CrashHandler.h"

#include <DbgHelp.h>
#include <intrin.h>

#define SHCDESE_ARRAY_COUNT(value) (sizeof(value) / sizeof((value)[0]))

namespace
{
    constexpr size_t kPathCapacity = 32768;
    constexpr DWORD kMaximumFrames = 64;
    constexpr DWORD kStackWords = 32;

    wchar_t g_bepInExLogPath[kPathCapacity]{};
    wchar_t g_dumpDirectory[kPathCapacity]{};
    wchar_t g_crashLogPath[kPathCapacity]{};
    wchar_t g_minidumpPath[kPathCapacity]{};
    SRWLOCK g_installLock = SRWLOCK_INIT;
    PVOID g_vectoredHandler = nullptr;
    volatile LONG g_handlingCrash = 0;

    struct OutputFiles
    {
        HANDLE bepInExLog = INVALID_HANDLE_VALUE;
        HANDLE crashLog = INVALID_HANDLE_VALUE;
    };

    size_t StringLength(const wchar_t* value)
    {
        size_t length = 0;
        if (value != nullptr)
        {
            while (value[length] != L'\0')
                ++length;
        }
        return length;
    }

    bool CopyString(wchar_t* destination, size_t capacity, const wchar_t* source)
    {
        if (destination == nullptr || capacity == 0 || source == nullptr)
            return false;

        const size_t length = StringLength(source);
        if (length >= capacity)
            return false;

        for (size_t i = 0; i <= length; ++i)
            destination[i] = source[i];
        return true;
    }

    bool JoinPath(wchar_t* destination, size_t capacity, const wchar_t* directory, const wchar_t* name)
    {
        const size_t directoryLength = StringLength(directory);
        const size_t nameLength = StringLength(name);
        const bool needsSeparator = directoryLength != 0 &&
            directory[directoryLength - 1] != L'\\' && directory[directoryLength - 1] != L'/';
        const size_t totalLength = directoryLength + (needsSeparator ? 1 : 0) + nameLength;
        if (destination == nullptr || directory == nullptr || name == nullptr || totalLength >= capacity)
            return false;

        size_t position = 0;
        for (size_t i = 0; i < directoryLength; ++i)
            destination[position++] = directory[i];
        if (needsSeparator)
            destination[position++] = L'\\';
        for (size_t i = 0; i < nameLength; ++i)
            destination[position++] = name[i];
        destination[position] = L'\0';
        return true;
    }

    HANDLE OpenAppendFile(const wchar_t* path)
    {
        return CreateFileW(path, FILE_APPEND_DATA | SYNCHRONIZE,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_ALWAYS,
            FILE_ATTRIBUTE_NORMAL, nullptr);
    }

    void WriteBytes(HANDLE file, const char* data, DWORD length)
    {
        if (file == INVALID_HANDLE_VALUE || data == nullptr || length == 0)
            return;

        DWORD written = 0;
        WriteFile(file, data, length, &written, nullptr);
    }

    void WriteBoth(const OutputFiles& files, const char* text)
    {
        if (text == nullptr)
            return;
        DWORD length = 0;
        while (text[length] != '\0')
            ++length;
        WriteBytes(files.bepInExLog, text, length);
        WriteBytes(files.crashLog, text, length);
    }

    void WriteHex(const OutputFiles& files, unsigned long long value, DWORD digits = 16)
    {
        static constexpr char alphabet[] = "0123456789ABCDEF";
        char buffer[18] = { '0', 'x' };
        if (digits > 16)
            digits = 16;
        for (DWORD i = 0; i < digits; ++i)
        {
            const DWORD shift = (digits - i - 1) * 4;
            buffer[i + 2] = alphabet[(value >> shift) & 0xF];
        }
        WriteBytes(files.bepInExLog, buffer, digits + 2);
        WriteBytes(files.crashLog, buffer, digits + 2);
    }

    void WriteUnsigned(const OutputFiles& files, unsigned long long value)
    {
        char buffer[24]{};
        DWORD position = static_cast<DWORD>(SHCDESE_ARRAY_COUNT(buffer));
        do
        {
            buffer[--position] = static_cast<char>('0' + (value % 10));
            value /= 10;
        } while (value != 0 && position != 0);
        WriteBytes(files.bepInExLog, buffer + position, static_cast<DWORD>(SHCDESE_ARRAY_COUNT(buffer)) - position);
        WriteBytes(files.crashLog, buffer + position, static_cast<DWORD>(SHCDESE_ARRAY_COUNT(buffer)) - position);
    }

    void WriteWide(const OutputFiles& files, const wchar_t* text)
    {
        if (text == nullptr)
            return;
        char buffer[2048]{};
        const int converted = WideCharToMultiByte(CP_UTF8, 0, text, -1, buffer,
            static_cast<int>(SHCDESE_ARRAY_COUNT(buffer)), nullptr, nullptr);
        if (converted > 1)
        {
            WriteBytes(files.bepInExLog, buffer, static_cast<DWORD>(converted - 1));
            WriteBytes(files.crashLog, buffer, static_cast<DWORD>(converted - 1));
        }
    }

    const char* ExceptionName(DWORD code)
    {
        switch (code)
        {
            case EXCEPTION_ACCESS_VIOLATION: return "EXCEPTION_ACCESS_VIOLATION";
            case EXCEPTION_ARRAY_BOUNDS_EXCEEDED: return "EXCEPTION_ARRAY_BOUNDS_EXCEEDED";
            case EXCEPTION_BREAKPOINT: return "EXCEPTION_BREAKPOINT";
            case EXCEPTION_DATATYPE_MISALIGNMENT: return "EXCEPTION_DATATYPE_MISALIGNMENT";
            case EXCEPTION_FLT_DENORMAL_OPERAND: return "EXCEPTION_FLT_DENORMAL_OPERAND";
            case EXCEPTION_FLT_DIVIDE_BY_ZERO: return "EXCEPTION_FLT_DIVIDE_BY_ZERO";
            case EXCEPTION_FLT_INEXACT_RESULT: return "EXCEPTION_FLT_INEXACT_RESULT";
            case EXCEPTION_FLT_INVALID_OPERATION: return "EXCEPTION_FLT_INVALID_OPERATION";
            case EXCEPTION_FLT_OVERFLOW: return "EXCEPTION_FLT_OVERFLOW";
            case EXCEPTION_FLT_STACK_CHECK: return "EXCEPTION_FLT_STACK_CHECK";
            case EXCEPTION_FLT_UNDERFLOW: return "EXCEPTION_FLT_UNDERFLOW";
            case EXCEPTION_ILLEGAL_INSTRUCTION: return "EXCEPTION_ILLEGAL_INSTRUCTION";
            case EXCEPTION_IN_PAGE_ERROR: return "EXCEPTION_IN_PAGE_ERROR";
            case EXCEPTION_INT_DIVIDE_BY_ZERO: return "EXCEPTION_INT_DIVIDE_BY_ZERO";
            case EXCEPTION_INT_OVERFLOW: return "EXCEPTION_INT_OVERFLOW";
            case EXCEPTION_INVALID_DISPOSITION: return "EXCEPTION_INVALID_DISPOSITION";
            case EXCEPTION_NONCONTINUABLE_EXCEPTION: return "EXCEPTION_NONCONTINUABLE_EXCEPTION";
            case EXCEPTION_PRIV_INSTRUCTION: return "EXCEPTION_PRIV_INSTRUCTION";
            case EXCEPTION_SINGLE_STEP: return "EXCEPTION_SINGLE_STEP";
            case EXCEPTION_STACK_OVERFLOW: return "EXCEPTION_STACK_OVERFLOW";
            default: return "UNKNOWN_EXCEPTION";
        }
    }

    bool ResolveAddress(DWORD64 address, wchar_t* modulePath, size_t modulePathCapacity,
        DWORD64& moduleBase, DWORD64& rva, DWORD64& fileOffset, bool& hasFileOffset)
    {
        modulePath[0] = L'\0';
        moduleBase = 0;
        rva = 0;
        fileOffset = 0;
        hasFileOffset = false;

        MEMORY_BASIC_INFORMATION memoryInfo{};
        if (VirtualQuery(reinterpret_cast<LPCVOID>(address), &memoryInfo, sizeof(memoryInfo)) == 0 ||
            memoryInfo.AllocationBase == nullptr)
            return false;

        const auto module = static_cast<HMODULE>(memoryInfo.AllocationBase);
        const DWORD modulePathLength = GetModuleFileNameW(module, modulePath, static_cast<DWORD>(modulePathCapacity));
        if (modulePathLength == 0)
            return false;
        modulePath[modulePathCapacity - 1] = L'\0';

        moduleBase = reinterpret_cast<DWORD64>(module);
        if (address < moduleBase)
            return false;
        rva = address - moduleBase;

        __try
        {
            const auto dosHeader = reinterpret_cast<const IMAGE_DOS_HEADER*>(module);
            if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
                return true;
            const auto ntHeaders = reinterpret_cast<const IMAGE_NT_HEADERS64*>(reinterpret_cast<const BYTE*>(module) + dosHeader->e_lfanew);
            if (ntHeaders->Signature != IMAGE_NT_SIGNATURE ||
                ntHeaders->OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC)
                return true;

            if (rva < ntHeaders->OptionalHeader.SizeOfHeaders)
            {
                fileOffset = rva;
                hasFileOffset = true;
                return true;
            }

            const IMAGE_SECTION_HEADER* section = IMAGE_FIRST_SECTION(ntHeaders);
            for (WORD i = 0; i < ntHeaders->FileHeader.NumberOfSections; ++i, ++section)
            {
                const DWORD64 sectionStart = section->VirtualAddress;
                const DWORD64 sectionSpan = section->Misc.VirtualSize > section->SizeOfRawData ? section->Misc.VirtualSize : section->SizeOfRawData;
                if (rva >= sectionStart && rva < sectionStart + sectionSpan)
                {
                    const DWORD64 offsetInSection = rva - sectionStart;
                    if (offsetInSection < section->SizeOfRawData)
                    {
                        fileOffset = section->PointerToRawData + offsetInSection;
                        hasFileOffset = true;
                    }
                    break;
                }
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            hasFileOffset = false;
        }
        return true;
    }

    void WriteAddress(const OutputFiles& files, DWORD64 address)
    {
        WriteHex(files, address);

        wchar_t modulePath[MAX_PATH]{};
        DWORD64 moduleBase = 0;
        DWORD64 rva = 0;
        DWORD64 fileOffset = 0;
        bool hasFileOffset = false;
        if (!ResolveAddress(address, modulePath, SHCDESE_ARRAY_COUNT(modulePath), moduleBase, rva, fileOffset, hasFileOffset))
            return;

        const wchar_t* fileName = modulePath;
        for (const wchar_t* cursor = modulePath; *cursor != L'\0'; ++cursor)
        {
            if (*cursor == L'\\' || *cursor == L'/')
                fileName = cursor + 1;
        }

        WriteBoth(files, " (");
        WriteWide(files, fileName);
        WriteBoth(files, "+");
        WriteHex(files, rva);
        if (hasFileOffset)
        {
            WriteBoth(files, ", file+");
            WriteHex(files, fileOffset);
        }
        WriteBoth(files, ")");
    }

    void WriteRegister(const OutputFiles& files, const char* name, DWORD64 value)
    {
        WriteBoth(files, name);
        WriteBoth(files, "=");
        WriteHex(files, value);
        WriteBoth(files, "  ");
    }

    void WriteRegisters(const OutputFiles& files, const CONTEXT& context)
    {
        WriteBoth(files, "Registers (x64):\r\n");
        WriteRegister(files, "RIP", context.Rip); WriteRegister(files, "RSP", context.Rsp);
        WriteRegister(files, "RBP", context.Rbp); WriteRegister(files, "EFLAGS", context.EFlags);
        WriteBoth(files, "\r\n");
        WriteRegister(files, "RAX", context.Rax); WriteRegister(files, "RBX", context.Rbx);
        WriteRegister(files, "RCX", context.Rcx); WriteRegister(files, "RDX", context.Rdx);
        WriteBoth(files, "\r\n");
        WriteRegister(files, "RSI", context.Rsi); WriteRegister(files, "RDI", context.Rdi);
        WriteRegister(files, "R8 ", context.R8);  WriteRegister(files, "R9 ", context.R9);
        WriteBoth(files, "\r\n");
        WriteRegister(files, "R10", context.R10); WriteRegister(files, "R11", context.R11);
        WriteRegister(files, "R12", context.R12); WriteRegister(files, "R13", context.R13);
        WriteBoth(files, "\r\n");
        WriteRegister(files, "R14", context.R14); WriteRegister(files, "R15", context.R15);
        WriteRegister(files, "MXCSR", context.MxCsr);
        WriteBoth(files, "\r\n");
    }

    bool ReadPointer(DWORD64 address, DWORD64& value)
    {
        SIZE_T bytesRead = 0;
        return ReadProcessMemory(GetCurrentProcess(), reinterpret_cast<LPCVOID>(address),
            &value, sizeof(value), &bytesRead) != FALSE && bytesRead == sizeof(value);
    }

    void WriteStackWords(const OutputFiles& files, const CONTEXT& context)
    {
        WriteBoth(files, "Stack memory from RSP:\r\n");
        for (DWORD i = 0; i < kStackWords; ++i)
        {
            const DWORD64 slotAddress = context.Rsp + static_cast<DWORD64>(i) * sizeof(DWORD64);
            DWORD64 value = 0;
            if (!ReadPointer(slotAddress, value))
            {
                WriteBoth(files, "  <unreadable at ");
                WriteHex(files, slotAddress);
                WriteBoth(files, ">\r\n");
                break;
            }
            WriteBoth(files, "  [RSP+");
            WriteHex(files, static_cast<DWORD64>(i) * sizeof(DWORD64), 4);
            WriteBoth(files, "] ");
            WriteHex(files, value);

            wchar_t modulePath[MAX_PATH]{};
            DWORD64 moduleBase = 0, rva = 0, fileOffset = 0;
            bool hasFileOffset = false;
            if (value != 0 && ResolveAddress(value, modulePath, SHCDESE_ARRAY_COUNT(modulePath), moduleBase, rva, fileOffset, hasFileOffset))
            {
                WriteBoth(files, " -> ");
                WriteAddress(files, value);
            }
            WriteBoth(files, "\r\n");
        }
    }

    bool UnwindOneFrame(CONTEXT& context)
    {
        const DWORD64 oldRip = context.Rip;
        const DWORD64 oldRsp = context.Rsp;
        bool result = false;

        __try
        {
            DWORD64 imageBase = 0;
            PRUNTIME_FUNCTION functionEntry = RtlLookupFunctionEntry(context.Rip, &imageBase, nullptr);
            if (functionEntry != nullptr)
            {
                PVOID handlerData = nullptr;
                DWORD64 establisherFrame = 0;
                KNONVOLATILE_CONTEXT_POINTERS contextPointers{};
                RtlVirtualUnwind(UNW_FLAG_NHANDLER, imageBase, context.Rip, functionEntry, &context, &handlerData, &establisherFrame, &contextPointers);
                result = true;
            }
            else
            {
                DWORD64 returnAddress = 0;
                if (ReadPointer(context.Rsp, returnAddress))
                {
                    context.Rip = returnAddress;
                    context.Rsp += sizeof(DWORD64);
                    result = true;
                }
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return false;
        }

        return result && context.Rip != 0 && context.Rip != oldRip && context.Rsp > oldRsp;
    }

    void WriteBacktrace(const OutputFiles& files, const CONTEXT& originalContext)
    {
        WriteBoth(files, "Native backtrace (module+RVA, PE file offset when mapped):\r\n");
        CONTEXT context = originalContext;
        for (DWORD frame = 0; frame < kMaximumFrames && context.Rip != 0; ++frame)
        {
            WriteBoth(files, "  #");
            WriteUnsigned(files, frame);
            WriteBoth(files, " ");
            WriteAddress(files, context.Rip);
            WriteBoth(files, "\r\n");
            if (!UnwindOneFrame(context))
                break;
        }
    }

    void WriteExceptionDetails(const OutputFiles& files, const EXCEPTION_RECORD& record)
    {
        WriteBoth(files, "Exception: ");
        WriteBoth(files, ExceptionName(record.ExceptionCode));
        WriteBoth(files, " (");
        WriteHex(files, record.ExceptionCode, 8);
        WriteBoth(files, ") at ");
        WriteAddress(files, reinterpret_cast<DWORD64>(record.ExceptionAddress));
        WriteBoth(files, "\r\nFlags: ");
        WriteHex(files, record.ExceptionFlags, 8);
        WriteBoth(files, "\r\n");

        if ((record.ExceptionCode == EXCEPTION_ACCESS_VIOLATION ||
            record.ExceptionCode == EXCEPTION_IN_PAGE_ERROR) && record.NumberParameters >= 2)
        {
            const ULONG_PTR operation = record.ExceptionInformation[0];
            const char* operationName = operation == 0 ? "read" : (operation == 1 ? "write" : (operation == 8 ? "execute" : "unknown"));
            WriteBoth(files, "Access: ");
            WriteBoth(files, operationName);
            WriteBoth(files, " at virtual address ");
            WriteHex(files, record.ExceptionInformation[1]);
            WriteBoth(files, "\r\n");
            if (record.ExceptionCode == EXCEPTION_IN_PAGE_ERROR && record.NumberParameters >= 3)
            {
                WriteBoth(files, "Underlying NTSTATUS: ");
                WriteHex(files, record.ExceptionInformation[2], 8);
                WriteBoth(files, "\r\n");
            }
        }
    }

    bool IsReportableNativeException(DWORD exceptionCode)
    {
        // Restrict dump generation to exception classes that normally indicate a native process failure.
        switch (exceptionCode)
        {
        case EXCEPTION_ACCESS_VIOLATION:
        case EXCEPTION_IN_PAGE_ERROR:
        case EXCEPTION_ARRAY_BOUNDS_EXCEEDED:
        case EXCEPTION_DATATYPE_MISALIGNMENT:
        case EXCEPTION_FLT_DENORMAL_OPERAND:
        case EXCEPTION_FLT_DIVIDE_BY_ZERO:
        case EXCEPTION_FLT_INEXACT_RESULT:
        case EXCEPTION_FLT_INVALID_OPERATION:
        case EXCEPTION_FLT_OVERFLOW:
        case EXCEPTION_FLT_STACK_CHECK:
        case EXCEPTION_FLT_UNDERFLOW:
        case EXCEPTION_ILLEGAL_INSTRUCTION:
        case EXCEPTION_INT_DIVIDE_BY_ZERO:
        case EXCEPTION_INT_OVERFLOW:
        case EXCEPTION_INVALID_DISPOSITION:
        case EXCEPTION_NONCONTINUABLE_EXCEPTION:
        case EXCEPTION_PRIV_INSTRUCTION:
        case EXCEPTION_STACK_OVERFLOW:
        case 0xC0000008UL: // STATUS_INVALID_HANDLE
        case 0xC0000374UL: // STATUS_HEAP_CORRUPTION
        case 0xC0000409UL: // STATUS_STACK_BUFFER_OVERRUN / fail-fast
            return true;
        default:
            return false;
        }
    }

    void BuildTimestampedName(wchar_t* destination, size_t capacity, const wchar_t* extension)
    {
        SYSTEMTIME time{};
        GetLocalTime(&time);
        wchar_t name[128]{};
        // Fixed-width conversion without depending on locale or allocating memory.
        wchar_t* cursor = name;
        const auto appendNumber = [&cursor](unsigned value, unsigned digits)
            {
                for (unsigned i = 0; i < digits; ++i)
                {
                    const unsigned divisor = i == 0 && digits == 4 ? 1000 :
                        (digits - i == 3 ? 100 : (digits - i == 2 ? 10 : 1));
                    *cursor++ = static_cast<wchar_t>(L'0' + ((value / divisor) % 10));
                }
            };
        const wchar_t prefix[] = L"SHCDESE-crash-";
        for (const wchar_t ch : prefix)
        {
            if (ch != L'\0') *cursor++ = ch;
        }
        appendNumber(time.wYear, 4); *cursor++ = L'-';
        appendNumber(time.wMonth, 2); *cursor++ = L'-';
        appendNumber(time.wDay, 2); *cursor++ = L'-';
        appendNumber(time.wHour, 2); *cursor++ = L'-';
        appendNumber(time.wMinute, 2); *cursor++ = L'-';
        appendNumber(time.wSecond, 2); *cursor++ = L'-';
        appendNumber(time.wMilliseconds, 3); *cursor++ = L'-';

        DWORD pid = GetCurrentProcessId();
        DWORD tid = GetCurrentThreadId();
        const wchar_t pidLabel[] = L"pid";
        for (const wchar_t ch : pidLabel) 
            if (ch != L'\0') *cursor++ = ch;

        wchar_t numberBuffer[16]{};
        wchar_t* numberEnd = numberBuffer + SHCDESE_ARRAY_COUNT(numberBuffer);
        wchar_t* numberStart = numberEnd;

        do 
        { 
            *--numberStart = static_cast<wchar_t>(L'0' + pid % 10); 
            pid /= 10; 
        } 
        while (pid != 0);

        while (numberStart != numberEnd) 
            *cursor++ = *numberStart++;

        *cursor++ = L'-';
        const wchar_t tidLabel[] = L"tid";
        for (const wchar_t ch : tidLabel) 
            if (ch != L'\0') 
                *cursor++ = ch;

        numberEnd = numberBuffer + SHCDESE_ARRAY_COUNT(numberBuffer);
        numberStart = numberEnd;

        do 
        { 
            *--numberStart = static_cast<wchar_t>(L'0' + tid % 10); tid /= 10; 
        } 
        while (tid != 0);

        while (numberStart != numberEnd) 
            *cursor++ = *numberStart++;

        for (const wchar_t* ch = extension; *ch != L'\0'; ++ch) 
            *cursor++ = *ch;
        *cursor = L'\0';

        JoinPath(destination, capacity, g_dumpDirectory, name);
    }

    OutputFiles OpenOutputFiles(wchar_t* crashLogPath, size_t crashLogPathCapacity)
    {
        OutputFiles files{};
        files.bepInExLog = OpenAppendFile(g_bepInExLogPath);
        BuildTimestampedName(crashLogPath, crashLogPathCapacity, L".log");
        files.crashLog = OpenAppendFile(crashLogPath);
        return files;
    }

    bool WriteMinidump(EXCEPTION_POINTERS* exceptionPointers, wchar_t* dumpPath, size_t dumpPathCapacity,
        DWORD& error)
    {
        BuildTimestampedName(dumpPath, dumpPathCapacity, L".dmp");
        HANDLE dumpFile = CreateFileW(dumpPath, GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (dumpFile == INVALID_HANDLE_VALUE)
        {
            error = GetLastError();
            return false;
        }

        MINIDUMP_EXCEPTION_INFORMATION exceptionInformation{};
        exceptionInformation.ThreadId = GetCurrentThreadId();
        exceptionInformation.ExceptionPointers = exceptionPointers;
        exceptionInformation.ClientPointers = FALSE;

        const MINIDUMP_TYPE dumpType = static_cast<MINIDUMP_TYPE>(
            MiniDumpNormal |
            MiniDumpWithDataSegs |
            MiniDumpWithHandleData |
            MiniDumpWithUnloadedModules |
            MiniDumpWithIndirectlyReferencedMemory |
            MiniDumpWithThreadInfo);
        const BOOL succeeded = MiniDumpWriteDump(GetCurrentProcess(), GetCurrentProcessId(), dumpFile,
            dumpType, &exceptionInformation, nullptr, nullptr);
        error = succeeded ? ERROR_SUCCESS : GetLastError();
        FlushFileBuffers(dumpFile);
        CloseHandle(dumpFile);
        return succeeded != FALSE;
    }

    LONG CALLBACK NativeVectoredExceptionHandler(EXCEPTION_POINTERS* exceptionPointers)
    {
        if (exceptionPointers == nullptr || exceptionPointers->ExceptionRecord == nullptr || exceptionPointers->ContextRecord == nullptr)
            return EXCEPTION_CONTINUE_SEARCH;
        if (!IsReportableNativeException(exceptionPointers->ExceptionRecord->ExceptionCode))
            return EXCEPTION_CONTINUE_SEARCH;
        if (InterlockedCompareExchange(&g_handlingCrash, 1, 0) != 0)
            return EXCEPTION_CONTINUE_SEARCH;

        OutputFiles files = OpenOutputFiles(g_crashLogPath, SHCDESE_ARRAY_COUNT(g_crashLogPath));
        WriteBoth(files, "\r\n================ SHCDE-SE native crash report ================\r\n");
        WriteBoth(files, "Process ID: "); WriteUnsigned(files, GetCurrentProcessId());
        WriteBoth(files, "  Thread ID: "); WriteUnsigned(files, GetCurrentThreadId());
        WriteBoth(files, "\r\n");
        WriteExceptionDetails(files, *exceptionPointers->ExceptionRecord);
        WriteRegisters(files, *exceptionPointers->ContextRecord);
        WriteBacktrace(files, *exceptionPointers->ContextRecord);
        WriteStackWords(files, *exceptionPointers->ContextRecord);

        DWORD dumpError = ERROR_SUCCESS;
        const bool dumped = WriteMinidump(exceptionPointers, g_minidumpPath, SHCDESE_ARRAY_COUNT(g_minidumpPath), dumpError);
        WriteBoth(files, "Minidump: ");
        if (dumped)
            WriteWide(files, g_minidumpPath);
        else
        {
            WriteBoth(files, "FAILED (Win32 error ");
            WriteUnsigned(files, dumpError);
            WriteBoth(files, ")");
        }
        WriteBoth(files, "\r\nStandalone report: ");
        WriteWide(files, g_crashLogPath);
        WriteBoth(files, "\r\n===============================================================\r\n");

        if (files.bepInExLog != INVALID_HANDLE_VALUE)
        {
            FlushFileBuffers(files.bepInExLog);
            CloseHandle(files.bepInExLog);
        }
        if (files.crashLog != INVALID_HANDLE_VALUE)
        {
            FlushFileBuffers(files.crashLog);
            CloseHandle(files.crashLog);
        }

        InterlockedExchange(&g_handlingCrash, 0);
        return EXCEPTION_CONTINUE_SEARCH;
    }
}

DWORD WINAPI SHCDECrashHandler_Install(const wchar_t* bepInExLogPath, const wchar_t* dumpDirectory)
{
    if (bepInExLogPath == nullptr || dumpDirectory == nullptr ||
        *bepInExLogPath == L'\0' || *dumpDirectory == L'\0')
        return ERROR_INVALID_PARAMETER;

    AcquireSRWLockExclusive(&g_installLock);
    if (g_vectoredHandler != nullptr)
    {
        ReleaseSRWLockExclusive(&g_installLock);
        return ERROR_SUCCESS;
    }

    if (!CopyString(g_bepInExLogPath, SHCDESE_ARRAY_COUNT(g_bepInExLogPath), bepInExLogPath) ||
        !CopyString(g_dumpDirectory, SHCDESE_ARRAY_COUNT(g_dumpDirectory), dumpDirectory))
    {
        ReleaseSRWLockExclusive(&g_installLock);
        return ERROR_FILENAME_EXCED_RANGE;
    }

    if (!CreateDirectoryW(g_dumpDirectory, nullptr))
    {
        const DWORD error = GetLastError();
        if (error != ERROR_ALREADY_EXISTS)
        {
            ReleaseSRWLockExclusive(&g_installLock);
            return error;
        }
    }

    g_vectoredHandler = AddVectoredExceptionHandler(1, NativeVectoredExceptionHandler);
    if (g_vectoredHandler == nullptr)
    {
        DWORD error = GetLastError();
        if (error == ERROR_SUCCESS)
            error = ERROR_GEN_FAILURE;
        ReleaseSRWLockExclusive(&g_installLock);
        return error;
    }

    ReleaseSRWLockExclusive(&g_installLock);
    return ERROR_SUCCESS;
}

DWORD WINAPI SHCDECrashHandler_Uninstall()
{
    AcquireSRWLockExclusive(&g_installLock);
    if (g_vectoredHandler == nullptr)
    {
        ReleaseSRWLockExclusive(&g_installLock);
        return ERROR_SUCCESS;
    }

    if (RemoveVectoredExceptionHandler(g_vectoredHandler) == 0)
    {
        DWORD error = GetLastError();
        if (error == ERROR_SUCCESS)
            error = ERROR_INVALID_HANDLE;
        ReleaseSRWLockExclusive(&g_installLock);
        return error;
    }

    g_vectoredHandler = nullptr;
    ReleaseSRWLockExclusive(&g_installLock);
    return ERROR_SUCCESS;
}
