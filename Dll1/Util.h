#pragma once

extern "C"
{
    __declspec(dllexport) bool CreateCaptureItemForWindow(HWND hwnd, void** result);
}