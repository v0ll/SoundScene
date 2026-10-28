#include "soundscene_dax.h"

#include <windows.h>
#include <appmodel.h>
#include <roapi.h>
#include <unknwn.h>
#include <winstring.h>

#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>

namespace {

constexpr const wchar_t* kClassName = L"DAX.CDolbyDAX";
constexpr UINT32 kClassNameLen = 13;

using DllGetActivationFactoryFn = HRESULT(WINAPI*)(HSTRING, void**);

void Append(std::string& out, const char* fmt, ...)
{
    char tmp[4096];
    va_list args;
    va_start(args, fmt);
    vsnprintf_s(tmp, sizeof(tmp), _TRUNCATE, fmt, args);
    va_end(args);
    out += tmp;
}

std::wstring Widen(const char* utf8)
{
    if (utf8 == nullptr || *utf8 == '\0')
    {
        return {};
    }

    const int need = MultiByteToWideChar(CP_UTF8, 0, utf8, -1, nullptr, 0);
    if (need <= 1)
    {
        return {};
    }

    std::wstring wide(static_cast<size_t>(need - 1), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, utf8, -1, wide.data(), need);
    return wide;
}

const char* HresultName(HRESULT hr)
{
    switch (static_cast<unsigned long>(hr))
    {
    case 0x00000000UL: return "S_OK";
    case 0x00000001UL: return "S_FALSE";
    case 0x80040154UL: return "REGDB_E_CLASSNOTREG";
    case 0x80040111UL: return "CLASS_E_CLASSNOTAVAILABLE";
    case 0x80070005UL: return "E_ACCESSDENIED";
    case 0x8007007EUL: return "ERROR_MOD_NOT_FOUND";
    case 0x800700C1UL: return "ERROR_BAD_EXE_FORMAT";
    case 0x8007000EUL: return "E_OUTOFMEMORY";
    case 0x80004005UL: return "E_FAIL";
    case 0x80004002UL: return "E_NOINTERFACE";
    case 0x80070032UL: return "ERROR_NOT_SUPPORTED";
    case 0x800706BAUL: return "RPC_S_SERVER_UNAVAILABLE";
    case 0x80010106UL: return "RPC_E_CHANGED_MODE";
    default: return "unknown";
    }
}

const char* HresultMeaning(HRESULT hr)
{
    switch (static_cast<unsigned long>(hr))
    {
    case 0x80040154UL: return "class not registered for this process - package boundary";
    case 0x80070005UL: return "blocked by ACL, capability, or code integrity policy";
    case 0x8007007EUL: return "a dependent DLL was not found (likely the *_app CRT)";
    case 0x80004005UL: return "generic failure - usually the RPC endpoint is not open";
    case 0x80070032UL: return "API present but rejected for this caller";
    case 0x800706BAUL: return "DolbyDAXAPI service not reachable";
    case 0x00000000UL: return "success";
    default: return "";
    }
}

void AppendHresult(std::string& out, const char* label, HRESULT hr)
{
    Append(out, "%-14s: 0x%08X %s", label, static_cast<unsigned long>(hr), HresultName(hr));
    const char* meaning = HresultMeaning(hr);
    if (meaning[0] != '\0')
    {
        Append(out, "  (%s)", meaning);
    }
    Append(out, "\r\n");
}

std::string Narrow(const std::wstring& wide)
{
    if (wide.empty())
    {
        return {};
    }

    const int need = WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (need <= 1)
    {
        return {};
    }

    std::string narrow(static_cast<size_t>(need - 1), '\0');
    WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), -1, narrow.data(), need, nullptr, nullptr);
    return narrow;
}

} // namespace

SS_API ss_result ss_probe_activation(const char* daxClientDll,
                                     const char* vclibsDir,
                                     char* report,
                                     uint32_t reportLen)
{
    if (report == nullptr || reportLen == 0)
    {
        return -1;
    }

    std::string out;
    out.reserve(8192);

    // ---------------------------------------------------------------- identity
    UINT32 packageFullNameLen = 0;
    const LONG packageResult = GetCurrentPackageFullName(&packageFullNameLen, nullptr);
    Append(out, "process        : %s\r\n",
           (packageResult == APPMODEL_ERROR_NO_PACKAGE) ? "unpackaged (desktop)" : "packaged (MSIX)");

    HRESULT hrInit = RoInitialize(RO_INIT_MULTITHREADED);
    if (hrInit == RPC_E_CHANGED_MODE)
    {
        hrInit = S_OK; // already initialised on this thread with a different model
    }
    AppendHresult(out, "RoInitialize", hrInit);
    Append(out, "\r\n");

    // ------------------------------------------------------------- attempt A
    Append(out, "[A] RoGetActivationFactory(\"%ls\")\r\n", kClassName);

    HSTRING className = nullptr;
    HRESULT hrCreate = WindowsCreateString(kClassName, kClassNameLen, &className);
    if (FAILED(hrCreate))
    {
        AppendHresult(out, "  create HSTRING", hrCreate);
        Append(out, "\r\n");
    }
    else
    {
        void* factory = nullptr;
        const HRESULT hrA = RoGetActivationFactory(className, IID_IUnknown, &factory);
        AppendHresult(out, "  result", hrA);

        if (SUCCEEDED(hrA) && factory != nullptr)
        {
            Append(out, "  factory        : %p\r\n", factory);
            reinterpret_cast<IUnknown*>(factory)->Release();
        }
        WindowsDeleteString(className);
        Append(out, "\r\n");
    }

    // ------------------------------------------------------------- attempt B
    const std::wstring daxPath = Widen(daxClientDll);
    const std::wstring vclPath = Widen(vclibsDir);

    Append(out, "[B] LoadLibraryEx + DllGetActivationFactory\r\n");

    if (daxPath.empty())
    {
        Append(out, "  skipped        : no path supplied\r\n\r\n");
    }
    else
    {
        const DWORD attrs = GetFileAttributesW(daxPath.c_str());
        Append(out, "  dll exists     : %s\r\n",
               (attrs != INVALID_FILE_ATTRIBUTES) ? "yes" : "no");

        // B1 - default search only, so we can see the *_app CRT dependency fail.
        SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS);

        HMODULE plain = LoadLibraryExW(daxPath.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        if (plain != nullptr)
        {
            Append(out, "  B1 (no vclibs) : loaded %p\r\n", plain);
        }
        else
        {
            const DWORD err = GetLastError();
            AppendHresult(out, "  B1 (no vclibs)", HRESULT_FROM_WIN32(err));
        }

        // B2 - add the AppContainer CRT directory, then retry.
        DLL_DIRECTORY_COOKIE cookie = nullptr;
        if (!vclPath.empty())
        {
            cookie = AddDllDirectory(vclPath.c_str());
            Append(out, "  AddDllDirectory: %s\r\n", (cookie != nullptr) ? "ok" : "failed");
        }

        HMODULE dax = plain;
        if (dax == nullptr)
        {
            dax = LoadLibraryExW(daxPath.c_str(), nullptr,
                                 LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS);
            if (dax != nullptr)
            {
                Append(out, "  B2 (vclibs)    : loaded %p\r\n", dax);
            }
            else
            {
                AppendHresult(out, "  B2 (vclibs)", HRESULT_FROM_WIN32(GetLastError()));
            }
        }

        if (dax != nullptr)
        {
            auto getFactory = reinterpret_cast<DllGetActivationFactoryFn>(
                GetProcAddress(dax, "DllGetActivationFactory"));

            if (getFactory == nullptr)
            {
                Append(out, "  export lookup  : DllGetActivationFactory not found\r\n");
            }
            else
            {
                Append(out, "  export lookup  : found at %p\r\n",
                       reinterpret_cast<void*>(getFactory));

                HSTRING name = nullptr;
                if (SUCCEEDED(WindowsCreateString(kClassName, kClassNameLen, &name)))
                {
                    void* factory = nullptr;
                    const HRESULT hrB = getFactory(name, &factory);
                    AppendHresult(out, "  activate", hrB);

                    if (SUCCEEDED(hrB) && factory != nullptr)
                    {
                        Append(out, "  factory        : %p\r\n", factory);
                        reinterpret_cast<IUnknown*>(factory)->Release();
                    }
                    WindowsDeleteString(name);
                }
            }
        }

        if (cookie != nullptr)
        {
            RemoveDllDirectory(cookie);
        }
        Append(out, "\r\n");
    }

    // ------------------------------------------------------------- attempt C
    Append(out, "[C] App Service (com.DolbyLaboratories.DolbyAccess.)\r\n");
    Append(out, "  not attempted  : gated by custom capability\r\n");
    Append(out, "                   DolbyLabs.dolbyDAX3ApiService_rz1tebttyb220\r\n");

    // ------------------------------------------------------------- write out
    const size_t n = out.size();
    const size_t copy = (n < static_cast<size_t>(reportLen) - 1) ? n : static_cast<size_t>(reportLen) - 1;
    memcpy(report, out.c_str(), copy);
    report[copy] = '\0';

    return 0;
}

namespace {

struct LoadVariant
{
    const char* name;
    DWORD flags;
};

constexpr LoadVariant kLoadVariants[] = {
    { "flags=0 (legacy search)", 0 },
    { "SEARCH_DEFAULT_DIRS", LOAD_LIBRARY_SEARCH_DEFAULT_DIRS },
    { "ALTERED_SEARCH_PATH", LOAD_WITH_ALTERED_SEARCH_PATH },
    { "AS_DATAFILE (no execute)", LOAD_LIBRARY_AS_DATAFILE },
};

} // namespace

SS_API ss_result ss_probe_load(const char* dllPath,
                               const char* extraDir,
                               char* report,
                               uint32_t reportLen)
{
    if (report == nullptr || reportLen == 0)
    {
        return -1;
    }

    std::string out;
    out.reserve(4096);

    const std::wstring path = Widen(dllPath);
    if (path.empty())
    {
        Append(out, "no path supplied\r\n");
        const size_t len0 = out.size() < reportLen - 1 ? out.size() : reportLen - 1;
        memcpy(report, out.c_str(), len0);
        report[len0] = '\0';
        return 0;
    }

    Append(out, "dll            : %s\r\n", dllPath);
    Append(out, "exists         : %s\r\n", (GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES) ? "yes" : "no");

    SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS);

    const std::wstring extra = Widen(extraDir);
    DLL_DIRECTORY_COOKIE cookie = nullptr;
    if (!extra.empty())
    {
        cookie = AddDllDirectory(extra.c_str());
        Append(out, "extra dir      : %s (%s)\r\n", extraDir, cookie != nullptr ? "added" : "FAILED");
    }
    Append(out, "\r\n");

    for (const auto& variant : kLoadVariants)
    {
        SetLastError(0);
        HMODULE h = LoadLibraryExW(path.c_str(), nullptr, variant.flags);
        if (h != nullptr)
        {
            Append(out, "  %-26s: OK  %p\r\n", variant.name, h);
            FreeLibrary(h);
        }
        else
        {
            const DWORD err = GetLastError();
            AppendHresult(out, variant.name, HRESULT_FROM_WIN32(err));
        }
    }

    if (cookie != nullptr)
    {
        RemoveDllDirectory(cookie);
    }

    const size_t n = out.size();
    const size_t copy = (n < static_cast<size_t>(reportLen) - 1) ? n : static_cast<size_t>(reportLen) - 1;
    memcpy(report, out.c_str(), copy);
    report[copy] = '\0';

    return 0;
}
