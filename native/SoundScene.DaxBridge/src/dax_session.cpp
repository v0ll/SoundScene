#include "soundscene_dax.h"

#include "dax_interface.h"
#include "diag.h"

#include <objbase.h>
#include <cwchar>
#include <vector>

using namespace ss::diag;
using ss::dax::boolean_t;
using ss::dax::IActivationFactory;
using ss::dax::IDolbyDAX;

namespace
{

struct Session
{
    HMODULE module = nullptr;
    IActivationFactory* factory = nullptr;
    IDolbyDAX* dax = nullptr;
    bool initialized = false;
    bool rpcOpen = false;
};

Session g_session;

constexpr const wchar_t* kClassName = L"DAX.CDolbyDAX";
constexpr UINT32 kClassNameLen = 13;

class HString
{
public:
    explicit HString(const wchar_t* text)
    {
        if (text != nullptr)
        {
            WindowsCreateString(text, static_cast<UINT32>(wcslen(text)), &value_);
        }
    }

    ~HString()
    {
        if (value_ != nullptr)
        {
            WindowsDeleteString(value_);
        }
    }

    HString(const HString&) = delete;
    HString& operator=(const HString&) = delete;

    HSTRING get() const { return value_; }

private:
    HSTRING value_ = nullptr;
};

std::string TakeHString(HSTRING h)
{
    if (h == nullptr)
    {
        return "<null>";
    }

    UINT32 length = 0;
    const wchar_t* buffer = WindowsGetStringRawBuffer(h, &length);
    const std::string result = Narrow(std::wstring(buffer, length));
    WindowsDeleteString(h);
    return result;
}

std::wstring CurrentUserName()
{
    wchar_t buffer[256] = {};
    DWORD size = static_cast<DWORD>(sizeof(buffer) / sizeof(buffer[0]));
    if (GetUserNameW(buffer, &size) == 0)
    {
        return L"";
    }
    return buffer;
}

} // namespace

SS_API ss_result ss_open(const char* cacheDir, char* report, uint32_t reportLen)
{
    std::string out;

    if (g_session.dax != nullptr)
    {
        Append(out, "already open\r\n");
        CopyOut(out, report, reportLen);
        return 0;
    }

    const std::wstring dir = Widen(cacheDir);
    if (dir.empty())
    {
        Append(out, "ss_open: no cache directory supplied\r\n");
        CopyOut(out, report, reportLen);
        return -1;
    }

    const std::wstring dllPath = dir + L"\\DAXRPCClient.dll";
    Append(out, "cache dir      : %s\r\n", cacheDir);
    Append(out, "client dll     : %s\r\n", Narrow(dllPath).c_str());

    if (GetFileAttributesW(dllPath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        Append(out, "client dll     : MISSING\r\n");
        CopyOut(out, report, reportLen);
        return -2;
    }

    SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS);
    AddDllDirectory(dir.c_str());

    g_session.module = LoadLibraryExW(dllPath.c_str(), nullptr,
                                      LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS);
    if (g_session.module == nullptr)
    {
        AppendHresult(out, "LoadLibraryExW", HRESULT_FROM_WIN32(GetLastError()));
        CopyOut(out, report, reportLen);
        return -3;
    }
    Append(out, "LoadLibraryExW : ok %p\r\n", g_session.module);

    using DllGetActivationFactoryFn = HRESULT(WINAPI*)(HSTRING, void**);
    auto getFactory = reinterpret_cast<DllGetActivationFactoryFn>(
        GetProcAddress(g_session.module, "DllGetActivationFactory"));
    if (getFactory == nullptr)
    {
        Append(out, "DllGetActivationFactory : not found\r\n");
        CopyOut(out, report, reportLen);
        return -4;
    }

    HString className(kClassName);
    void* factory = nullptr;
    HRESULT hr = getFactory(className.get(), &factory);
    AppendHresult(out, "DllGetActivationFactory", hr);
    if (FAILED(hr) || factory == nullptr)
    {
        CopyOut(out, report, reportLen);
        return -5;
    }

    g_session.factory = static_cast<IActivationFactory*>(factory);

    IInspectable* instance = nullptr;
    hr = g_session.factory->ActivateInstance(&instance);
    AppendHresult(out, "ActivateInstance", hr);
    if (FAILED(hr) || instance == nullptr)
    {
        CopyOut(out, report, reportLen);
        return -6;
    }

    IDolbyDAX* dax = nullptr;
    hr = instance->QueryInterface(ss::dax::IID_IDolbyDAX, reinterpret_cast<void**>(&dax));
    instance->Release();
    AppendHresult(out, "QueryInterface", hr);
    if (FAILED(hr) || dax == nullptr)
    {
        CopyOut(out, report, reportLen);
        return -7;
    }

    g_session.dax = dax;

    INT32 v1 = 0, v2 = 0, v3 = 0, v4 = 0;
    hr = dax->GetVersionNumber(&v1, &v2, &v3, &v4);
    if (SUCCEEDED(hr))
    {
        Append(out, "version        : %d.%d.%d.%d\r\n", v1, v2, v3, v4);
    }
    else
    {
        AppendHresult(out, "GetVersionNumber", hr);
    }

    const std::wstring userName = CurrentUserName();
    Append(out, "user name      : %s\r\n", Narrow(userName).c_str());

    HString user(userName.c_str());
    boolean_t initialized = 0;
    hr = dax->Initialize(user.get(), &initialized);
    AppendHresult(out, "Initialize", hr);
    Append(out, "  initialized  : %s\r\n", initialized ? "true" : "false");
    g_session.initialized = (SUCCEEDED(hr) && initialized != 0);

    INT64 endpoint = 0;
    hr = dax->OpenRPCEndpoint(&endpoint);
    AppendHresult(out, "OpenRPCEndpoint", hr);
    if (SUCCEEDED(hr))
    {
        Append(out, "  endpoint     : 0x%llX\r\n", static_cast<unsigned long long>(endpoint));
        g_session.rpcOpen = true;
    }

    Append(out, "\r\nopen result    : %s\r\n",
           (g_session.initialized && g_session.rpcOpen) ? "READY" : "PARTIAL");

    CopyOut(out, report, reportLen);
    return 0;
}

SS_API ss_result ss_close(void)
{
    if (g_session.dax != nullptr)
    {
        if (g_session.rpcOpen)
        {
            g_session.dax->CloseRPCEndpoint();
        }
        g_session.dax->Release();
        g_session.dax = nullptr;
    }

    if (g_session.factory != nullptr)
    {
        g_session.factory->Release();
        g_session.factory = nullptr;
    }

    if (g_session.module != nullptr)
    {
        FreeLibrary(g_session.module);
        g_session.module = nullptr;
    }

    g_session.initialized = false;
    g_session.rpcOpen = false;
    return 0;
}

SS_API ss_result ss_dump_state(char* report, uint32_t reportLen)
{
    std::string out;

    if (g_session.dax == nullptr)
    {
        Append(out, "not open - call ss_open first\r\n");
        CopyOut(out, report, reportLen);
        return -1;
    }

    IDolbyDAX* dax = g_session.dax;

    boolean_t enabled = 0;
    HRESULT hr = dax->get_DolbyEnable(&enabled);
    Append(out, "DolbyEnable         : %s (hr=0x%08X)\r\n", enabled ? "true" : "false", static_cast<unsigned long>(hr));

    INT32 profile = -1;
    hr = dax->get_ActiveProfile(&profile);
    Append(out, "ActiveProfile       : %d (hr=0x%08X)\r\n", profile, static_cast<unsigned long>(hr));

    INT32 subProfile = -1;
    hr = dax->get_ActiveSubProfile(&subProfile);
    Append(out, "ActiveSubProfile    : %d (hr=0x%08X)\r\n", subProfile, static_cast<unsigned long>(hr));

    INT32 ieq = -1;
    hr = dax->get_IEQ(&ieq);
    Append(out, "IEQ                 : %d (hr=0x%08X)\r\n", ieq, static_cast<unsigned long>(hr));

    INT32 apiStatus = -1;
    hr = dax->GetAPIStatus(&apiStatus);
    Append(out, "GetAPIStatus        : %d (hr=0x%08X)\r\n", apiStatus, static_cast<unsigned long>(hr));

    INT32 maxRange = -1;
    hr = dax->GetGEQMaximumRange(&maxRange);
    Append(out, "GetGEQMaximumRange  : %d (hr=0x%08X)\r\n", maxRange, static_cast<unsigned long>(hr));

    UINT32 geqSize = 0;
    INT32* geqValues = nullptr;
    hr = dax->get_GEQLevels(&geqSize, &geqValues);
    if (SUCCEEDED(hr) && geqValues != nullptr)
    {
        Append(out, "GEQLevels           : %u bands\r\n  [", geqSize);
        for (UINT32 i = 0; i < geqSize; ++i)
        {
            Append(out, "%s%d", (i == 0) ? "" : ", ", geqValues[i]);
        }
        Append(out, "]\r\n");
        CoTaskMemFree(geqValues);
    }
    else
    {
        AppendHresult(out, "get_GEQLevels", hr);
    }

    INT32 endpointCount = 0;
    hr = dax->get_EndpointCount(&endpointCount);
    Append(out, "EndpointCount       : %d (hr=0x%08X)\r\n", endpointCount, static_cast<unsigned long>(hr));

    for (INT32 i = 0; i < endpointCount; ++i)
    {
        HSTRING name = nullptr;
        UINT32 formFactor = 0;
        boolean_t offloaded = 0;
        boolean_t isDefault = 0;
        hr = dax->GetEndpointData(i, &name, &formFactor, &offloaded, &isDefault);
        if (SUCCEEDED(hr))
        {
            Append(out, "  [%d] %-40s formFactor=%u offloaded=%s default=%s\r\n",
                   i, TakeHString(name).c_str(), formFactor,
                   offloaded ? "true" : "false", isDefault ? "true" : "false");
        }
        else
        {
            Append(out, "  [%d] <GetEndpointData failed 0x%08X>\r\n", i, static_cast<unsigned long>(hr));
        }
    }

    HSTRING daxEndpoints = nullptr;
    hr = dax->get_DaxEndpoints(&daxEndpoints);
    if (SUCCEEDED(hr))
    {
        Append(out, "DaxEndpoints        : %s\r\n", TakeHString(daxEndpoints).c_str());
    }
    else
    {
        AppendHresult(out, "get_DaxEndpoints", hr);
    }

    CopyOut(out, report, reportLen);
    return 0;
}

SS_API ss_result ss_get_active_profile(int32_t* value)
{
    if (g_session.dax == nullptr || value == nullptr)
    {
        return -1;
    }

    INT32 profile = 0;
    const HRESULT hr = g_session.dax->get_ActiveProfile(&profile);
    if (FAILED(hr))
    {
        return static_cast<ss_result>(hr);
    }

    *value = profile;
    return 0;
}

SS_API ss_result ss_set_active_profile(int32_t value)
{
    if (g_session.dax == nullptr)
    {
        return -1;
    }

    return static_cast<ss_result>(g_session.dax->put_ActiveProfile(value));
}

SS_API ss_result ss_get_geq(int32_t* values, uint32_t capacity, uint32_t* written)
{
    if (g_session.dax == nullptr || values == nullptr || written == nullptr)
    {
        return -1;
    }

    UINT32 size = 0;
    INT32* raw = nullptr;
    const HRESULT hr = g_session.dax->get_GEQLevels(&size, &raw);
    if (FAILED(hr) || raw == nullptr)
    {
        return static_cast<ss_result>(hr);
    }

    const UINT32 count = (size < capacity) ? size : capacity;
    for (UINT32 i = 0; i < count; ++i)
    {
        values[i] = raw[i];
    }

    CoTaskMemFree(raw);
    *written = count;
    return 0;
}

SS_API ss_result ss_set_geq(const int32_t* values, uint32_t count)
{
    if (g_session.dax == nullptr || values == nullptr || count == 0)
    {
        return -1;
    }

    return static_cast<ss_result>(
        g_session.dax->put_GEQLevels(count, const_cast<INT32*>(values)));
}
