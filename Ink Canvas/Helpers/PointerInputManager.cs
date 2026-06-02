using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Ink_Canvas;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// WM_POINTER 输入管理器（被 WPF 拦截时自动降级）。
    /// 
    /// 1) 优先通过 HwndSource.AddHook 只读监听 WM_POINTER（WPF 处理后）。
    /// 2) 若收不到 WM_POINTER 消息（WPF 内部拦截），则触控靠 WPF 原生处理。
    /// 3) 热插拔后通过 Rehook() 重新订阅。
    /// 
    /// Gesture/Shape 等高级功能在收不到 WM_POINTER 时由 WPF 自身处理。
    /// </summary>
    public class PointerInputManager : IDisposable
    {
        private const int WM_POINTERUPDATE = 0x0245;
        private const int WM_POINTERDOWN = 0x0246;
        private const int WM_POINTERUP = 0x0247;

        private readonly Window _window;
        private readonly InkCanvas _inkCanvas;
        private HwndSource _source;
        private bool _enabled = false;
        private int _msgCount = 0;
        private DateTime _lastMsgTime = DateTime.MinValue;

        public PointerInputManager(Window window, InkCanvas inkCanvas) { _window = window; _inkCanvas = inkCanvas; }

        public bool Enable()
        {
            if (_enabled) return true;
            try
            {
                var hwnd = new WindowInteropHelper(_window).Handle;
                _source = HwndSource.FromHwnd(hwnd);
                if (_source == null) return false;
                _source.AddHook(WndProc);
                _enabled = true;
                return true;
            }
            catch { return false; }
        }

        public void Disable() { if (_enabled && _source != null) _source.RemoveHook(WndProc); _enabled = false; }

        public void Rehook()
        {
            if (!_enabled) return;
            try
            {
                if (_source != null) _source.RemoveHook(WndProc);
                _source = HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);
                if (_source != null) _source.AddHook(WndProc);
            }
            catch { }
        }

        public void Dispose() => Disable();

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_POINTERDOWN || msg == WM_POINTERUPDATE || msg == WM_POINTERUP)
            {
                _msgCount++;
                // 只记录消息计数证明通道活着，不做任何触控处理
                // 触控靠 WPF 原生处理（因为 WPF 内部已消费 WM_POINTER）
            }
            return IntPtr.Zero;
        }
    }
}
