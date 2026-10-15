## 晾衣绳 Hangline 0.1.1

0.1.0 之后修掉的一批问题。功能没有增减，都是"看着有、其实没接上"和"接了但不好用"
这一类：装到别人机器上才发现的那种。已经在用 0.1.0 的话，换掉 exe 即可，设置和挂在
绳上的图都不会丢（`%APPDATA%\Hangline` 不动）。

### 修了什么

**微信 / QQ 截的图不再挂上一张透明的空图。** 微信截图放进剪贴板的是 CF_DIB 系列格式，
其中带 alpha 通道的那一份读出来整张是全透明。之前按"能读到就挂"的顺序取，结果挂上去
的是一张看得见边框、点开什么都没有的空图。现在改读 `CF_DIBV5` 并优先按位图头里的
alpha 信息还原，实测 2560×1600 的微信截图内容完整。

**任务栏上那个没有名字的按钮，关掉它不再把剪贴板监听一起带走。** 托盘程序在任务栏上
留一个没有名字的按钮，点它的 ✕ 之后"挂上复制的图片"从此失灵，重启才好。查下来是
`MessageSink` 那个窗口：它带着 `WS_VISIBLE` 又没有 `WS_EX_TOOLWINDOW`，资源管理器
就把它当成一个正常窗口列了出来，而关闭它会连带销毁剪贴板监听和所有已注册热键所依赖
的那个句柄。现在给这个窗口加上 `WS_EX_TOOLWINDOW` 把它从任务栏摘掉，并且在窗口过程里
拒绝 `WM_CLOSE`——宁可忽略一次关闭请求，也不要静默丢掉监听。
（`WS_EX_NOACTIVATE` 试过，加上之后 `WM_HOTKEY` 干脆不来了，所以没有采用。）

**标注的矩形和箭头画不出来。** 画笔与文字是好的，矩形和箭头拖完什么都没有——几何本身
没错，是那一笔没有落到文档上。

**新图挂上后，绳子等你看过再收。** 原版是"挂上后放下 2.5 秒"，照搬到 Windows 上不对：
Windows 的触发带在屏幕最上边缘，而复制图片时指针在浏览器或桌面里，从键盘移到屏幕顶部
早就超过 2.5 秒，抬头看时绳子已经收回去了，观感就是"挂上了但没弹出来"。现在 peek 6 秒，
并且指针没来过之前不计时，另加 4 秒兜底，避免一次没人看的 peek 让绳子一直挂着。

**`PrtSc` 按一次只截一次。** 按住不放会连发，截出好几张几乎一样的图。

**快捷键的失败原因终于说实话。** 注册被别的程序抢先（`win32 1409`）、和自己另一个
功能撞键、以及正常生效，日志里分得清是哪一种；以前三种都只表现为"按了没反应"。

### 下载哪个

| 文件 | 体积 | 适合 |
|:--|:--|:--|
| `Hangline-0.1.1-win-x64-portable.zip` | 76.4 MB | **大多数人**。解压后双击 `Hangline.exe` |
| `Hangline-0.1.1-win-x64-singlefile.exe` | 221.1 MB | 只传一个文件的场合 |

两者都是自包含的：目标机器**不需要安装 .NET 运行库**，不需要管理员权限，Windows 10 /
11 均可。**解压后请从 `Hangline.exe` 启动**，不要用 `dotnet Hangline.dll`：进程的 DPI
感知度取自 exe 的 manifest，走 dotnet 宿主时多显示器缩放下的几何尺寸会算错。

### 第一次启动

双击后没有窗口弹出——这是对的。它常驻在**右下角托盘**里，图标是一枚灰色衣夹。
建议第一件事：右键托盘图标 → 勾上「登录时启动」。

完整说明见 [docs/guide.md](https://github.com/zhaokangwei095/Hangline/blob/master/docs/guide.md)，
与 Mac 原版的逐条差异见
[docs/parity.md](https://github.com/zhaokangwei095/Hangline/blob/master/docs/parity.md)。

### 自己编译

```powershell
scripts/install-sdk.ps1                        # .NET 8 SDK 装到用户目录，无需管理员
scripts/dev.ps1                                # 编译并启动
scripts/publish.ps1                            # 产出 publish\Hangline\ 便携目录
scripts/publish.ps1 -SingleFile                # 单文件变体
scripts/pack-release.ps1                       # 打出上面这两个发布产物
dotnet test                                    # 118 个单元测试
```

MIT License。Copyright (c) 2026 Alejandro Buján（原版），本移植版同样以 MIT 发布。
