## 晾衣绳 Hangline 0.1.2

修一处移植时漏掉的行为：**外接屏。** 功能没有增减，其余和 0.1.1 一致。已经在用 0.1.1
的话，换掉 exe 即可，设置和挂在绳上的图都不会丢（`%APPDATA%\Hangline` 不动）。

### 修了什么

**绳子现在会跟着指针换屏，外接屏顶边也能放绳。**

之前的绳子只会停在启动时那一块屏上。指针挪到外接屏顶边，绳子不下来——判定写的是
"光标在**这块屏**顶部 6 个像素内"，而光标在外接屏上时永远不成立。截图也一样：在外接屏上
截的图，绳子出现在主屏顶上。

上游原版是每个 tick 都重算指针下的屏幕、换了就搬家（`AppDelegate.swift:481-500`），
源码注释里专门点明另一个场景：*"it also works when another display sits above and the
pointer never stops"*——**另一块屏摆在上方时，指针从主屏顶上划过根本不会停留**，所以
每帧换屏是必须的，不是优化。这里漏了整条规则，现已照搬。

现在：

- 收起状态跟随光标换屏，鼠标停在哪块屏顶边就放哪块屏的绳；
- 绳子**放着的时候不跟**：正看着照片时把它瞬移到另一块屏，卡片会从指针底下被拽走；
- 截图按来源屏落绳，图从选区的实际位置飞进那张绳。

### 下载哪个

| 文件 | 体积 | 适合 |
|:--|:--|:--|
| `Hangline-0.1.2-win-x64-portable.zip` | 76.4 MB | **大多数人**。解压后双击 `Hangline.exe` |
| `Hangline-0.1.2-win-x64-singlefile.exe` | 221.2 MB | 只传一个文件的场合 |

两者都是自包含的：目标机器**不需要安装 .NET 运行库**，不需要管理员权限，Windows 10 /
11 均可。**解压后请从 `Hangline.exe` 启动**，不要用 `dotnet Hangline.dll`：进程的 DPI
感知度取自 exe 的 manifest，走 dotnet 宿主时多显示器缩放下的几何尺寸会算错。

体积在下载之前看不到，所以写在这里：便携目录解压后约 200 MB，单文件 221 MB。**单文件更大
也更慢**——WPF 的原生 dll 无法打包进单文件，每次启动都要先解包到 `%TEMP%\.net`。
实测冷启动：便携目录 4.3 s，单文件 4.9 s。所以默认推荐便携目录，单文件只给"只能传一个文件"
的场合。

### 单一显示器用户是否要升级

不用急。这个版本只改多屏行为，单屏下表现与 0.1.1 完全相同（单屏本就接一块屏，无从跟起）。

### 完整说明

使用见 [docs/guide.md](https://github.com/zhaokangwei095/Hangline/blob/master/docs/guide.md)，
与 Mac 原版的逐条差异见
[docs/parity.md](https://github.com/zhaokangwei095/Hangline/blob/master/docs/parity.md)。

### 自己编译

```powershell
scripts/install-sdk.ps1                        # .NET 8 SDK 装到用户目录，无需管理员
scripts/dev.ps1                                # 编译并启动
scripts/publish.ps1                            # 产出 publish\Hangline\ 便携目录
scripts/publish.ps1 -SingleFile                # 单文件变体
scripts/pack-release.ps1                       # 打出上面这两个发布产物
dotnet test                                    # 123 个单元测试
```

MIT License。Copyright (c) 2026 Alejandro Buján（原版），本移植版同样以 MIT 发布。
