<div align="center">

<img src="https://raw.githubusercontent.com/Sylinko/Motion.Avalonia/main/img/icon.png" alt="Motion.Avalonia Logo" width="128" height="128" />

<h1>Motion.Avalonia</h1>

**面向 Avalonia 的声明式、可双向寻址实时布局动画框架**

<p align="center">
  <a href="https://www.nuget.org/packages/Motion.Avalonia/"><img src="https://img.shields.io/nuget/v/Motion.Avalonia.svg?style=flat-square" alt="NuGet"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8.0%20%7C%2010.0%20%7C%2011.0-512BD4.svg?style=flat-square" alt=".NET 8, .NET 10, and .NET 11"></a>
  <a href="https://avaloniaui.net/"><img src="https://img.shields.io/badge/Avalonia-12%2B-8B44AC.svg?style=flat-square" alt="Avalonia 12+"></a>
  <img alt="Status: early preview" src="https://img.shields.io/badge/status-early%20preview-orange?style=flat-square">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square" alt="MIT License"></a>
</p>

[English](README.md) | 简体中文

</div>

---

Motion.Avalonia 让稳定的 Avalonia 控件可以在结构完全不同的实时布局树之间平滑过渡。你只需使用带有 `MotionSlot` 占位符的常规 Panel 布局树，并根据交互输入选择合适的 Motion 模型：

- `StateMotion`：在完整的命名状态之间过渡（支持中途打断与平滑重定向）；
- `SequenceMotion`：根据外部传入的归一化进度采样稀疏关键帧；
- `TimelineMotion`：按绝对时间点采样稀疏关键帧，并提供帧同步播放器控制。

框架全程执行真实的 `Measure` 与 `Arrange`：文本会在中间宽度下自然换行，数据绑定保持活跃，宿主窗口尺寸变化也会立刻反映到目标布局终点。它既不把控件离屏光栅化成位图，也不用缩放变换来伪造布局。

## 核心特性

- 全程保持目标控件实例与内部状态（文本框输入、选区、焦点、动画控件等）不变
- 以实时响应式的 `DataTemplate` 作为布局数据源
- 每一中间帧均执行真实测量与排列
- 命名状态过渡支持平滑中途打断与反向重定向
- 支持可双向寻址的进度关键帧与绝对时间线关键帧
- 按属性维度的稀疏插值轨道（Track）
- 布局边界与样式属性统一在同一条插值管线上求值
- 支持有向状态转换（`MotionEdge` 独立配置去程/返程时长与缓动）
- 强类型自定义插值器（`MotionInterpolator`）
- 自动复用 Avalonia 内置 `Animator<T>` 实现
- 通过 Avalonia 选择器语法（`TargetStyles`）为目标嵌套子元素设置样式动画
- 基于关键帧的相对 Z-Order（层级）计算
- 采用 Animation 优先级绑定，Motion 结束后自动恢复底层绑定或本地值
- 无位图快照、无缩放伪布局
- 支持 `net8.0`、`net10.0`、`net11.0` 与 `Avalonia 12.0.0+`

## 快速开始

安装 NuGet 包：

```shell
dotnet add package Motion.Avalonia
```

定义一个 `StateMotion`，并将需要参与动画的真实控件作为 `MotionPanel` 的直接子元素：

```xml
<UserControl
  xmlns="https://github.com/avaloniaui"
  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
  xmlns:motion="using:Motion.Avalonia">

  <motion:MotionPanel>
    <motion:MotionPanel.Motion>
      <motion:StateMotion
        x:Name="EditorMotion"
        State="Quick"
        Duration="0:0:0.35"
        Easing="CubicEaseInOut">

        <motion:MotionState Key="Quick">
          <DataTemplate>
            <Grid>
              <motion:MotionSlot
                x:Name="Editor"
                MaxWidth="600"
                HorizontalAlignment="Center"/>
            </Grid>
          </DataTemplate>
        </motion:MotionState>

        <motion:MotionState Key="Advanced">
          <DataTemplate>
            <DockPanel HorizontalSpacing="24">
              <motion:MotionSlot
                x:Name="Editor"
                MaxWidth="420"
                DockPanel.Dock="Left"/>
              <motion:MotionSlot x:Name="Preview"/>
            </DockPanel>
          </DataTemplate>
        </motion:MotionState>
      </motion:StateMotion>
    </motion:MotionPanel.Motion>

    <Border x:Name="Editor"/>
    <Border x:Name="Preview"/>
  </motion:MotionPanel>
</UserControl>
```

将 `EditorMotion.State` 切换为 `"Advanced"` 即可触发平滑布局过渡。

模板与真实控件通过以下三条规则关联：

1. 每个目标控件必须是 `MotionPanel` 下的稳定直接子元素。
2. 每个目标控件必须拥有唯一且非空的 `x:Name`。
3. 模板中的 `MotionSlot` 使用与其对应目标控件相同的 `x:Name`。

完整教程请参阅 [Getting Started（英文）](docs/getting-started.md)。

## 选择合适的 Motion 模型

### 命名应用状态切换（`StateMotion`）

当界面在紧凑/展开、快速/高级、编辑/预览等完整状态之间切换时使用 `StateMotion`。某个状态中未出现的 `MotionSlot` 即表示该目标在该状态下缺席（支持进入/离开过渡）。

### 滚动、滑块或手势驱动（`SequenceMotion`）

当外部组件掌握归一化进度（`0..1` / `Cue="0%..100%"`）时使用 `SequenceMotion`：

```xml
<motion:SequenceMotion Progress="{Binding #ProgressSlider.Value}">
  <motion:SequenceMotionKeyFrame Cue="0%">
    <DataTemplate>
      <motion:MotionSlot x:Name="Card" HorizontalAlignment="Left"/>
    </DataTemplate>
  </motion:SequenceMotionKeyFrame>
  <motion:SequenceMotionKeyFrame Cue="100%" Easing="CubicEaseOut">
    <DataTemplate>
      <motion:MotionSlot x:Name="Card" HorizontalAlignment="Right"/>
    </DataTemplate>
  </motion:SequenceMotionKeyFrame>
</motion:SequenceMotion>
```

关键帧是稀疏的：未显式声明的属性不会在该 Cue 上生成关键帧，也不会干扰其他轨道。

### 绝对时间线与播放器控制（`TimelineMotion`）

当动画按绝对 `KeyTime` 编排时使用 `TimelineMotion`。可直接绑定或赋值 `Time` 实现拖动预览，或调用 `TimelineMotion.Player.Play()` 进行帧对齐播放；`Loop` 控制循环，`Duration` 根据最晚关键帧自动推导。

## 工作原理

```text
MotionPanel 目标控件 + 实时布局数据源
                  │
                  ▼
         Motion.Compile(context)
                  │
                  ▼
        长生命周期按属性插值轨道
                  │
                  ▼
Easing → MotionInterpolator → Animation 优先级绑定
                  │
                  ▼
        稳定的宿主容器与真实控件实例
```

编译阶段只发现一次布局与属性通道；随后的进度或时间推进只会更新已有轨道，而不会重建目标控件。`MotionPanel` 在每帧负责测量实时布局树，并将最新目标边界提供给布局轨道。

## 文档目录（英文）

| 章节 | 说明 |
|---|---|
| [Getting Started](docs/getting-started.md) | 构建完整的双模式编辑器布局过渡示例 |
| [Core Concepts](docs/core-concepts.md) | 理解 Target、Layout Source、Channel 与 Presence |
| [States and Slots](docs/states-and-slots.md) | 编写响应式完整状态布局模板 |
| [Motions and Keyframes](docs/motions-and-keyframes.md) | 在 State、Sequence 与 Timeline 之间做选择 |
| [Target Properties](docs/target-properties.md) | 使用快捷属性、Setter、绑定与 TargetStyles |
| [Interpolators](docs/interpolators.md) | 自定义缓动与数值空间插值器 |
| [Recipes](docs/recipes.md) | 将核心原语组合用于常见 UI 动效模式 |
| [Architecture](docs/architecture.md) | 深入 Apply、Track 生命周期与每帧开销 |
| [Limitations and Troubleshooting](docs/limitations-and-troubleshooting.md) | 排查作用域限制与配置异常 |
| [API Reference](docs/api-reference.md) | 查询公开类型与属性契约 |

## 当前适用范围

`Motion.Avalonia` 当前面向单个 `MotionPanel` 下的稳定直接子元素；跨窗口、跨页面或跨祖先容器的共享元素过渡暂不在当前实现范围内。直接 Avalonia 属性（DirectProperty）无法参与属性轨道插值。当前 API 处于早期预览阶段，后续引入物理交互与更高层动效系统时可能继续演进。

## 项目结构与本地构建

构建需要 **.NET SDK 11.0.100**（或更高 `11.0.1xx` 补丁版），所有 NuGet 依赖通过仓库根目录的 `NuGet.config` 直接从 `nuget.org` 还原。

- `Motion.Avalonia` — 核心 Motion、实时布局、播放器与插值原语库
- `Motion.Avalonia.Demo` — 跨平台展示 UI
- `Motion.Avalonia.Demo.Desktop` — Windows、macOS、Linux 桌面宿主
- `Motion.Avalonia.Demo.Android` — Android 宿主
- `Motion.Avalonia.Demo.iOS` — iPhone 与 iPad 宿主
- `Motion.Avalonia.Demo.Browser` — WebAssembly 宿主
- `Motion.Avalonia.Tests` — 行为与生命周期测试

编译核心库并运行测试：

```shell
dotnet restore tests/Motion.Avalonia.Tests/Motion.Avalonia.Tests.csproj --configfile NuGet.config
dotnet build src/Motion.Avalonia/Motion.Avalonia.csproj -c Release --no-restore
dotnet test tests/Motion.Avalonia.Tests/Motion.Avalonia.Tests.csproj -c Release --no-restore
```

运行桌面端展示程序（需拉取子模块，建议克隆时带 `--recursive`）：

```shell
git submodule update --init --recursive
dotnet run --project src/Motion.Avalonia.Demo.Desktop -c Release
```

## 参与贡献

欢迎提交 Issue、设计讨论、示例与 Pull Request。提交讨论或 PR 时请同时说明面向用户的动效问题场景及拟议实现方案。

## 开源协议

`Motion.Avalonia` 基于 [MIT License](LICENSE) 开源。
