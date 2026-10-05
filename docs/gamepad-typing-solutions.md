# 用手柄打字：方案调研

> 目标：使用游戏手柄输入文字，包括 Windows 桌面、Steam 游戏、客厅媒体中心以及中文输入。
>
> 先说明一个关键区别：把 A 键映射成字母 A，只是在“按键层面”模拟键盘。真正要用手柄打字，还需要一个屏幕键盘、文字输入界面，或者外接实体小键盘。

## 1. 结论速选

| 使用场景 | 首选方案 | 备选 | 说明 |
| --- | --- | --- | --- |
| Windows 桌面，中英文都要输 | Windows 触摸键盘/屏幕键盘 + 手柄模拟鼠标 | Controller Companion、Keysticks | 最通用，配合微软拼音可以正常输入中文 |
| Windows 自带屏幕键盘的映射器 | JoyXoff | Controller Companion、Keysticks | 用户态映射 + 内置虚拟键盘，可直接用于手柄打字；闭源且更新较旧 |
| Steam 游戏或 Steam Deck | Steam 屏幕键盘 | Steam Input 桌面布局、手柄映射鼠标 | Deck 上体验最好；Windows Big Picture 和普通桌面要实测 |
| 客厅/HTPC 媒体中心 | Kodi 内置屏幕键盘 | Controller Companion、蓝牙小键盘 | Kodi 的输入界面本身就是为遥控器/手柄设计的 |
| Playnite 游戏库 | GamepadDesktop Playnite 扩展 | Windows 触摸键盘 | 扩展带集成虚拟键盘，适合手柄浏览游戏库时搜索 |
| 追求最快打字速度 | Xbox Chatpad/夹持式小键盘 | 蓝牙迷你键盘、手机远程键盘 | 物理按键最快，但硬件兼容性和便携性较差 |
| 无障碍/单手输入 | Dasher、OptiKey、Grid 3 | Windows 屏幕键盘 + 开关控制 | 适合把摇杆当鼠标或把按钮当开关的用户 |
| 完全自研 | QWERTY 网格屏幕键盘 + 预测输入 + IME | 径向键盘、T9、双摇杆和弦输入 | 需要处理焦点、中文候选词和输入法集成 |
| 不想安装软件 | 游戏或主机自带屏幕键盘 | 语音输入 | 主机内置输入法最省事，但只能在对应平台内使用 |

## 2. 现成方案

### 2.1 Steam 屏幕键盘

Steam 的屏幕键盘是 PC 上最完整的免费方案之一。

- Steam Deck：通常按 `STEAM + X` 唤出屏幕键盘，之后可用摇杆、触控板、方向键和按钮操作。
- Windows Big Picture：可以在 Steam Input 配置中给“显示屏幕键盘”绑定按键；入口随 Steam 版本变化，建议按当前版本实测。
- 普通 Windows 桌面：可以先尝试 Steam Desktop Layout；如果屏幕键盘不能进入目标窗口，改用 Windows 触摸键盘或 Controller Companion。
- 布局：提供标准字母/数字/符号键盘，Deck 还支持触控板输入和预测候选。
- 中文：Steam 屏幕键盘本身主要负责发送字符，中文通常依赖 Windows 当前输入法。需要确认微软拼音候选词窗口是否能在屏幕键盘打开时正常操作。
- 限制：独占全屏、反作弊、部分 UWP/管理员程序可能无法接收；游戏内聊天框是否支持也取决于游戏。

适合：Steam 游戏、Steam Deck、使用 Steam 统一管理手柄的用户。

### 2.2 Windows 触摸键盘/屏幕键盘 + 手柄转鼠标

这是最通用的 Windows 方案，尤其适合中文输入。

组件：

- Windows 触摸键盘：任务栏右下角的键盘图标，或 `TabTip.exe`。
- 经典屏幕键盘：`osk.exe`。
- 手柄转鼠标：Gopher360、AntiMicroX、JoyToKey、Steam Desktop Layout、reWASD、Controller Companion 等。
- 中文输入法：Windows 微软拼音、搜狗拼音、QQ 拼音等。

基本映射：

| 手柄输入 | 映射 | 作用 |
| --- | --- | --- |
| 左摇杆 | 鼠标移动 | 移动屏幕键盘光标 |
| A | 鼠标左键 | 点击字母 |
| B | 鼠标右键/退格 | 删除或打开候选菜单 |
| 左/右肩键 | Backspace/Space | 删除/空格 |
| 方向键 | 键盘方向键 | 在字母或候选词之间移动 |
| Start | Enter | 确认 |
| Back | Esc | 关闭候选或键盘 |
| 右摇杆 | 滚轮/鼠标加速 | 长文本浏览 |

优点：

- 不依赖特定游戏；桌面、浏览器、聊天软件都能用。
- 可使用系统输入法的中文预测和候选词。
- `osk.exe`/触摸键盘对管理员程序的兼容性通常比自己合成字符好一些。

缺点：

- 屏幕键盘会占据桌面空间。
- 鼠标模拟需要调好曲线和死区，否则选字母很慢。
- 在独占全屏游戏中通常无法唤出。
- 需要先确认焦点：点击目标输入框，再开始操作屏幕键盘。

### 2.3 Controller Companion

[Controller Companion](https://store.steampowered.com/app/367670/Controller_Companion/) 是面向手柄的 Windows 桌面控制工具，特点是桌面鼠标、键盘模式和屏幕键盘集成度高。

适合：

- HTPC、客厅电脑、Steam Big Picture。
- 不想自己拼接多个映射工具的用户。
- 需要在桌面输入文字、控制播放器和浏览网页。

注意：

- 商业付费软件，主要面向 Windows。
- 中文输入仍依赖 Windows 输入法，实际候选词体验需要实测。
- 和 Steam Input 同时开启时可能发生输入冲突，建议一次只启用一个主要映射层。

### 2.3.1 JoyXoff（自带虚拟键盘）

JoyXoff 是 Diego Ferracini 开发的 Windows 手柄映射工具，公开版本最后可见到 3.58.4.26（2022-04）。它属于：

> 用户态手柄映射 + 合成键盘/鼠标事件 + 内置虚拟键盘

不是虚拟 HID 设备，也不是外置硬件 HID 桥。

它和普通 JoyToKey/AntiMicroX 类工具的区别是：JoyXoff 自身带屏幕键盘，后期版本的功能记录里还出现了：

- Plain virtual keyboard（Xbox One 主题）
- Virtual Keyboard Symbols
- Cycle virtual keyboard language
- Use D-pad for virtual keyboard input
- IME functions
- 多显示器下的虚拟键盘位置修复
- 全屏窗口相关规则和进程/配置规则

因此，JoyXoff 可以算“手柄打字方案”，而不只是“手柄按键映射方案”。

使用定位：

- 适合：Windows 桌面、HTPC、少量文字输入、用摇杆/D-pad 选择字符。
- 中文：后期版本有 IME 相关内容，理论上可以配合 Windows 输入法；但候选词、焦点和 Windows 11 兼容性必须实测。
- 输出方式：仍然是用户态合成键盘/鼠标事件，因此管理员窗口、独占全屏、Raw Input 游戏和反作弊场景的限制与其他用户态映射器相同。
- 维护状态：闭源软件；公开 GitHub 仓库 [oldtools/JoyXoff](https://github.com/oldtools/JoyXoff) 只是未修改的 MSI 安装包镜像，不包含源码。最后可见更新约为 2022 年，使用前要确认官网、数字签名和系统兼容性。

### 2.4 Keysticks

[Keysticks](https://keysticks.net/) 是经典的 Windows 手柄桌面控制工具，可以把 XInput 手柄映射成鼠标或键盘操作。

适合：

- 希望通过手柄控制 Windows 桌面和媒体软件。
- 想使用免费工具、接受较老界面和较慢更新节奏的用户。

用法：

- 先用手柄控制鼠标，再打开 Windows 触摸键盘输入文字。
- 如果软件自身提供键盘模式，也可以直接用按钮选择屏幕键盘字符。
- 现代 Windows 11 和不同手柄上的兼容性需要单独验证。

### 2.5 Playnite GamepadDesktop 扩展

[GamepadDesktop-PlayniteExtension](https://github.com/roob-p/GamepadDesktop-PlayniteExtension) 为 Playnite 桌面模式增加手柄操作和集成虚拟键盘。

适合：

- 使用 Playnite 管理游戏库。
- 主要需求是搜索游戏、编辑名称和进行少量文字输入。
- 不想离开 Playnite 进入 Windows 桌面。

它不是通用 Windows 输入法；离开 Playnite 后仍需要系统级方案。

### 2.6 Kodi、RetroArch 和其他媒体中心

- [Kodi](https://kodi.wiki/view/Keyboard)：内置屏幕键盘，适合遥控器/手柄，是媒体中心场景最成熟的选择。
- RetroArch：支持和游戏核心/聊天相关的屏幕键盘，适合模拟器场景。
- LaunchBox/BigBox、EmulationStation Desktop Edition 等前端：搜索和设置界面可能带屏幕键盘，具体版本差异较大。
- 这类方案只解决各自应用内的文字输入，不是系统级键盘。

## 3. 外接实体键盘方案

### 3.1 Xbox Chatpad/夹持式小键盘

Xbox 360、Xbox One 时代存在官方或第三方的 Chatpad/夹持键盘，安装在手柄底部，提供实体 QWERTY 按键。

优点：

- 打字速度通常高于所有屏幕键盘。
- 不需要用摇杆逐个选择字符。
- 在很多场景下会被系统识别为标准 HID 键盘。

注意：

- 并非所有型号都能在 Windows 上作为普通键盘使用；可能需要 Xbox 无线接收器或特定驱动。
- 不同控制器代际的扩展接口兼容性不同，购买前必须确认。
- 老款产品可能停产，二手市场质量和价格不稳定。
- 对中文输入仍有帮助，因为实体按键可以直接驱动微软拼音。

### 3.2 蓝牙迷你键盘/手持键盘

- Rii、iPazzPort 等迷你键盘：自带触摸板或摇杆，适合客厅电脑。
- 手机远程键盘：Unified Remote、KDE Connect 等，可以把手机当成电脑键盘。
- 这类方案不是“手柄打字”本身，但如果你已经有手柄，它常比屏幕键盘效率高。

### 3.3 带实体键盘的掌机

GPD Win、AYANEO、部分 Windows 掌机自带实体键盘，或者支持外接键盘。这属于硬件形态变化，不是通用手柄方案，但适合评估“客厅/掌上输入”的最终体验。

## 4. 无障碍专用方案

### 4.1 Dasher

[Dasher](https://dasher.zone/) 是一个基于信息几何和预测的文本输入系统。

- 用户通过指针持续移动来选择字母。
- 字母会随着输入动态放大/移动，常用字符更容易选中。
- 可以配合鼠标、摇杆、触摸板或眼动设备使用。
- 学习曲线较高，但熟练后可以连续输入。
- 对英语效果较好；中文需要依赖输入法或额外文本模型。

适合：有持续指针控制能力、愿意学习新输入方式的用户。

### 4.2 OptiKey

[OptiKey](https://www.optikey.org/) 是开源的屏幕键盘/AAC 工具。

- 支持鼠标、眼动仪、开关和多种辅助输入。
- 可以作为屏幕键盘，也可以配置成短语和语音输出。
- 如果手柄能模拟鼠标或开关，就可以用它打字。

适合：无障碍场景、需要自定义键盘布局和短语输入的用户。

### 4.3 Grid 3

[Grid 3](https://www.thinksmartbox.com/product/grid-3/) 是商业 AAC/辅助沟通软件，支持多种指针、开关和眼动输入方式。

- 功能完整、面向专业辅助沟通。
- 价格和使用门槛高。
- 是否能直接接受特定手柄，需要按其输入设备支持列表确认，通常要先把手柄转成鼠标或开关。

适合：有专业无障碍评估支持的场景。

## 5. 自研输入方案

如果要把“手柄打字”做进 GamepadPilot 或自己的应用，建议把文本输入独立成“输入法模式”，不要只做按键映射。

### 5.1 QWERTY 网格屏幕键盘

最常见、最容易实现，也最容易让用户理解。

基本交互：

- 左摇杆：移动高亮格。
- A：输入当前字符。
- B：Backspace。
- X：Space。
- Y：Shift/Caps。
- LB/RB：切换数字、符号、中文/英文层。
- LT/RT：切换候选词页或快速移动。
- Start：确认/提交。
- Back：关闭键盘或取消。

优化点：

- 词级预测：输入前几个字母后给出候选词。
- 自动大写：句首、换行后自动切换。
- 常用短语：保存“收到”“谢谢”“稍等”等快捷短语。
- 长按重复：方向键长按连续移动。
- 取消/重做：保留输入缓冲，避免误按后丢失整段文字。

### 5.2 中文输入必须处理 IME

中文输入不能只靠一张 A-Z 屏幕键盘。需要决定使用哪种输入法集成方式：

1. 使用系统输入法：
   - 屏幕键盘发送 A-Z、空格、数字和方向键。
   - Windows 微软拼音负责拼音组合和候选词。
   - 优点是开发量小，能复用用户词库和云输入。
   - 缺点是候选窗口位置、焦点、管理员权限和全屏游戏兼容性不可控。

2. 使用触摸键盘：
   - 直接调用 `TabTip.exe` 或 Windows 触摸键盘。
   - 输入法、候选词和手写由系统处理。
   - 再用鼠标控制点击字符，是最省事的通用方案。

3. 自己实现拼音和候选词：
   - 需要拼音词典、语言模型、用户词库、简繁转换和模糊音。
   - 工作量和维护成本远高于按键映射。
   - 除非目标是无 Windows 输入法的嵌入式/游戏主机环境，否则不建议从零实现。

4. 直接发送 Unicode：
   - `SendInput` 的 `KEYEVENTF_UNICODE` 可以绕过键盘布局直接输出字符。
   - 适合确定要输入单个 Unicode 字符的场景。
   - 可能绕过输入法组合，不能替代“拼音候选词选择”。
   - 部分游戏使用 Raw Input 或忽略 Unicode 注入，需要用虚拟键事件。

### 5.3 径向/Daisywheel 输入

思路：

- 摇杆方向选择一个字母分区。
- 再用 A/B/X/Y 或肩键选择具体字符。
- 字符按照出现频率排列，常用字母放在更容易到达的位置。

优点：

- 不需要鼠标在屏幕上大幅移动。
- 很适合只有摇杆和少量按钮的手柄。
- Steam 早期的 Daisywheel 以及一些主机输入法使用了类似思路。

缺点：

- 需要记忆布局或持续查看提示。
- 不适合输入大量数字和符号，通常要和网格键盘配合。

### 5.4 T9/多击输入

- 把 A-Z 分成若干组，按钮按下后循环选择组内字符。
- 例如：A/B/C 一组，按一次、两次、三次选择不同字母。
- 配合词级预测，短文本输入速度可以接受。

适合：按钮数量很少的手柄、遥控器或可访问性输入。

### 5.5 和弦/盲文/摩尔斯输入

- 通过多个按钮的同时组合或多个短按序列表示字符。
- 盲文 6 点输入和摩尔斯电码只需要很少的按钮。
- 熟练用户可以高速输入，但学习成本最高。
- 不建议作为普通用户的默认方案，适合无障碍或专业用户。

### 5.6 手势/滑动输入

- 右摇杆按方向滑动代表字母或字母组。
- 类似手机滑行输入，但缺少触控精度。
- 可以只用于快捷指令、标点和表情，而不是完整字符集。

## 6. 方案对比

| 方案 | 英文速度 | 中文速度 | 学习成本 | 是否要额外硬件 | 适用范围 |
| --- | --- | --- | --- | --- | --- |
| Steam 屏幕键盘 | 中 | 中到低 | 低 | 否 | Steam/Deck |
| Windows 触摸键盘 + 鼠标映射 | 中到低 | 中到高 | 低 | 否 | Windows 桌面 |
| Controller Companion | 中 | 中 | 低 | 否 | Windows 桌面/HTPC |
| JoyXoff | 中 | 中（带 IME 相关功能） | 低 | 否 | Windows |
| Keysticks + 系统 OSK | 中到低 | 中 | 低到中 | 否 | Windows 桌面 |
| Kodi/Playnite 内置键盘 | 中到低 | 视输入法 | 低 | 否 | 特定应用 |
| Chatpad/实体小键盘 | 高 | 高 | 低 | 是 | 通用 HID |
| Dasher | 中到高 | 低到中 | 高 | 否 | 无障碍/指针输入 |
| OptiKey/Grid 3 | 中 | 中 | 中到高 | 可能 | 无障碍/AAC |
| 自研网格 OSK | 可调 | 取决于 IME | 中 | 否 | 产品定制 |
| 语音输入 | 高 | 高 | 低 | 麦克风 | 长句、非静音环境 |

## 7. 推荐的组合

### 组合 A：免费、Windows 通用

```text
Xbox/XInput 手柄
  -> Gopher360 / AntiMicroX / JoyToKey
  -> 左摇杆控制鼠标，A 点击，B 退格，LB/RB 空格/回车
  -> Windows 触摸键盘 + 微软拼音
```

适合中文，成本低，缺点是需要两个工具配合。

### 组合 B：Steam 用户

```text
Steam Big Picture / Steam Deck
  -> Steam 屏幕键盘
  -> Windows 输入法或 Steam 自带输入
  -> Steam Input 绑定桌面模式/游戏内文本输入
```

适合 Steam 游戏和 Deck，缺点是离开 Steam 后的桌面兼容性不稳定。

### 组合 C：客厅/HTPC

```text
Controller Companion 或 Keysticks
  -> 手柄控制桌面
  -> 系统屏幕键盘
  -> 媒体中心内置搜索键盘
```

适合懒人配置，直接购买 Controller Companion 可以少调很多参数。

### 组合 D：最快输入

```text
Xbox 手柄 + Chatpad/夹持键盘
  -> 标准 HID 键盘输入
  -> Windows 微软拼音
```

英文和中文都最快，但硬件兼容性、购买渠道和携带便利性需要提前确认。

### 组合 E：无障碍

```text
手柄模拟鼠标/开关
  -> Dasher、OptiKey 或 Grid 3
  -> 词级预测/短语板/语音输出
```

适合不方便使用实体键盘的用户，需要按个人能力和辅助设备定制。

## 8. 如果做进 GamepadPilot

建议把功能拆成两层：

1. 输入层：继续负责手柄读取、摇杆曲线、按键触发。
2. 文本输入层：新增屏幕键盘、输入模式、候选词和输入法桥接。

最小可行产品：

- 一个 4×6 或 5×6 的 QWERTY 网格键盘。
- 左摇杆移动，A 选择，B 删除，X 空格，Y Shift。
- LB/RB 切换数字/符号/中文层。
- Start 提交，Back 关闭。
- 词级建议和常用短语。
- 调用 Windows 输入法处理中文，不自己实现拼音引擎。

关键实现细节：

- 不要用普通按键轮询直接输出字符；先维护一个编辑缓冲，再按提交或实时写入目标控件。
- 中文优先使用系统 IME/触摸键盘；直接用 `KEYEVENTF_UNICODE` 发送中文会绕过候选词。
- 屏幕键盘需要处理焦点：打开键盘时记录目标窗口，关闭后把焦点恢复。
- 全屏独占游戏通常无法显示桌面键盘；只在窗口化/无边框或游戏自身聊天界面中承诺支持。
- 输入法候选词需要方向键或数字键；屏幕键盘必须能发送这些按键。
- 要给用户一个“紧急退出文本输入模式”的按钮，防止手柄焦点卡在键盘上。

## 9. 关键限制

- 手柄屏幕键盘始终比实体键盘慢；如果追求效率，实体 Chatpad/迷你键盘才是最优解。
- 中文输入的关键不是字母映射，而是输入法候选词和焦点管理。
- Steam 屏幕键盘、Windows 触摸键盘和游戏内输入法的行为差别很大，必须逐场景测试。
- 独占全屏、反作弊、管理员窗口和 Raw Input 程序可能无法使用桌面级屏幕键盘。
- 语音输入是很好的补充，但不适合安静环境、多人房间或需要精确文本的场景。

## 10. 参考入口

- [Steam Big Picture](https://store.steampowered.com/bigpicture/)
- [Steam Input 文档](https://partner.steamgames.com/doc/features/steam_controller)
- [Controller Companion](https://store.steampowered.com/app/367670/Controller_Companion/)
- [JoyXoff GitHub 安装包镜像](https://github.com/oldtools/JoyXoff)（不是源码仓库）
- [JoyXoff 2022 官网存档](https://web.archive.org/web/2022/https://joyxoff.com/)
- [Keysticks](https://keysticks.net/)
- [Playnite GamepadDesktop 扩展](https://github.com/roob-p/GamepadDesktop-PlayniteExtension)
- [Kodi 键盘说明](https://kodi.wiki/view/Keyboard)
- [Dasher](https://dasher.zone/)
- [OptiKey](https://www.optikey.org/)
- [Grid 3](https://www.thinksmartbox.com/product/grid-3/)
- [AntiMicroX](https://github.com/AntiMicroX/antimicrox)
- [Gopher360](https://github.com/Gopher360/Gopher360)
- [JoyToKey](https://joytokey.net/en/)
- [AutoHotkey](https://www.autohotkey.com/)

## 11. 一句话选型

- Steam 场景：先用 Steam 屏幕键盘。
- Windows 桌面中文输入：Windows 触摸键盘 + 手柄转鼠标 + 微软拼音。
- 想省事：Controller Companion。
- 想试自带虚拟键盘的旧款免费工具：JoyXoff，但要注意闭源、更新停在 2022 年左右以及 Windows 11 兼容性。
- 想最快：Xbox Chatpad 或蓝牙迷你键盘。
- 无障碍：Dasher/OptiKey/Grid 3。
- 自己开发：QWERTY 网格键盘 + 系统 IME，不要从零实现拼音。

