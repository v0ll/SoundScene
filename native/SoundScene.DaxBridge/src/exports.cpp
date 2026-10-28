#include "soundscene_dax.h"

#include <cstdio>
#include <cstring>

namespace {

void WriteUtf8(char* dst, uint32_t len, const char* src) noexcept
{
    if (dst == nullptr || len == 0)
    {
        return;
    }

    if (src == nullptr)
    {
        dst[0] = '\0';
        return;
    }

    const size_t n = std::strlen(src);
    const size_t copy = (n < static_cast<size_t>(len) - 1) ? n : static_cast<size_t>(len) - 1;
    std::memcpy(dst, src, copy);
    dst[copy] = '\0';
}

} // namespace

SS_API ss_result ss_bridge_info(char* buf, uint32_t len)
{
    char text[256];
    std::snprintf(text, sizeof(text),
                  "SoundScene.DaxBridge 0.1.0 | %s | MSVC %d | C ABI v1",
                  (sizeof(void*) == 8) ? "x64" : "x86",
                  _MSC_VER);

    WriteUtf8(buf, len, text);
    return 0;
}
