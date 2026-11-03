#pragma once

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* 20 bands, matching Dolby's own GEQ layout. */
#define SS_GEQ_BANDS 20

/* 0 == success. Negative == failure. */
typedef int32_t ss_result;

/*
 * SS_BRIDGE_EXPORTS is defined by the CMake target that builds the DLL.
 * Consumers (including C# via DllImport) never include this header.
 */
#if defined(SS_BRIDGE_EXPORTS)
#  define SS_API __declspec(dllexport)
#else
#  define SS_API __declspec(dllimport)
#endif

/*
 * Writes a short build/ABI identification string into buf.
 * Always safe to call; touches nothing outside the DLL.
 */
SS_API ss_result ss_bridge_info(char* buf, uint32_t len);

/*
 * Runs the activation feasibility probe and writes a human-readable
 * multi-line report into buf.
 *
 *   daxClientDll - full path to DAXRPCClient.dll
 *   vclibsDir    - directory containing vccorlib140_app.dll and friends
 *   report       - caller-owned buffer, UTF-8, null terminated
 *   reportLen    - size of report in bytes
 *
 * Returns 0 if the report was written, regardless of whether activation
 * succeeded. The report itself carries the outcome.
 */
SS_API ss_result ss_probe_activation(const char* daxClientDll,
                                     const char* vclibsDir,
                                     char* report,
                                     uint32_t reportLen);

/*
 * Loads dllPath under several loader flag combinations and reports the exact
 * error for each. Used to distinguish an ACL problem from a loader policy that
 * forbids executing code out of WindowsApps.
 *
 *   extraDir - optional directory added to the DLL search path (may be null)
 */
SS_API ss_result ss_probe_load(const char* dllPath,
                               const char* extraDir,
                               char* report,
                               uint32_t reportLen);

/*
 * Opens a DAX session against a directory containing a private copy of
 * DAXRPCClient.dll and the AppContainer CRT DLLs.
 *
 * The copy is required because Windows blocks executing code directly out of
 * C:\Program Files\WindowsApps for unpackaged processes.
 */
SS_API ss_result ss_open(const char* cacheDir, char* report, uint32_t reportLen);

/* Closes the RPC endpoint and releases everything. Safe to call twice. */
SS_API ss_result ss_close(void);

/* Writes a full state dump (profile, EQ, endpoints) into buf. */
SS_API ss_result ss_dump_state(char* report, uint32_t reportLen);

SS_API ss_result ss_get_active_profile(int32_t* value);
SS_API ss_result ss_set_active_profile(int32_t value);

/* Returns 0 on success and fills up to capacity values; count goes in written. */
SS_API ss_result ss_get_geq(int32_t* values, uint32_t capacity, uint32_t* written);
SS_API ss_result ss_set_geq(const int32_t* values, uint32_t count);

/*
 * Per-sub-profile feature switches of the active profile. 1 == on, 0 == off.
 * Like the EQ, these live on the Personalize sub-profile; reading them from
 * another profile reports that profile's own values.
 */
SS_API ss_result ss_get_features(int32_t* volumeLeveler, int32_t* surroundVirtualizer);
SS_API ss_result ss_set_volume_leveler(int32_t enabled);
SS_API ss_result ss_set_surround_virtualizer(int32_t enabled);

/* One-shot read of the scalar state. Any out pointer may be null. */
SS_API ss_result ss_get_state(int32_t* profile,
                              int32_t* subProfile,
                              int32_t* dolbyEnabled,
                              int32_t* ieq,
                              int32_t* geqMaximumRange);

/*
 * What this particular DAX build exposes. Any out pointer may be null.
 * bandCount is the real GEQ width, profileCount the number of device modes.
 * Dolby does not document either, so callers must not assume 20/six.
 */
SS_API ss_result ss_get_capabilities(int32_t* bandCount,
                                     int32_t* geqMaximumRange,
                                     int32_t* profileCount);

/* Writes the display name of device mode `index` into buf (UTF-8). */
SS_API ss_result ss_get_profile_name(int32_t index, char* buf, uint32_t len);

#ifdef __cplusplus
}
#endif
