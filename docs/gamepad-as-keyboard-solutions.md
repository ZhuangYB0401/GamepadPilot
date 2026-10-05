# 手柄当键盘用：方案调研

> 如果目标是“用手柄输入文字”，请看同目录的 `gamepad-typing-solutions.md`；本文件主要覆盖按键/鼠标映射和底层输出路线。


> 调研日期：2026-09-25  
> 面向场景：Windows 桌面、PC 游戏、HTPC、无障碍输入，以及 GamepadPilot 后续架构选型。

## 1. 先明确“当键盘用”到底指什么

不同需求对应的技术路线差别很大，建议先按下面四类拆开：

| 需求 | 典型场景 | 推荐路线 |
| --- | --- | --- |
| 手柄按键触发键盘按键 | 旧游戏不支持手柄、快捷操作、宏 | 用户态映射软件 + SendInput |
| 手柄控制桌面鼠标和文字输入 | HTPC、客厅电脑、无障碍 | Controller Companion、Steam Desktop Layout、自研映射器 |
| 系统把设备识别为真正键盘 | BIOS、登录界面、部分游戏/反作弊环境 | 外置 HID 桥接器，或已签名虚拟 HID 驱动 |
| 模拟成一个手柄 | 游戏只认 XInput/DS4，不接受键鼠 | ViGEmBus、DS4Windows、Steam Input；这不是“手柄转键盘” |

关键结论：

- 只让普通 Windows 程序收到键盘事件，`XInput/SDL + SendInput` 最简单，当前 GamepadPilot 已属于这条路线。
- 要让 BIOS、登录界面或按 HID 设备识别的软件看到键盘，必须使用虚拟 HID、内核级键盘注入，或外置硬件 HID 桥。
- `ViGEmBus` 输出的是虚拟 Xbox/DS4 手柄，不是虚拟键盘；它适合“键鼠转手柄”或“统一手柄协议”，不能直接解决手柄转键盘。
- 任何键盘注入方案都不应被描述为反作弊安全。虚拟 HID 和内核驱动同样可能被检测、拦截或违反游戏服务条款。

## 2. 现成软件方案速选

### 2.1 Windows

| 方案 | 类型/费用 | 输入 | 输出 | 适用与注意 |
| --- | --- | --- | --- | --- |
| [reWASD](https://rewasd.com/) | 商业付费 | Xbox、DualSense、DualShock、Switch 等 | 键盘、鼠标、宏、虚拟手柄 | 功能最完整之一，支持组合、分层、连发、按应用配置；需要安装驱动，反作弊兼容性需逐游戏确认 |
| [JoyToKey](https://joytokey.net/en/) | 共享软件/付费 | DirectInput、XInput | 键盘、鼠标、宏 | 经典、轻量、配置直观，适合手柄转键盘；界面和架构较老，手柄覆盖不如新方案 |
| [AntiMicroX](https://github.com/AntiMicroX/antimicrox) | 开源 | SDL2 支持的多种手柄 | 键盘、鼠标、宏 | Windows/Linux 均可，适合学习和二次开发；需要核对许可证、手柄映射库和当前维护状态 |
| [Controller Companion](https://store.steampowered.com/app/367670/Controller_Companion/) | 商业付费 | XInput 手柄 | 键盘、鼠标、桌面操作 | 面向 HTPC/桌面控制，带屏幕键盘；不是通用游戏内映射器 |
| [Gopher360](https://github.com/Gopher360/Gopher360) | 开源 | Xbox/XInput | 鼠标、键盘 | 体量小，适合阅读 Windows 用户态实现；功能相对基础 |
| [Joystick Gremlin](https://github.com/WhiteMagic/JoystickGremlin) | 开源 | 摇杆、HOTAS、手柄 | vJoy、键盘、鼠标 | 高级玩家向，支持脚本、模式、曲线；通常和 vJoy 配合，配置复杂度高 |
| [JoyShockMapper](https://github.com/JibbSmart/JoyShockMapper) | 开源 | DualShock/DualSense、Switch 手柄、陀螺仪 | 键盘、鼠标 | 陀螺仪瞄准和竞技映射很强，也可作为高级输入算法参考；普通桌面映射并非其主要 UI 定位 |
| [DS4Windows](https://github.com/Ryochan7/DS4Windows) | 开源 | DualShock 4、DualSense | 虚拟 DS4/Xbox、键盘、鼠标、宏 | Sony 手柄首选之一；通常与 ViGEm/驱动组件配合，适合研究 Sony 设备兼容层 |
| [DSX](https://store.steampowered.com/app/1257110/DSX/) | 商业付费 | DualSense | 虚拟手柄、宏、部分键盘映射 | DualSense 专属功能丰富，含灯效、触觉和自适应扳机；键盘映射不是唯一重点 |
| [ShikiPad](https://github.com/ForestOuting/ShikiPad) | 开源 | DualSense | 键盘、鼠标 | 面向 DualSense 的 Windows 映射器，使用 Interception；可作为内核级注入案例 |
| [Xbox Accessories](https://www.microsoft.com/store/apps/9NBLGGH30XJ3) | 官方免费 | Xbox 手柄 | 主要是手柄按钮重映射/配置 | 厂商限制较多，不能视为通用的“手柄转键盘”软件；具体键盘映射能力取决于型号和固件 |
| [Steam Input](https://partner.steamgames.com/doc/features/steam_controller) | Steam 内置免费 | Steam 支持的控制器 | 键盘、鼠标、手柄、宏、动作层 | 对 Steam 游戏和 Steam Desktop Layout 很实用；独立于 Steam 的程序和系统级场景覆盖有限 |

### 2.2 Linux

| 方案 | 类型 | 主要方向 | 适用与注意 |
| --- | --- | --- | --- |
| [AntiMicroX](https://github.com/AntiMicroX/antimicrox) | 开源 | 手柄 → 键盘/鼠标 | Linux 桌面最实用的图形化方案之一；Wayland/X11 行为需分别验证 |
| [input-remapper](https://github.com/sezanzeb/input-remapper) | 开源 | 设备输入 → 键盘/鼠标 | 基于 evdev/uinput 的设备级重映射，按键重映射很成熟；游戏手柄摇杆/组合是否满足需求要实测 |
| [xboxdrv](https://github.com/Grumbel/xboxdrv) | 开源、偏旧 | Xbox 手柄 → 键盘/鼠标/虚拟设备 | 配置能力强，但项目历史较久；适合研究 Linux 输入栈，不建议直接当新产品基础 |
| QJoyPad | 开源、旧 | 手柄 → 键盘/鼠标 | 经典 X11 工具，现代 Wayland 和发行版兼容性需确认 |
| [SDL3/SDL2](https://github.com/libsdl-org/SDL) | 库 | 手柄输入抽象 | 不是成品映射器，但适合跨平台读取手柄并统一设备映射数据库 |
| [python-evdev](https://python-evdev.readthedocs.io/) + [uinput](https://docs.kernel.org/input/uinput.html) | 技术路线 | Linux 输入设备 → 虚拟键盘 | 最小可行方案通常几十行；Wayland 下不能只依赖 X11 注入工具 |

### 2.3 macOS

| 方案 | 类型 | 说明 |
| --- | --- | --- |
| [Xbox Controller Mapper](https://github.com/NSEvent/xbox-controller-mapper) | 开源、较新 | 支持多种手柄映射到键盘、鼠标、宏和脚本，可作为 macOS 现代方案参考 |
| [InputConfig](https://github.com/ryleighnewman/InputConfig) | 开源 | 面向无障碍和通用桌面的控制器映射，支持键盘/鼠标 |
| Joystick Mapper | 商业付费 | Mac App Store 常见的通用手柄映射工具，使用前确认系统版本兼容性 |
| ControllerMate | 商业付费、偏旧 | 功能强大，适合复杂映射和自动化；现代 macOS 兼容性需核实 |
| Steam Input | Steam 内置 | 只在 Steam 场景内最可靠；macOS 系统级映射要另找工具 |
| Karabiner-Elements | 开源 | 强在键盘和虚拟 HID，不是通用手柄读取器；可以和其他工具联动，但不是完整方案 |

### 2.4 Android / Android TV

| 方案 | 说明 |
| --- | --- |
| [Key Mapper](https://github.com/keymapperorg/KeyMapper) | 用无障碍和输入事件做按键/按钮重映射；不同系统版本、root/ADB 权限下能力差异较大 |
| Button Mapper | 商业软件为主，偏硬件按键重映射；游戏手柄兼容性取决于设备和系统 |
| Octopus、Mantis 等 | 主要是屏幕触控映射，不是键盘输出，和本调研目标不同 |

### 2.5 硬件适配器

- Cronus Zen、Titan Two、ReaSnow、Mayflash 等适配器通常解决“键鼠 → 手柄”或跨主机协议转换，并不等于“手柄 → 键盘”。
- 部分可编程适配器支持脚本输出键盘/鼠标，但价格高、配置复杂，并可能触发反作弊或平台条款问题。
- 若目标是 BIOS、登录界面或完全独立于宿主软件，RP2040/ESP32/树莓派等自建 USB HID 桥通常更可控。

## 3. 自研技术路线

### 3.1 路线 A：用户态读取手柄 + SendInput 注入键盘

这是 MVP 和最常见的桌面方案。

输入层可选：

- Windows XInput：实现简单，Xbox 兼容手柄最稳定，但只有四个标准槽位，不暴露 Elite 背键、触摸板、陀螺仪和部分厂商扩展按键。
- SDL3/SDL2 GameController：跨平台，支持 Xbox、DualSense、Switch、8BitDo 等大量设备，内置映射数据库，推荐作为扩展后端。
- Raw Input/HID：可读取厂商扩展按键、背键、陀螺仪、触摸板，但需要处理设备协议差异和权限问题。
- Windows.Gaming.Input：适合 UWP/现代 Windows 应用，但对传统 Win32 和厂商扩展支持有限。

输出层可选：

- `SendInput`：最通用，支持键盘、鼠标、滚轮、媒体键；实现简单，不需要驱动。
- `keybd_event`/`mouse_event`：旧 API，不推荐新项目使用。
- AutoHotkey v2：适合原型、个人脚本和快速验证；不建议作为复杂商业产品唯一基础。
- Python `pynput`/`keyboard`、Rust `enigo` 等：适合验证和跨平台原型，性能和可维护性要评估。

优点：

- 无驱动、便携、安装成本低。
- 调试容易，配置和宏系统可以完全放在用户态。
- 适合桌面软件、浏览器、办公软件、部分游戏。

限制：

- 低完整性进程通常不能向管理员程序注入输入，这是 Windows UIPI 设计。
- 使用 Raw Input 独占或高完整性运行的游戏可能忽略 `SendInput`。
- 无法让设备在 BIOS、登录界面或设备管理器里显示为真实键盘。
- 反作弊可能拦截或判定风险；不要承诺兼容所有在线游戏。

### 3.2 路线 B：虚拟 HID/内核级注入

这一路线追求更高的系统兼容性，但工程和分发成本明显更高。

Windows：

- Virtual HID Framework（VHF）+ KMDF：创建虚拟 HID 键盘/鼠标设备，最接近“真键盘”体验。
- Interception：键盘/鼠标过滤驱动，常用于更底层的输入注入；需要安装驱动，可能和反作弊、远程桌面、其他输入工具冲突。
- 内核驱动签名、安装/卸载、Secure Boot、系统更新兼容都必须纳入发布流程。
- 不建议默认随应用捆绑驱动；更适合作为高级可选组件，或独立安装包。

Linux：

- `evdev` 读取设备，`uinput` 创建虚拟键盘/鼠标，是 Linux 上最标准的用户态虚拟输入方案。
- Wayland 下应优先使用输入设备/libinput/uinput，而不是依赖 X11 的 `xdotool`。
- 需要 udev 权限规则；系统服务化和多用户会引入权限边界。

macOS：

- `IOHIDUserDevice`/DriverKit 可以创建用户态或受控的虚拟 HID 设备，但签名、权限和系统版本适配较复杂。
- 只做应用内快捷键时，优先使用系统事件 API；跨应用系统级注入再考虑虚拟 HID。

### 3.3 路线 C：外置硬件 HID 桥

原理：单片机读取手柄输入，再通过 USB/BLE 向电脑上报标准键盘/鼠标 HID。

常见平台：

- RP2040/Pico + TinyUSB 或 CircuitPython `usb_hid`。
- ESP32-S3 + TinyUSB/HID 示例。
- Arduino Leonardo/Micro/Pro Micro；较老的 AVR 型号资源有限。
- STM32 + TinyUSB/LUFA。
- 树莓派 Zero/4/5 通过 Linux USB Gadget 模拟 HID 键盘。
- [TinyUSB](https://github.com/hathach/tinyusb) 和 [GP2040-CE](https://github.com/OpenStickCommunity/GP2040-CE) 适合研究 USB 描述符和复合设备。

优点：

- 对宿主系统最透明，能进入 BIOS、登录界面和多数不走用户态注入的环境。
- 不需要在电脑上安装驱动或常驻软件。
- 可以把映射、宏、配置持久化到硬件。

限制：

- 需要硬件、供电、USB 直连或蓝牙配对。
- 固件开发、HID 描述符、设备兼容和固件升级都要维护。
- 无线手柄同时连接电脑和桥接器时需要额外转发或双设备方案。
- 仍然不等同于反作弊豁免。

### 3.4 路线 D：虚拟手柄，不是虚拟键盘

- [ViGEmBus](https://github.com/nefarius/ViGEmBus) / Nefarius.ViGEm.Client 可以创建虚拟 Xbox/DS4 手柄。
- 适合把键盘、鼠标或其他设备模拟成手柄，或者把不同手柄统一成一种协议。
- 如果目标游戏只接受手柄，虚拟手柄有价值；如果目标软件需要键盘事件，仍要另有键盘输出层。
- 驱动安装和反作弊风险需要单独评估。

### 3.5 路线 E：平台内置/厂商工具

- Steam Input：最适合 Steam 游戏；支持动作层、组合键、摇杆/陀螺仪映射到键盘鼠标，Desktop Layout 可用于桌面。
- Xbox Accessories：官方配置工具，主要服务于 Xbox 手柄按钮/配置，通用键盘映射能力有限。
- DualSense 官方/厂商工具：主要面向灯效、触觉、自适应扳机，不等于通用手柄转键盘。
- 8BitDo、雷蛇、飞智等厂商软件：可能有宏、键盘模式或按键映射，但设备型号差异大，必须逐款实测。

## 4. 方案对比矩阵

| 方案/路线 | 输出类型 | 是否要驱动 | 跨应用 | BIOS/登录界面 | 宏/分层 | 反作弊友好度 | 适合场景 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| SendInput 用户态 | 合成键盘事件 | 否 | 通常可以 | 否 | 自研可实现 | 低到中，取决于游戏 | 桌面、办公、普通游戏 |
| Interception | 内核级键盘事件 | 是 | 可以 | 通常否或有限 | 自研可实现 | 不确定，可能被拦 | 需要更底层注入的高级用户 |
| VHF 虚拟 HID | 虚拟 HID 设备 | 是、需签名 | 可以 | 部分场景可以 | 自研可实现 | 不确定，可能被检测 | 产品级虚拟外设 |
| 硬件 HID 桥 | 真实 USB/BLE HID | 否（宿主侧） | 可以 | 可以 | 固件实现 | 仍可能被检测 | 全系统、BIOS、零宿主软件 |
| ViGEmBus | 虚拟 Xbox/DS4 | 是 | 可以 | 通常否 | 客户端实现 | 不确定 | 键鼠转手柄、统一手柄协议 |
| Steam Input | 应用内键盘/手柄事件 | 通常否 | 主要限 Steam | 否 | 强 | 依游戏而定 | Steam 游戏和桌面布局 |
| AntiMicroX/Gopher360 | 合成键盘/鼠标事件 | 否 | 可以 | 否 | 中等/基础 | 依游戏而定 | 免费桌面映射 |
| reWASD/JoyToKey | 合成输入/虚拟手柄 | 视方案 | 可以 | 通常否 | 强/中等 | 依游戏而定 | 成品方案 |

## 5. GamepadPilot 的针对性建议

当前实现的路线是：`XInput 轮询 → MappingEngine → WindowsInputSink/SendInput`。这套架构适合先做稳定、易分发的桌面版，建议保留并逐步扩展。

### 5.1 短期优先级

1. 保持输入和输出接口解耦：继续以 `IGamepadSource`、`IActionDispatcher`、`IMacroOutput` 为边界，不要把 `SendInput` 调用渗进映射引擎。
2. 增加兼容性提示：检测前台进程是否以管理员权限运行；如果当前进程无法注入，明确提示用户，而不是静默失效。
3. 完善安全释放：休眠、锁屏、UAC、全屏、拔出手柄、崩溃、紧急停止时释放全部按键和鼠标按钮。
4. 增加键盘布局处理：区分虚拟键和扫描码，避免非美式布局和游戏内键位错乱。
5. 补充自动化测试：为映射状态机、长按/双击/组合、死区、曲线、配置迁移建立不依赖硬件的行为测试。
6. 建立兼容矩阵：Windows 10/11、Xbox 手柄、DualSense、Switch Pro、8BitDo、USB/蓝牙、普通权限/管理员权限、前台普通程序/Raw Input 游戏。

### 5.2 中期演进

1. 增加 SDL3/SDL2 输入后端：保留 XInput 快速路径，同时支持更多设备和映射数据库。
2. 增加 Raw HID 后端：用于背键、陀螺仪、触摸板和厂商扩展按键。
3. 抽象输出后端：`SendInput` 作为默认；预留 Interception、VHF、串口/BLE 硬件桥接口。
4. 引入“输出能力探测”：启动时检测能否注入普通窗口、管理员窗口和当前前台程序，并给出可执行建议。
5. 增加动作系统：Tap、Hold、Long Press、Double Press、Repeat、Chord、Layer、模式切换、按应用配置。
6. 增加无障碍能力：桌面导航、屏幕键盘、按键停留、单手布局、可调连发和宏节奏。

### 5.3 不建议做的事

- 不建议把内核驱动或 Interception 作为默认安装项；这会显著放大安全、签名、卸载和反作弊风险。
- 不建议把 `ViGEmBus` 当成键盘输出方案；它解决的是虚拟手柄，不是键盘事件。
- 不建议宣传“反作弊兼容”或“绕过检测”。正确表述是“在允许输入注入的环境下可用，需要逐程序测试”。
- 不建议直接复制 GPL 项目代码到闭源或许可证不兼容的项目中；先做许可证审查，再决定参考、链接还是重新实现。

## 6. 推荐的自研实现骨架

### 6.1 Windows 用户态版本

```text
手柄设备
  -> XInput / SDL / Raw HID 输入后端
  -> 统一 GamepadSnapshot
  -> 映射状态机（deadzone、曲线、tap/hold/chord/layer）
  -> IActionDispatcher
     -> SendInput 键盘/鼠标输出
     -> 宏执行器
     -> 可选的 Interception/VHF/硬件桥输出
```

关键工程点：

- 输入轮询和输出注入分离，避免 UI 卡顿影响手感。
- 对摇杆保留亚像素余量，避免低速移动丢失。
- 维护按下计数，处理多个手柄输入映射到同一个键的情况。
- 对宏和连续动作设置停止/看门狗，防止按键卡住。
- 使用前台进程和白名单/黑名单规则自动切换配置。

### 6.2 Linux 用户态版本

```text
/dev/input/event* 或 SDL 手柄事件
  -> evdev / SDL
  -> 映射状态机
  -> /dev/uinput 创建虚拟键盘/鼠标
```

关键工程点：

- 用 udev 规则授予设备访问权限。
- 同时考虑 X11、Wayland 和纯 TTY；不要把 `xdotool` 当作唯一输出。
- 用 `uinput` 创建标准键盘/鼠标设备，宿主程序看到的是正常输入设备。

### 6.3 硬件桥版本

```text
手柄 USB/BLE 输入
  -> 单片机解析 HID 报告
  -> 板载映射/宏/配置
  -> USB HID 或 BLE HID 键盘/鼠标报告
```

关键工程点：

- 先确定目标手柄的 USB/BLE 协议，XInput 并不能直接告诉单片机所有厂商按键。
- 设计 HID 报告描述符时同时考虑键盘、鼠标和消费者控制键。
- 最好使用 USB 有线连接验证延迟和稳定性，再做 BLE。
- 固件配置接口要支持升级，不要只能刷线改映射。

## 7. 常用技术资料

### Windows

- [XInput 入门](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput)
- [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
- [Virtual HID Framework](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/virtual-hid-framework--vhf-)
- [ViGEmBus](https://github.com/nefarius/ViGEmBus)
- [Interception](https://github.com/oblitum/Interception)

### 跨平台输入

- [SDL](https://github.com/libsdl-org/SDL)
- [hidapi](https://github.com/libusb/hidapi)
- [TinyUSB](https://github.com/hathach/tinyusb)

### Linux

- [uinput 文档](https://docs.kernel.org/input/uinput.html)
- [python-evdev](https://python-evdev.readthedocs.io/)
- [input-remapper](https://github.com/sezanzeb/input-remapper)

### 可参考的开源实现

- [AntiMicroX](https://github.com/AntiMicroX/antimicrox)：手柄映射、图形界面、跨平台输入。
- [Gopher360](https://github.com/Gopher360/Gopher360)：小体量 Windows 桌面映射。
- [DS4Windows](https://github.com/Ryochan7/DS4Windows)：Sony 手柄、虚拟手柄和配置体系。
- [JoyShockMapper](https://github.com/JibbSmart/JoyShockMapper)：陀螺仪、摇杆曲线和高级映射。
- [Joystick Gremlin](https://github.com/WhiteMagic/JoystickGremlin)：脚本、模式、HOTAS。
- [ShikiPad](https://github.com/ForestOuting/ShikiPad)：DualSense 到 Windows 键盘/鼠标、Interception 实践。
- [Xbox Controller Mapper](https://github.com/NSEvent/xbox-controller-mapper)：macOS 现代映射参考。
- [InputConfig](https://github.com/ryleighnewman/InputConfig)：macOS 无障碍映射参考。

## 8. 选型结论

- 以 GamepadPilot 当前目标看，继续使用“XInput + 映射引擎 + SendInput”是最合理的默认路线，成本和风险最低。
- 下一阶段最值得做的是增加 SDL3/SDL2 或 Raw HID 输入后端，覆盖更多手柄、背键、陀螺仪和触摸板。
- 如果必须支持 BIOS、登录界面或更底层的键盘设备语义，优先考虑外置 RP2040/ESP32 HID 桥；其次才是签名虚拟 HID 驱动。
- 如果需要 Steam 游戏和桌面布局，先比较 Steam Input，很多场景无需自研。
- 如果目标是 DualSense/DualShock，先评估 DS4Windows、DSX、ShikiPad 和 JoyShockMapper，再决定哪些能力要自研。
- 任何涉及内核驱动、反作弊或游戏服务条款的方案，都应作为可选项并明确风险，不应默认捆绑。

