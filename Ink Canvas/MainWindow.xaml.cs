using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        #region Window Initialization

        public MainWindow()
        {
            /*
                处于画板模式内：Topmost == false / currentMode != 0
                处于 PPT 放映内：BtnPPTSlideShowEnd.Visibility
            */
            InitializeComponent();

            BlackboardLeftSide.Visibility = Visibility.Collapsed;
            BlackboardCenterSide.Visibility = Visibility.Collapsed;
            BlackboardRightSide.Visibility = Visibility.Collapsed;

            BorderTools.Visibility = Visibility.Collapsed;
            BorderSettings.Visibility = Visibility.Collapsed;

            BtnPPTSlideShowEnd.Visibility = Visibility.Collapsed;
            PPTNavigationBottomLeft.Visibility = Visibility.Collapsed;
            PPTNavigationBottomRight.Visibility = Visibility.Collapsed;
            PPTNavigationSidesLeft.Visibility = Visibility.Collapsed;
            PPTNavigationSidesRight.Visibility = Visibility.Collapsed;

            BorderSettings.Margin = new Thickness(0, 150, 0, 150);

            TwoFingerGestureBorder.Visibility = Visibility.Collapsed;
            BoardTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
            BorderDrawShape.Visibility = Visibility.Collapsed;
            BoardBorderDrawShape.Visibility = Visibility.Collapsed;

            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

            ViewboxFloatingBar.Margin = new Thickness((SystemParameters.WorkArea.Width - 284) / 2, SystemParameters.WorkArea.Height - 60, -2000, -200);
            ViewboxFloatingBarMarginAnimation();

            try
            {
                if (File.Exists("Log.txt"))
                {
                    FileInfo fileInfo = new FileInfo("Log.txt");
                    long fileSizeInKB = fileInfo.Length / 1024;
                    if (fileSizeInKB > 512)
                    {
                        try
                        {
                            File.Delete("Log.txt");
                            LogHelper.WriteLogToFile("The Log.txt file has been successfully deleted. Original file size: " + fileSizeInKB + " KB", LogHelper.LogType.Info);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile(ex + " | Can not delete the Log.txt file. File size: " + fileSizeInKB + " KB", LogHelper.LogType.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }

            InitTimers();
            timeMachine.OnRedoStateChanged += TimeMachine_OnRedoStateChanged;
            timeMachine.OnUndoStateChanged += TimeMachine_OnUndoStateChanged;
            inkCanvas.Strokes.StrokesChanged += StrokesOnStrokesChanged;

            Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            try
            {
                if (File.Exists("SpecialVersion.ini")) SpecialVersionResetToSuggestion_Click();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }

            CheckColorTheme(true);
        }

        #endregion

        #region Ink Canvas Functions

        DrawingAttributes drawingAttributes;
        private void loadPenCanvas()
        {
            try
            {
                drawingAttributes = inkCanvas.DefaultDrawingAttributes;
                drawingAttributes.Color = Colors.Red;

                drawingAttributes.Height = 2.5;
                drawingAttributes.Width = 2.5;

                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                inkCanvas.Gesture += InkCanvas_Gesture;
            }
            catch { }
        }

        private void InkCanvas_Gesture(object sender, InkCanvasGestureEventArgs e)
        {
            ReadOnlyCollection<GestureRecognitionResult> gestures = e.GetGestureRecognitionResults();
            try
            {
                foreach (GestureRecognitionResult gest in gestures)
                {
                    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                    {
                        if (gest.ApplicationGesture == ApplicationGesture.Left)
                        {
                            BtnPPTSlidesDown_Click(null, null);
                        }
                        if (gest.ApplicationGesture == ApplicationGesture.Right)
                        {
                            BtnPPTSlidesUp_Click(null, null);
                        }
                    }
                }
            }
            catch { }
        }

        private void inkCanvas_EditingModeChanged(object sender, RoutedEventArgs e)
        {
            var inkCanvas1 = sender as InkCanvas;
            if (inkCanvas1 == null) return;
            if (Settings.Canvas.IsShowCursor)
            {
                if (inkCanvas1.EditingMode == InkCanvasEditingMode.Ink || drawingShapeMode != 0)
                {
                    inkCanvas1.ForceCursor = true;
                }
                else
                {
                    inkCanvas1.ForceCursor = false;
                }
            }
            else
            {
                inkCanvas1.ForceCursor = false;
            }
            if (inkCanvas1.EditingMode == InkCanvasEditingMode.Ink) forcePointEraser = !forcePointEraser;
        }

        #endregion Ink Canvas Functions

        #region Definations and Loading

        public static Settings Settings = new Settings();
        public static string settingsFileName = "Settings.json";
        bool isLoaded = false;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            loadPenCanvas();
            //加载设置
            LoadSettings(true);
            if (Environment.Is64BitProcess)
            {
                GroupBoxInkRecognition.Visibility = Visibility.Collapsed;
            }

            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            SystemEvents_UserPreferenceChanged(null, null);

            AppVersionTextBlock.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            LogHelper.WriteLogToFile("Ink Canvas Loaded", LogHelper.LogType.Event);
            isLoaded = true;
            RegisterGlobalHotkeys();
            SetupTouchDeviceWatcher();
            ApplySuperTopmostOnStartup();
            InitPointerInput();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            LogHelper.WriteLogToFile("Ink Canvas closing", LogHelper.LogType.Event);
            if (!CloseIsFromButton && Settings.Advanced.IsSecondConfimeWhenShutdownApp)
            {
                e.Cancel = true;
                if (MessageBox.Show("是否继续关闭 Ink Canvas 画板，这将丢失当前未保存的工作。", "Ink Canvas 画板", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    if (MessageBox.Show("真的狠心关闭 Ink Canvas 画板吗？", "Ink Canvas 画板", MessageBoxButton.OKCancel, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        if (MessageBox.Show("是否取消关闭 Ink Canvas 画板？", "Ink Canvas 画板", MessageBoxButton.OKCancel, MessageBoxImage.Error) != MessageBoxResult.OK)
                        {
                            e.Cancel = false;
                        }
                    }
                }
            }
            if (e.Cancel)
            {
                LogHelper.WriteLogToFile("Ink Canvas closing cancelled", LogHelper.LogType.Event);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            StopSuperTopmostLoop();
            UnregisterHidDeviceNotification();
            LogHelper.WriteLogToFile("Ink Canvas closed", LogHelper.LogType.Event);
        }

        #region Touch device hot-plug fix

        // ==================== 检测机制 ====================
        //
        // 根因：WPF 的 StylusPlugIn 链在触摸屏热插拔后仍引用旧的 StylusDevice 对象，
        // 导致整个窗口所有元素触控失效。
        //
        // 四重检测 + 防抖，覆盖以下场景：
        //   ① WM_DEVICECHANGE（Windows 实时广播任何硬件设备变动，包括 USB HID 触摸屏）
        //   ② WM_TABLET_ADDED（Tablet PC 专用消息）
        //   ③ SystemEvents.DisplaySettingsChanged（显示配置变更）
        //   ④ 定时器轮询 Tablet.TabletDevices（兜底）
        //
        // 修复：反射调用 WPF 内部 StylusLogic.HandleTabletAdded()，
        //       强制重新枚举设备 + 重建全部 StylusPlugIn 链。

        private DateTime lastTouchResetTime = DateTime.MinValue;
        private const int TOUCH_RESET_DEBOUNCE_MS = 2000;

        /// <summary>
        /// 检测到触摸设备变更的入口（带防抖）。
        /// 防抖原因：WM_DEVICECHANGE 在每次硬件变动（插拔鼠标/键盘/U盘等）都会触发，
        /// 但只有真正的触摸屏变更才需要执行重置。
        /// </summary>
        private void OnTouchDeviceChangeDetected()
        {
            var now = DateTime.Now;
            if ((now - lastTouchResetTime).TotalMilliseconds < TOUCH_RESET_DEBOUNCE_MS)
                return;
            lastTouchResetTime = now;

            // 先快速验证——检查 Tablet 设备数量或名称是否有实际变化
            if (!HasTabletDevicesActuallyChanged())
                return;

            HandleTouchDeviceReconnection();
        }

        /// <summary>
        /// 验证 Tablet 设备是否真的发生了变化。
        /// 避免对非触摸屏硬件变动（鼠标、键盘等）做无效重置。
        /// </summary>
        private bool HasTabletDevicesActuallyChanged()
        {
            try
            {
                var devices = System.Windows.Input.Tablet.TabletDevices;
                int currentCount = devices.Count;

                // 如果数量变了，肯定是真变化
                if (_lastTabletDeviceSnapshot != null && _lastTabletDeviceSnapshot.Count != currentCount)
                    return true;

                // 数量没变但设备有了新名字 → 设备被替换/重新枚举了
                if (_lastTabletDeviceSnapshot != null)
                {
                    var currentNames = new System.Collections.Generic.HashSet<string>();
                    for (int i = 0; i < currentCount; i++)
                    {
                        string name = devices[i]?.Name ?? "";
                        currentNames.Add(name);
                    }
                    if (!currentNames.SetEquals(_lastTabletDeviceSnapshot))
                        return true;
                }

                // 更新快照
                _lastTabletDeviceSnapshot = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < currentCount; i++)
                {
                    _lastTabletDeviceSnapshot.Add(devices[i]?.Name ?? "");
                }

                return false;
            }
            catch
            {
                // 如果读取失败（设备断开中），返回 true 以触发重置
                return true;
            }
        }

        private System.Collections.Generic.HashSet<string> _lastTabletDeviceSnapshot = null;

        // ==================== 修复执行 ====================

        private void HandleTouchDeviceReconnection()
        {
            LogHelper.WriteLogToFile("Touch device change detected — running HandleTabletAdded...", LogHelper.LogType.Event);
            // 反射调用 HandleTabletAdded 修复 WPF 触控管道
            ForceStylusSystemReset();
            // 重新挂载 WM_POINTER 诊断监听
            _pointerInput?.Rehook();
        }

        private void ForceStylusSystemReset()
        {
            try
            {
                var stylusLogicType = typeof(Stylus).Assembly
                    .GetType("System.Windows.Input.StylusLogic");
                if (stylusLogicType == null) return;

                var currentStylusLogic = stylusLogicType.GetField("_currentStylusLogic",
                    BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
                if (currentStylusLogic == null) return;

                // HandleTabletAdded 是 WPF 内部用于响应 WM_TABLET_ADDED 的方法，
                // 它会重新枚举所有 Tablet 设备，并为每个 HwndSource 重建
                // StylusPlugIn 链，从而修复触控失效。
                var handleTabletAdded = stylusLogicType.GetMethod("HandleTabletAdded",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                handleTabletAdded?.Invoke(currentStylusLogic, new object[] { null });

                // 更新设备快照
                UpdateTabletDeviceSnapshot();

                LogHelper.WriteLogToFile("Stylus system reinitialized via StylusLogic.HandleTabletAdded().", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ForceStylusSystemReset via reflection failed: {ex}", LogHelper.LogType.Error);
                FallbackTouchReset();
            }
        }

        private void UpdateTabletDeviceSnapshot()
        {
            try
            {
                var devices = System.Windows.Input.Tablet.TabletDevices;
                _lastTabletDeviceSnapshot = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < devices.Count; i++)
                {
                    _lastTabletDeviceSnapshot.Add(devices[i]?.Name ?? "");
                }
            }
            catch { }
        }

        private void FallbackTouchReset()
        {
            try
            {
                if (Main_Grid == null) return;
                var editingMode = inkCanvas.EditingMode;
                this.Content = null;
                this.Content = Main_Grid;
                inkCanvas.EditingMode = editingMode;
                LogHelper.WriteLogToFile("Touch reset fallback: Main_Grid reparented.", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"FallbackTouchReset failed: {ex}", LogHelper.LogType.Error);
            }
        }

        // ==================== 消息钩子 + RegisterDeviceNotification ====================
        //
        // 用 RegisterDeviceNotification 主动向 Windows 注册 HID 设备通知，
        // 让窗口主动订阅触摸屏等 HID 设备的插拔消息，而非被动等待广播。
        // 相比被动监听 WM_DEVICECHANGE，这种方式：
        //   ① 只接收我们关心的 HID 设备事件，减少无意义触发
        //   ② 消息直接发到本窗口，响应更及时
        //   ③ 在 Windows 10/11 上更可靠

        private IntPtr _deviceNotifyHandle = IntPtr.Zero;

        private void SetupTouchDeviceWatcher()
        {
            try
            {
                var source = PresentationSource.FromDependencyObject(this) as HwndSource;
                if (source != null)
                {
                    source.AddHook(TabletWndProc);
                }

                // 主动向 Windows 注册 HID 设备通知
                RegisterHidDeviceNotification();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"SetupTouchDeviceWatcher failed: {ex}", LogHelper.LogType.Error);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr notificationFilter, uint flags);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterDeviceNotification(IntPtr handle);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public System.Guid dbcc_classguid;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 256)]
            public string dbcc_name;
        }

        private const int DBT_DEVTYP_DEVICEINTERFACE = 5;
        private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0;
        // HID 设备类 GUID（触摸屏、手写笔等都属于 HID 设备）
        private static readonly System.Guid GUID_DEVINTERFACE_HID = new System.Guid("{745a17a0-74d3-11d0-b6fe-00a0c90f57da}");

        private void RegisterHidDeviceNotification()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;

                var devInterface = new DEV_BROADCAST_DEVICEINTERFACE
                {
                    dbcc_size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(DEV_BROADCAST_DEVICEINTERFACE)),
                    dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
                    dbcc_reserved = 0,
                    dbcc_classguid = GUID_DEVINTERFACE_HID
                };

                IntPtr buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(
                    System.Runtime.InteropServices.Marshal.SizeOf(devInterface));
                try
                {
                    System.Runtime.InteropServices.Marshal.StructureToPtr(devInterface, buffer, false);
                    _deviceNotifyHandle = RegisterDeviceNotification(hwnd, buffer, DEVICE_NOTIFY_WINDOW_HANDLE);

                    if (_deviceNotifyHandle == IntPtr.Zero)
                    {
                        int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        LogHelper.WriteLogToFile($"RegisterDeviceNotification failed, error: {err}", LogHelper.LogType.Error);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("HID device notification registered successfully.", LogHelper.LogType.Event);
                    }
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"RegisterHidDeviceNotification failed: {ex}", LogHelper.LogType.Error);
            }
        }

        private void UnregisterHidDeviceNotification()
        {
            try
            {
                if (_deviceNotifyHandle != IntPtr.Zero)
                {
                    UnregisterDeviceNotification(_deviceNotifyHandle);
                    _deviceNotifyHandle = IntPtr.Zero;
                    LogHelper.WriteLogToFile("HID device notification unregistered.", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"UnregisterHidDeviceNotification failed: {ex}", LogHelper.LogType.Error);
            }
        }

        private const int WM_TABLET_ADDED = 0x0708;
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        private IntPtr TabletWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TABLET_ADDED)
            {
                // WM_TABLET_ADDED 是 Windows Tablet PC API 的广播消息，作为辅助检测
                OnTouchDeviceChangeDetected();
            }
            else if (msg == WM_DEVICECHANGE)
            {
                // 来自 RegisterDeviceNotification 订阅的 HID 设备插拔通知
                int wParamInt = wParam.ToInt32();
                if (wParamInt == DBT_DEVICEARRIVAL || wParamInt == DBT_DEVICEREMOVECOMPLETE)
                {
                    OnTouchDeviceChangeDetected();
                }
            }
            return IntPtr.Zero;
        }

        // ==================== 事件和定时器入口 ====================

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            OnTouchDeviceChangeDetected();
        }

        internal void OnTabletDeviceCountChanged()
        {
            OnTouchDeviceChangeDetected();
        }

        #endregion

        #region Super Topmost (仿 Inkeys)

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private System.Threading.Thread _superTopmostThread;
        private volatile bool _superTopmostRunning = false;

        private void ApplySuperTopmostOnStartup()
        {
            try
            {
                if (!Settings.Advanced.IsSuperTopmostEnabled) return;

                var hwnd = new WindowInteropHelper(this).Handle;
                ApplyWindowStyles(hwnd);
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);

                // 仿 Inkeys：启动后台线程持续维持置顶状态
                _superTopmostRunning = true;
                _superTopmostThread = new System.Threading.Thread(() => SuperTopmostLoop(hwnd))
                {
                    IsBackground = true,
                    Name = "SuperTopmostThread"
                };
                _superTopmostThread.Start();

                LogHelper.WriteLogToFile("Super Topmost thread started (Inkeys-style).", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ApplySuperTopmostOnStartup failed: {ex}", LogHelper.LogType.Error);
            }
        }

        private void ApplyWindowStyles(IntPtr hwnd)
        {
            // 设置 WS_EX_LAYERED 和 WS_EX_NOACTIVATE，仿 Inkeys 方式
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            bool changed = false;

            if ((exStyle & WS_EX_LAYERED) == 0)
            {
                exStyle |= WS_EX_LAYERED;
                changed = true;
            }
            if ((exStyle & WS_EX_NOACTIVATE) == 0)
            {
                exStyle |= WS_EX_NOACTIVATE;
                changed = true;
            }

            if (changed)
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

        private void SuperTopmostLoop(IntPtr hwnd)
        {
            // 仿 Inkeys IdtWindow.cpp TopWindow() 循环
            while (_superTopmostRunning)
            {
                try
                {
                    // 检查并恢复 WS_EX_LAYERED 和 WS_EX_NOACTIVATE 样式
                    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    int desired = exStyle;

                    if ((desired & WS_EX_LAYERED) == 0)
                        desired |= WS_EX_LAYERED;
                    if ((desired & WS_EX_NOACTIVATE) == 0)
                        desired |= WS_EX_NOACTIVATE;

                    if (desired != exStyle)
                        SetWindowLong(hwnd, GWL_EXSTYLE, desired);

                    // 检查窗口是否可见，若被隐藏则重新显示
                    if (!System.Windows.Interop.ComponentDispatcher.IsThreadModal)
                    {
                        // 跳过正在绘制时的置顶，避免闪烁（仿 Inkeys 的 StrokeImageList 检查）
                        if (inkCanvas == null || inkCanvas.Strokes == null || inkCanvas.Strokes.Count == 0)
                        {
                            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                        }
                    }
                }
                catch
                {
                    // 静默忽略线程中的异常
                }

                // 延迟 3 秒
                for (int i = 0; i < 30; i++)
                {
                    if (!_superTopmostRunning) break;
                    System.Threading.Thread.Sleep(100);
                }
            }
        }

        private void StopSuperTopmostLoop()
        {
            _superTopmostRunning = false;
            _superTopmostThread = null;
        }

        #endregion

        #region WM_POINTER Input

        /// <summary>WM_POINTER 接管触控后设为 true，WPF 触控事件直接跳过</summary>
        internal bool IsPointerActive { get; private set; } = false;

        private Helpers.PointerInputManager _pointerInput;

        // 图形绘制/选择拖拽的触控状态
        private Point _touchDownPos;
        private bool _isTouchDownForShape;
        private uint _shapePointerId;
        private readonly Dictionary<uint, Point> _modeTouchPositions = new Dictionary<uint, Point>();

        private void InitPointerInput()
        {
            try
            {
                _pointerInput = new Helpers.PointerInputManager(this, inkCanvas);

                if (TryEnablePointerInput()) return;

                // 首次失败，延迟 1 秒后重试（窗口初始化完成后再尝试子类化）
                LogHelper.WriteLogToFile("WM_POINTER init initial attempt failed — retrying...", LogHelper.LogType.Event);
                Task.Delay(1000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => TryEnablePointerInput());
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"WM_POINTER init error: {ex}", LogHelper.LogType.Error);
            }
        }

        private bool TryEnablePointerInput()
        {
            if (IsPointerActive) return true;
            if (!_pointerInput.Enable()) return false;

            // WM_POINTER 仅用于消息监控（统计消息量）
            // 触控完全由 WPF 原生处理，不拦截任何触控消息
            LogHelper.WriteLogToFile("WM_POINTER monitor active — WPF handles touch natively.", LogHelper.LogType.Event);
            return true;
        }

        /// <summary>WM_POINTER 触控动作回调，根据当前模式决定行为</summary>
        private bool HandlePointerTouchAction(uint pointerId, float x, float y, float pressure, bool isUp, bool isNew)
        {
            // 1) 图形绘制模式
            if (drawingShapeMode != 0)
            {
                if (isUp)
                {
                    if (_isTouchDownForShape)
                    {
                        inkCanvas_MouseUp(null, null);
                        _isTouchDownForShape = false;
                    }
                }
                else if (isNew)
                {
                    if (NeedUpdateIniP()) iniP = new Point(x, y);
                    _touchDownPos = new Point(x, y);
                    _isTouchDownForShape = true;
                    _shapePointerId = pointerId;
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
                else if (_isTouchDownForShape && pointerId == _shapePointerId)
                {
                    MouseTouchMove(new Point(x, y));
                }
                _modeTouchPositions[pointerId] = new Point(x, y);
                return true;
            }

            // 2) 选择模式 + 选中内容 → 单指拖拽
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select &&
                (inkCanvas.GetSelectedStrokes().Count > 0 ||
                 InkCanvasElementsHelper.GetSelectedElements(inkCanvas).Count > 0))
            {
                if (!isUp && !isNew && _modeTouchPositions.TryGetValue(pointerId, out var prev))
                {
                    double dx = x - prev.X;
                    double dy = y - prev.Y;
                    if (Math.Abs(dx) > 0.5 || Math.Abs(dy) > 0.5)
                    {
                        var m = new Matrix();
                        m.Translate(dx, dy);
                        foreach (Stroke s in inkCanvas.GetSelectedStrokes())
                            s.Transform(m, false);
                        foreach (UIElement el in InkCanvasElementsHelper.GetSelectedElements(inkCanvas))
                            ApplyElementMatrixTransform(el, m);
                    }
                }
                _modeTouchPositions[pointerId] = new Point(x, y);
                if (isUp) _modeTouchPositions.Remove(pointerId);
                return true;
            }

            // 3) 默认 → 普通笔画绘制（让 PointerInputManager 处理）
            if (isUp) _modeTouchPositions.Remove(pointerId);
            return false;
        }

        private void HandlePointerGesture(double scale, double rotate, double tx, double ty, double cx, double cy)
        {
            if (!Settings.Gesture.IsEnableTwoFingerGesture) return;
            if (isInMultiTouchMode) return;
            if (inkCanvas.Strokes.Count == 0) return;

            Matrix m = new Matrix();
            var center = new Point(cx, cy);

            if (Settings.Gesture.IsEnableTwoFingerGestureTranslateOrRotation)
            {
                if (Settings.Gesture.IsEnableTwoFingerZoom)
                    m.ScaleAt(scale, scale, center.X, center.Y);
                if (Settings.Gesture.IsEnableTwoFingerRotation)
                    m.RotateAt(rotate, center.X, center.Y);
                if (Settings.Gesture.IsEnableTwoFingerTranslate)
                    m.Translate(tx, ty);

                var elements = InkCanvasElementsHelper.GetAllElements(inkCanvas);
                foreach (UIElement element in elements)
                    ApplyElementMatrixTransform(element, m);
            }

            if (Settings.Gesture.IsEnableTwoFingerZoom)
            {
                foreach (Stroke stroke in inkCanvas.Strokes)
                {
                    stroke.Transform(m, false);
                    try { stroke.DrawingAttributes.Width *= scale; stroke.DrawingAttributes.Height *= scale; }
                    catch { }
                }
            }
            else
            {
                foreach (Stroke stroke in inkCanvas.Strokes)
                    stroke.Transform(m, false);
            }

            foreach (Circle circle in circles)
            {
                double d = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(),
                    circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                circle.R = d;
                circle.Centroid = new Point(
                    (circle.Stroke.StylusPoints[0].X + circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                    (circle.Stroke.StylusPoints[0].Y + circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2);
            }
        }

        #endregion

        #endregion Definations and Loading
    }
}