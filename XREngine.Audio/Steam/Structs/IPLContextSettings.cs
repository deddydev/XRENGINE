﻿using System.Runtime.InteropServices;

namespace XREngine.Audio.Steam;

/// <summary>
/// Settings used to create a context object.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct IPLContextSettings
{
    /// <summary>
    /// The API version used by the caller.
    /// Context creation will fail if `phonon.dll` does not implement a compatible version of the API.
    /// Typically, this should be set to `STEAMAUDIO_VERSION`.
    /// </summary>
    public uint version;

    /// <summary>
    /// (Optional) If non-NULL, Steam Audio will call this function to record log messages generated by certain operations.
    /// </summary>
    private IntPtr logCallback;

    /// <summary>
    /// (Optional) If non-NULL, Steam Audio will call this function whenever it needs to allocate memory.
    /// </summary>
    private IntPtr allocateCallback;

    /// <summary>
    /// (Optional) If non-NULL, Steam Audio will call this function whenever it needs to free memory.
    /// </summary>
    private IntPtr freeCallback;

    /// <summary>
    /// The maximum SIMD instruction set level that Steam Audio should use. Steam Audio automatically chooses the best instruction set to use based on the user's CPU, but you can prevent it from using certain newer instruction sets using this parameter. For example, with some workloads, AVX512 instructions consume enough power that the CPU clock speed will be throttled, resulting in lower performance than expected. If you observe this in your application, set this parameter to `IPL_SIMDLEVEL_AVX2` or lower.
    /// </summary>
    public IPLSIMDLevel simdLevel;

    /// <summary>
    /// Additional flags for modifying the behavior of the created context.
    /// </summary>
    public IPLContextFlags flags;

    public IPLContextSettings()
    {
        version = Phonon.STEAMAUDIO_VERSION;
        logCallback = IntPtr.Zero;
        allocateCallback = IntPtr.Zero;
        freeCallback = IntPtr.Zero;
        simdLevel = IPLSIMDLevel.IPL_SIMDLEVEL_AVX512;
        flags = IPLContextFlags.IPL_CONTEXTFLAGS_VALIDATION;
    }

    /// <summary>
    /// (Optional) If non-NULL, Steam Audio will call this function to record log messages generated by certain operations.
    /// </summary>
    public IPLLogFunction? LogCallback
    {
        readonly get => GetLogCallback();
        set => SetLogCallback(value);
    }
    /// <summary>
    /// (Optional) If non-NULL, Steam Audio will call this function whenever it needs to allocate memory.
    /// </summary>
    public IPLAllocateFunction? AllocateCallback
    {
        readonly get => GetAllocateCallback();
        set => SetAllocateCallback(value);
    }
    /// <summary>
    /// (Optional) If non-NULL, Steam Audio will call this function whenever it needs to free memory.
    /// </summary>
    public IPLFreeFunction? FreeCallback
    {
        readonly get => GetFreeCallback();
        set => SetFreeCallback(value);
    }

    private readonly IPLLogFunction? GetLogCallback()
        => logCallback == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<IPLLogFunction>(logCallback);
    private readonly IPLAllocateFunction? GetAllocateCallback()
        => allocateCallback == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<IPLAllocateFunction>(allocateCallback);
    private readonly IPLFreeFunction? GetFreeCallback()
        => freeCallback == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<IPLFreeFunction>(freeCallback);

    private void SetLogCallback(IPLLogFunction? callback)
        => logCallback = callback is null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(callback);
    private void SetAllocateCallback(IPLAllocateFunction? callback)
        => allocateCallback = callback is null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(callback);
    private void SetFreeCallback(IPLFreeFunction? callback)
        => freeCallback = callback is null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(callback);
}