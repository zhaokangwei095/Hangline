# Hangline 晾衣绳

把每张截图挂到屏幕顶上的一根绳子上。

移植自 macOS 应用 [Tendedero](https://github.com/alejandrobujan/tendedero)（MIT，
但名称与图标不在授权范围内，所以这里是另一套名字和一枚代码绘制的衣夹）。
Windows 版，免安装：整个文件夹拷到任何一台 Windows 10 / 11 上，双击
`Hangline.exe` 就能用，不需要装 .NET 运行库。

完整说明见 **[docs/guide.md](docs/guide.md)**（第一次启动、每个手势、托盘每一项
设置、文件存放位置、常见问题、卸载）。下面是最常用的一张表。

## 怎么用

截一张图，它就挂上绳了。

| | |
|:--|:--|
| 指针停在屏幕最上边缘 | 绳子带着照片滑下来；移开 0.5 秒后收起 |
| 单击一张照片 | 复制（图片 + 文件两种形式，粘到微信是图、粘到资源管理器是文件） |
| 双击 | 用系统默认程序打开 |
| 长按 0.45 秒 | 标记：矩形 / 箭头 / 画笔 / 文字 / 马赛克，`Enter` 保存后卡片就地换图 |
| 中键点击 | 快速查看（不打开任何程序地看一眼，`←` `→` 在绳上的图之间走） |
| 长按一段录屏 | 剪辑：自动播放，拖两个端点，`Enter` 保存（流拷贝，不重编码） |
| 拖到别的文件夹 | 移动过去，卡片从绳上取下 |
| 拖到回收站 | 删除 |
| 拖到别的程序里 | 送一份副本，原图仍挂在绳上 |
| 点左上角的 ✕ | 自己收件夹里的图 → 回收站；别处的图 → 只是从绳上取下 |
| 滚轮 | 图多了以后横向平移 |
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>T</kbd> | 显示 / 隐藏绳子 |
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>A</kbd> | 截一块区域 |
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>F</kbd> | 截全屏 |
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>W</kbd> | 截当前窗口 |

托盘图标（衣夹）右键是全部设置：尺寸、绳上保留多少张、额外监听哪些文件夹、
什么时候放绳、要不要挂剪贴板里的图、开机自启、音效。

自己截的图存在 `%APPDATA%\Hangline\Screenshots`；同时也会挂上
`图片\Screenshots`、`视频\Captures` 和桌面上的新截图。

## 从源码跑

```powershell
scripts/install-sdk.ps1   # 装 .NET 8 SDK 到用户目录，不需要管理员
scripts/dev.ps1           # 编译并启动
scripts/publish.ps1       # 产出 publish\Hangline\ 便携目录
scripts/publish.ps1 -SingleFile   # 单文件变体，见下表
dotnet test               # 118 个单元测试
```

两种交付形态都实测过（这台机器，冷启动到绳子窗口出现，含 0.5 秒轮询粒度）：

| | 体积 | 文件数 | 启动 |
|:--|:--|:--|:--|
| `publish\Hangline\` 便携目录 | 202 MB | 244 | 4.3 s |
| `publish-singlefile\Hangline.exe` | 220.9 MB | 1 | 4.9 s |

单文件**更大也更慢**：WPF 的原生 dll 无法打包进单文件，每次启动都要先解包到
`%TEMP%\.net`。所以默认交付是便携目录——整坨拷走就行，还少一次解包。单文件留着
是给"只能传一个文件"的场合用的。

必须用 `Hangline.exe` 启动，不要用 `dotnet Hangline.dll`：进程的 DPI 感知度取自
exe 的 manifest，走 dotnet 宿主时监视器尺寸会被系统按缩放比例虚拟化，量出来全是错的。

## 已知与 Mac 版不一样的地方

见 [docs/parity.md](docs/parity.md)。三处降级是：玻璃没有真背景模糊（Windows 无
受支持的跨进程模糊 API）、无法接管系统截图设置（改成自建截图 + 监听）、录屏剪辑
依赖机器上已有的 ffmpeg（系统的 `VideoEditingService` 从 .NET 8 够不到，实测过）。
标注编辑器是重建的：Windows 没有能给出保存回调的系统「标记」，所以自己写了一个。
