#pragma once

#include <windows.h>

#include <cstdarg>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <string>

namespace ss::diag
{

inline void Append(std::string& out, const char* fmt, ...)
{
    char tmp[4096];
    va_list args;
    va_start(args, fmt);
    vsnprintf_s(tmp, sizeof(tmp), _TRUNCATE, fmt, args);
    va_end(args);
    out += tmp;
}

inline std::wstring Widen(const char* utf8)
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

inline std::string Narrow(const std::wstring& wide)
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

inline const char* HresultName(HRESULT hr)
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
    case 0x80004001UL: return "E_NOTIMPL";
    case 0x80070057UL: return "E_INVALIDARG";
    case 0x80070032UL: return "ERROR_NOT_SUPPORTED";
    case 0x800706BAUL: return "RPC_S_SERVER_UNAVAILABLE";
    case 0x800706EFUL: return "RPC_X_NULL_REF_POINTER";
    case 0x80010106UL: return "RPC_E_CHANGED_MODE";
    case 0x8001010EUL: return "RPC_E_WRONG_THREAD";
    case 0x8000000BUL: return "E_BOUNDS";
    default: return "unknown";
    }
}

inline const char* HresultMeaning(HRESULT hr)
{
    switch (static_cast<unsigned long>(hr))
    {
    case 0x80040154UL: return "class not registered for this process - package boundary";
    case 0x80070005UL: return "blocked by ACL, capability, or code integrity policy";
    case 0x8007007EUL: return "a dependent DLL was not found";
    case 0x80004005UL: return "generic failure - usually the RPC endpoint is not open";
    case 0x80070057UL: return "bad argument - check the HSTRING or array size";
    case 0x80070032UL: return "API present but rejected for this caller";
    case 0x800706BAUL: return "DolbyDAXAPI service not reachable";
    case 0x800706EFUL: return "a required context handle was null - Initialize needs more arguments";
    case 0x8001010EUL: return "called from the wrong apartment - check threading";
    default: return "";
    }
}

inline void AppendHresult(std::string& out, const char* label, HRESULT hr)
{
    Append(out, "%-24s: 0x%08X %s", label, static_cast<unsigned long>(hr), HresultName(hr));
    const char* meaning = HresultMeaning(hr);
    if (meaning[0] != '\0')
    {
        Append(out, "  (%s)", meaning);
    }
    Append(out, "\r\n");
}

inline void CopyOut(const std::string& text, char* dst, uint32_t len)
{
    if (dst == nullptr || len == 0)
    {
        return;
    }

    const size_t copy = (text.size() < static_cast<size_t>(len) - 1)
                            ? text.size()
                            : static_cast<size_t>(len) - 1;

    memcpy(dst, text.c_str(), copy);
    dst[copy] = '\0';
}

} // namespace ss::diag
