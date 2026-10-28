#pragma once

/*
 * Minimal hand-written projection of DAX.IDolbyDAX.
 *
 * Source of truth: native/idl/DAX.idl, generated from DAX.winmd by winmdidl.
 * WinRT interfaces are COM-compatible, so a vtable with the exact method order
 * from the IDL is enough to call them - no C++/WinRT, no NuGet, no projection
 * assembly.
 *
 * Method order must match the IDL exactly. Do not reorder, do not insert.
 */

#include <windows.h>
#include <inspectable.h>
#include <winstring.h>

namespace ss::dax
{

using boolean_t = unsigned char;

/* DAX.IDolbyDAX */
inline constexpr GUID IID_IDolbyDAX =
    { 0x4378E64E, 0x3782, 0x3C8C, { 0xA0, 0x61, 0x84, 0xD4, 0x3C, 0xB1, 0x13, 0x32 } };

/* DAX.IDAXEvent - change notifications */
inline constexpr GUID IID_IDAXEvent =
    { 0xFBB34B5B, 0xB3CC, 0x3A02, { 0x88, 0x6E, 0x7C, 0xF8, 0x76, 0x4F, 0x36, 0x05 } };

struct IDolbyDAX : IInspectable
{
    virtual HRESULT STDMETHODCALLTYPE OpenRPCEndpoint(INT64* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE CloseRPCEndpoint() = 0;
    virtual HRESULT STDMETHODCALLTYPE GetVersionNumber(INT32* v1, INT32* v2, INT32* v3, INT32* v4) = 0;
    virtual HRESULT STDMETHODCALLTYPE IsDaxEndpoint(HSTRING deviceId, boolean_t* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE Initialize(HSTRING userName, boolean_t* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE InitializeEx(HSTRING userName, HSTRING deviceId, boolean_t* returnValue) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_DolbyEnable(boolean_t* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_DolbyEnable(boolean_t value) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_ActiveProfile(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_ActiveProfile(INT32 value) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_SurroundVirtualizerEnable(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_SurroundVirtualizerEnable(INT32 value) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_DialogEnhancerEnable(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_DialogEnhancerEnable(INT32 value) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_VolumeLevelerEnable(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_VolumeLevelerEnable(INT32 value) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_BassEnhancerEnable(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_BassEnhancerEnable(INT32 value) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_GEQLevels(UINT32* returnValueSize, INT32** returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_GEQLevels(UINT32 valueSize, INT32* value) = 0;

    virtual HRESULT STDMETHODCALLTYPE ResetGEQ() = 0;

    virtual HRESULT STDMETHODCALLTYPE get_DeviceMode(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_DeviceModeCount(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetDeviceModeName(INT32 index, HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetDeviceModeIcon(INT32 index, HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_OperatorInfo(HSTRING* returnValue) = 0;

    virtual HRESULT STDMETHODCALLTYPE GetSystemCapabilities(
        INT16* mediaIntelligenceEnabled,
        INT16* volumeLevelerEnabled,
        INT16* dialogEnchancerEnabled,
        INT16* virtualBassEnabled,
        INT16* bassEnchancerEnabled,
        INT16* speakerVirtualizerEnabled,
        INT16* headphoneVirtualizerEnabled,
        INT16* geqEnabled,
        INT16* ieqEnabled,
        INT16* audioOptimizerEnabled,
        INT16* audioRegulatorEnabled,
        INT16* surroundDecoderEnabled,
        INT16* volumeMaximizerEnabled) = 0;

    virtual HRESULT STDMETHODCALLTYPE GetGEQMaximumRange(INT32* returnValue) = 0;

    virtual HRESULT STDMETHODCALLTYPE GetAPIStatus(INT32* returnValue) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_EndpointCount(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetEndpointData(
        INT32 nIndex, HSTRING* friendlyName, UINT32* formFactor,
        boolean_t* offloaded, boolean_t* defaultDevice) = 0;

    /* ---- everything below is declared for layout completeness only ---- */

    virtual HRESULT STDMETHODCALLTYPE get_PeripheralCount(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetPeripheralData(
        INT32 index, HSTRING* brand, HSTRING* model,
        UINT32* deviceBitmapSize, BYTE** deviceBitmap) = 0;
    virtual HRESULT STDMETHODCALLTYPE SelectPeripheral(INT32 endpointIndex, INT32 peripheralIndex) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetSelectedPeripheral(INT32 endpointIndex, INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE IsSpatialAudioSupported(boolean_t* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetSpatialAudioStatus(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_DefaultUITheme(HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_SKU(HSTRING* returnValue) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_ActiveSubProfile(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_ActiveSubProfile(INT32 value) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_SubProfileDirty(boolean_t* returnValue) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_IEQ(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_IEQ(INT32 value) = 0;

    virtual HRESULT STDMETHODCALLTYPE ResetSubProfile() = 0;
    virtual HRESULT STDMETHODCALLTYPE ResetIEQ() = 0;

    virtual HRESULT STDMETHODCALLTYPE get_SurroundVirtualizerValue(FLOAT* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_SurroundVirtualizerValue(FLOAT value) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_DialogEnhancerValue(FLOAT* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_DialogEnhancerValue(FLOAT value) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_VolumeLevelerValue(FLOAT* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_VolumeLevelerValue(FLOAT value) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_IsShowAutoSwitch(boolean_t* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_AutoSwitchEnable(boolean_t* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_AutoSwitchEnable(boolean_t value) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_UnlockKey(HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_TuningVersion(HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_TuningDate(HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_OperatorName(HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_ContentProcessingParams(HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_DeviceProcessingParams(HSTRING* returnValue) = 0;

    virtual HRESULT STDMETHODCALLTYPE GetHotkey(INT32 index, HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetHotkey(INT32 index, HSTRING hotkey, INT32* returnValue) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_RadarEnable(boolean_t* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_RadarEnable(boolean_t value) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_RadarSize(FLOAT* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_RadarSize(FLOAT value) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_RadarTransparency(FLOAT* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_RadarTransparency(FLOAT value) = 0;
    virtual HRESULT STDMETHODCALLTYPE get_RadarSkin(INT32* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE put_RadarSkin(INT32 value) = 0;

    virtual HRESULT STDMETHODCALLTYPE get_DaxEndpoints(HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetRadarPosition(FLOAT* x, FLOAT* y) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetRadarPosition(FLOAT x, FLOAT y) = 0;

    virtual HRESULT STDMETHODCALLTYPE GetProperty(HSTRING name, HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetProperty(HSTRING name, HSTRING value) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetPropertyEx(HSTRING name, HSTRING parameter, HSTRING* returnValue) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetPropertyEx(HSTRING name, HSTRING parameter, HSTRING value) = 0;

    virtual HRESULT STDMETHODCALLTYPE RegisterCallback(IUnknown* callback) = 0;
    virtual HRESULT STDMETHODCALLTYPE UnregisterCallback() = 0;
    virtual HRESULT STDMETHODCALLTYPE RegisterCallback_Spatial(IUnknown* callback) = 0;
    virtual HRESULT STDMETHODCALLTYPE UnregisterCallback_Spatial() = 0;
    virtual HRESULT STDMETHODCALLTYPE RegisterCallback_Manager2(IUnknown* callback) = 0;
    virtual HRESULT STDMETHODCALLTYPE UnregisterCallback_Manager2() = 0;
    virtual HRESULT STDMETHODCALLTYPE RegisterCallback_Game(IUnknown* callback) = 0;
    virtual HRESULT STDMETHODCALLTYPE UnregisterCallback_Game() = 0;
};

/* Windows.Foundation.IActivationFactory */
struct IActivationFactory : IInspectable
{
    virtual HRESULT STDMETHODCALLTYPE ActivateInstance(IInspectable** instance) = 0;
};

} // namespace ss::dax
