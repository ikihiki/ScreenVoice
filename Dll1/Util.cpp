#include "pch.h"
#include <winrt/Windows.Graphics.Capture.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.capture.h>
#include "Util.h"


    bool CreateCaptureItemForWindow(HWND hwnd, void **result)
    {
        auto interop_factory = winrt::get_activation_factory<winrt::Windows::Graphics::Capture::GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
        auto reult = interop_factory->CreateForWindow(hwnd, winrt::guid_of<ABI::Windows::Graphics::Capture::IGraphicsCaptureItem>(), result);
        return SUCCEEDED(reult);
    }

    //bool CreateCaptureItemForMonitor(HMONITOR hmon)
    //{
    //    auto interop_factory = winrt::get_activation_factory<winrt::Windows::Graphics::Capture::GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
    //    winrt::Windows::Graphics::Capture::GraphicsCaptureItem item = { nullptr };
    //    winrt::check_hresult(interop_factory->CreateForMonitor(hmon, winrt::guid_of<ABI::Windows::Graphics::Capture::IGraphicsCaptureItem>(), winrt::put_abi(item)));
    //    return item;
    //}
