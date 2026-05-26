# PowerMonitor

Windows 桌面功耗监控悬浮窗 — 实时显示 CPU/GPU 功耗、整机功耗、进程耗电排行。

## 下载

在 [Releases](https://github.com/hoco-scy/PowerMonitor/releases) 页面下载：

| 版本 | 大小 | 说明 |
|------|------|------|
| `PowerMonitor-standalone.zip` | ~70MB | 自包含，无需安装运行时，开箱即用 |
| `PowerMonitor-framework.zip` | ~6.5MB | 需要 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |

> 两个版本均需**管理员权限**读取硬件传感器，启动时会弹出 UAC 提示。

## 功能

- CPU 功耗监控（RAPL Package/Core 功耗、使用率、温度）
- GPU 功耗监控（NVIDIA/AMD/Intel，多卡独立显示，功耗/温度/显存）
- 整机功耗估算（动态基线 + 传感器 + 外设估算）
- 进程功耗排行（基于 CPU 占用 × TDP 估算，Top 8）
- 滚动折线图（CPU/GPU/整机，120 秒历史）
- 弧形仪表盘总功耗可视化
- 系统托盘常驻，任务栏不显示
- 窗口可拖拽、可锁定

## 功耗计算

整机功耗采用三级策略：

1. **电池放电**（最准确）：直接读取电池放电速率
2. **传感器求和**：CPU Package + dGPU + 主板/内存/存储等传感器读数 + 外设估算（屏幕/Wi-Fi/蓝牙）+ 动态基线
3. **基线兜底**：无传感器时，用 CPU/GPU 负载 × TDP 估算 + 基线

动态基线会根据已有传感器类别自动扣减，避免双重计算。CPU/GPU 回退路径使用 idle + load 模型，低负载估算更准确。

## 多 GPU 支持

自动检测并分别显示所有 GPU：

- **NVIDIA** → 始终为独显 (dGPU)
- **Intel** → 名称含 "Arc" 为独显，其余为集显 (iGPU)
- **AMD** → 名称含 "RX"/"Pro"/"W" 为独显，其余为集显

## 从源码构建

```powershell
# 运行
dotnet run --project src/PowerMonitor.UI

# 打包自包含版本
dotnet publish src/PowerMonitor.UI -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-standalone

# 打包框架依赖版本（需 .NET 8 Runtime）
dotnet publish src/PowerMonitor.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish-framework
```

## 技术栈

- C# / .NET 8 / WPF
- LibreHardwareMonitorLib（硬件传感器）
- CommunityToolkit.Mvvm
- H.NotifyIcon.Wpf（系统托盘）

## 项目结构

```
src/
├── PowerMonitor.Core/          # 硬件监控核心
│   ├── Hardware/               # CPU/GPU/外设传感器读取
│   ├── Process/                # 进程功耗估算
│   ├── Models/                 # 数据模型
│   └── Services/               # 监控服务编排
│
└── PowerMonitor.UI/            # WPF 悬浮窗
    ├── Controls/               # MiniGraph, PowerGauge, ProcessList
    ├── Rendering/              # StreamGeometry 高性能绘图
    ├── ViewModels/             # MVVM 绑定
    ├── Services/               # 系统托盘
    └── Themes/                 # 暗色主题
```
