# PowerMonitor

Windows 功耗检测器 — 实时监控 CPU/GPU 功耗、整机功耗、各进程耗电。

## 功能

- CPU 功耗实时监控（Package/Core 功耗、使用率）
- GPU 功耗实时监控（NVIDIA/AMD，功耗、温度、显存）
- 系统总功耗估算（CPU + GPU + 基线）
- 各进程功耗排行（基于 CPU 占用 × TDP 估算）
- 60 秒滚动折线图
- 弧形仪表盘总功耗可视化
- 系统托盘常驻，可隐藏到后台
- 窗口可拖拽、可锁定位置

## 技术栈

- C# / .NET 8 / WPF
- LibreHardwareMonitorLib（硬件传感器读取）
- CommunityToolkit.Mvvm（MVVM 框架）
- H.NotifyIcon.Wpf（系统托盘）

## 运行

```powershell
dotnet run --project src/PowerMonitor.UI
```

> 需要**管理员权限**才能读取硬件传感器。启动时会弹出 UAC 提示。

## 项目结构

```
src/
├── PowerMonitor.Core/          # 硬件监控核心（无 WPF 依赖）
│   ├── Models/                 # 数据模型（PowerData, ProcessPowerData）
│   ├── Hardware/               # CPU/GPU 传感器读取
│   ├── Process/                # 进程功耗估算
│   └── Services/               # 监控服务编排器
│
└── PowerMonitor.UI/            # WPF 悬浮窗应用
    ├── Themes/                 # 暗色极客风主题
    ├── Controls/               # MiniGraph, PowerGauge, ProcessList
    ├── Rendering/              # StreamGeometry 高性能绘图
    ├── ViewModels/             # MainViewModel
    ├── Services/               # 系统托盘
    ├── Converters/             # 值转换器
    ├── MainWindow.xaml         # 主悬浮窗
    └── App.xaml                # 应用入口
```

## 已知问题

### 1. 进程监控导致严重卡顿

**现象**: 开启进程功耗监控后，UI 严重卡顿、无法交互、无法关闭。

**原因**: `Process.GetProcesses()` 每秒枚举系统所有进程（通常 200-400 个），对每个进程访问 `TotalProcessorTime` 属性。该操作：
- 对无权限的系统进程会抛出 `AccessDeniedException`，产生大量异常开销
- 创建大量 `Process` 对象造成 GC 压力
- 阻塞后台线程间接影响 UI 响应

**当前状态**: 已做部分优化（5 秒全量扫描 + 增量跟踪），但根本问题未解决。

**可能的解决方案**:
- 使用 Windows ETW (Event Tracing for Windows) 获取进程 CPU 数据，无需逐进程查询
- 使用 `PDH` (Performance Data Helper) API 读取性能计数器
- 仅监控用户态进程，跳过 PID < 1000 的系统进程
- 将进程监控放到独立线程，设置低优先级 (`ThreadPriority.Lowest`)
- 彻底禁用进程监控，仅保留 CPU/GPU 功耗

### 2. 进程功耗显示为 0

**现象**: Top Processes 列表为空或所有进程功耗为 0。

**原因**: 第一次采样没有历史数据可以对比，需要至少两次采样（间隔 3-5 秒）才能计算出差值。此外，工作集 < 10MB 的进程会被跳过。

### 3. GPU 功耗读数为 0

**现象**: GPU 功耗始终显示 0.0W。

**原因**:
- LibreHardwareMonitor 需要管理员权限才能访问 GPU 传感器
- 部分 GPU（尤其是核显）可能不暴露功耗传感器
- NVIDIA GPU 需要安装最新驱动

### 4. 窗口拖拽偶尔失效

**现象**: 鼠标点击标题栏区域偶尔无法拖拽。

**原因**: `DragMove()` 在 `MouseLeftButtonDown` 中调用，如果鼠标点击到了子控件（如按钮、图表），事件不会冒泡到 Window。

## UI 预览

```
┌────────────────────────────┐
│  P O W E R   M O N I T O R │
│  CPU   45.2W  ▓▓▓░░  32%  │
│  GPU  120.5W  ▓▓▓▓░  67%  │
│  SYSTEM [仪表盘]  198.7W  │
│  TOP PROCESSES             │
│    chrome     12.3W ▓▓     │
│    code        8.7W ▓      │
│    discord     3.2W ▓      │
│  1Hz │ Uptime 2h31m       │
└────────────────────────────┘
```
