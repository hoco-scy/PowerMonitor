# PowerMonitor

Windows 功耗检测器 — 实时监控 CPU/GPU 功耗、整机功耗、各进程耗电。

## 功能

- CPU 功耗实时监控（Package/Core 功耗、使用率）
- GPU 功耗实时监控（NVIDIA/AMD/Intel，多卡分别显示，功耗、温度、显存）
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

## 功耗计算说明

系统总功耗采用三级策略：

1. **电池放电功率**（最准确）：直接读取电池传感器的放电速率
2. **组件传感器求和**：CPU Package 功耗（含集显） + 独显功耗 + 主板/内存/存储等附加传感器
3. **基线估算**：CPU Package + 独显 + 30W 基线（无附加传感器时的兜底）

- Intel CPU 的 RAPL "Package" 域覆盖整个 SoC：CPU 核心 + 集显 + Uncore。因此 CPU 功耗读数已包含集显。
- 独显（dGPU，如 NVIDIA/AMD）功耗独立计算，不重复计入。

## 多 GPU 支持

自动检测并分别显示所有 GPU（集显 + 独显）：

- **NVIDIA** → 始终识别为独显 (dGPU)
- **Intel** → 名称含 "Arc" 为独显，其余为集显 (iGPU)
- **AMD** → 名称含 "Graphics" 且不含 "RX"/"Pro" 为集显，其余为独显

每个 GPU 独立显示功耗、温度、显存使用率和历史折线图。

## 已知问题

### 1. 进程功耗延迟出现

**现象**: Top Processes 列表前几秒显示 "sampling..."。

**原因**: 进程监控使用 `TotalProcessorTime` 差值计算 CPU 占用。首次启动需要两次采样（间隔 2 秒）才能计算出差值。工作集 < 10MB 的进程会被跳过。采样线程以最低优先级运行，不影响 UI 响应。

### 2. 窗口拖拽偶尔失效

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
