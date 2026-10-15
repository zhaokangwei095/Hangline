## 晾衣绳 Hangline 0.1.0

截一张图，它就挂到屏幕顶上的一根绳子上。指针停在屏幕最上边缘，绳子带着照片滑
下来；移开 0.5 秒，它自己收回去。单击复制，长按标注，拖出去移动或删除。

移植自 macOS 应用 [Tendedero](https://github.com/alejandrobujan/tendedero)
（Alejandro Buján，MIT 协议）。原项目的 MIT 授权不包含名称与衬衫图标，因此这里是
另一套名字，托盘图标由代码绘制（`Shell/IconGen.cs`）。

### 下载哪个

| 文件 | 体积 | 适合 |
|:--|:--|:--|
| `Hangline-0.1.0-win-x64-portable.zip` | 76.4 MB | **大多数人**。解压后双击 `Hangline.exe`（Assets 里标注为 Portable folder） |
| `Hangline-0.1.0-win-x64-singlefile.exe` | 221.1 MB | 只传一个文件的场合（Assets 里标注为 Single file） |

两者都是自包含的：目标机器**不需要安装 .NET 运行库**，不需要管理员权限，Windows 10 /
11 均可。整个文件夹可以拷到 U 盘随便搬。

单文件版**更大也更慢**（221 MB vs 解压后 200 MB）：WPF 的原生 dll 无法打包进单文件，
每次启动都要先解包到 `%TEMP%\.net`。实测冷启动：便携目录 4.3 s，单文件 4.9 s。

**解压后请从 `Hangline.exe` 启动**，不要用 `dotnet Hangline.dll`：进程的 DPI 感知度取自
exe 的 manifest，走 dotnet 宿主时多显示器缩放下的几何尺寸会算错。

### 第一次启动

双击后没有窗口弹出——这是对的。它常驻在**右下角托盘**里，图标是一枚灰色衣夹。
建议第一件事：右键托盘图标 → 勾上「登录时启动」。

快捷键（默认，均可在设置里改）：

| | |
|:--|:--|
| `Ctrl+Alt+T` | 放下 / 收起绳子 |
| `Ctrl+Alt+Shift+A` | 截一块区域 |
| `Ctrl+Alt+Shift+F` | 截全屏 |
| `Ctrl+Alt+Shift+W` | 截当前窗口 |
| `PrtSc` | 截一块区域（接管系统截图键，可在设置里关掉） |

完整说明见 [docs/guide.md](https://github.com/zhaokangwei095/Hangline/blob/master/docs/guide.md)。

### 已知与 Mac 原版不一样的地方

详见 [docs/parity.md](https://github.com/zhaokangwei095/Hangline/blob/master/docs/parity.md)。
三处降级，如实记录：

- 玻璃材质没有真背景模糊（Windows 无受支持的跨进程模糊 API）
- 无法接管系统截图设置，改为自建截图 + 目录监听
- 录屏剪辑依赖机器上已有的 ffmpeg（.NET 8 够不到 WinRT 的 `VideoEditingService`）

两处比原版更好：剪贴板监听是即时的（Windows 有 `WM_CLIPBOARDUPDATE`，macOS 只能
0.5 秒轮询）；重启后上次挂着的图会原样回来。

### 自己编译

```powershell
scripts/install-sdk.ps1                        # .NET 8 SDK 装到用户目录，无需管理员
scripts/dev.ps1                                # 编译并启动
scripts/publish.ps1                            # 产出 publish\Hangline\ 便携目录
scripts/publish.ps1 -SingleFile                # 单文件变体
scripts/pack-release.ps1                       # 打出上面这两个发布产物
dotnet test                                    # 114 个单元测试
```

### 校验和

发布产物的 SHA256 见本 Release 的资产列表下方，或下载后自行核对。

MIT License。Copyright (c) 2026 Alejandro Buján（原版），本移植版同样以 MIT 发布。
