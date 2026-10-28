# CodexQuotaPet · v1.0.0

Windows 上的 Codex 额度桌宠。点击角色查看 5 小时、1 周剩余用量与北京时间重置时间。

本项目由使用者提出需求，并借助 OpenAI Codex 实现；是个人非商业发布的非官方工具，不代表 OpenAI 或 TYPE-MOON。

![桌宠角色](assets/character.png)

## 功能

- 本地 Codex 插件加载时自动启动；已运行则复用单实例。
- 点击展开额度卡片，支持刷新、拖动和恢复位置。
- 右键放大、缩小和恢复默认大小；范围 50%～200%，每次 10%，自动保存。
- 跟随 Codex 窗口，在切换应用或最小化时隐藏。
- Windows 桌面代理启动，插件连接断开不会连带关闭桌宠。

## 使用

Windows 10/11 x64、已登录的 Codex 桌面端。独立桌宠可以直接运行 Windows 下载包中的 `app/CodexQuotaPet.exe`。

自动启动另需可用的 Python 3 和 PATH 中的 Codex CLI。把下载包解压到固定目录后，在 PowerShell 中运行：

```powershell
./scripts/install.ps1
```

如果系统策略阻止脚本，可以在确认脚本内容后，对这一次运行使用：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/install.ps1
```

安装后完整退出并重开 Codex 一次，再加载本地聊天。只停留在首页不保证启动。不添加 Windows 登录启动项，不修改 Codex 客户端。Python、Codex 与插件缓存位置由安装器在目标电脑生成，不包含开发者电脑的路径。

## 从源码构建

安装支持 .NET 8 的 SDK，然后运行：

```powershell
./scripts/build.ps1
```

结果位于 `dist/app/`。随后可运行安装脚本，或直接启动 `dist/app/CodexQuotaPet.exe`。

## 文件分类

| 目录 | 内容 |
|---|---|
| `source/` | WPF 桌宠、额度读取和窗口跟随源码 |
| `plugin/` | 本地启动桥接与插件声明模板 |
| `scripts/` | 构建及安装脚本 |
| `assets/` | 当前角色 PNG 与生成说明 |
| `vendor/` | CodexQuotaTitle.Core 源码和原许可证 |
| `tests/` | 额度模型、启动桥接、WPF 缩放及在线额度测试 |
| `docs/` | 隐私说明、使用说明、GitHub 上传步骤 |

## 额度与隐私

通过本机 `codex app-server` 的只读额度接口查询当前账号；不读取聊天记录。启动桥接没有网络请求。不向插件作者的服务器上传数据。Codex 自身仍会连接 OpenAI 获取额度，因此不是完全离线工具。详见 [隐私说明](docs/PRIVACY.md)。

## 素材、许可证与限制

当前图片为参考网图生成的琥珀 Q 版同人形象；原参考画师及授权尚未核实。非商业发布不表示取得角色或画作授权。详见 [素材与第三方声明](THIRD_PARTY_NOTICES.md)。代码许可状态见 [LICENSE.md](LICENSE.md)。

受 Codex 插件接口、登录状态和额度格式变化影响，数据可能暂时不可用或需要后续适配。旧数据会标记为过期。当前为静态角色附轻微呼吸效果，不包含完整动作库。

## 测试

```powershell
dotnet run --project tests/QuotaPet.Tests.csproj
python tests/test_bridge.py
dotnet run --project tests/Scale/Scale.Tests.csproj
```

缩放测试会暂时写入尺寸设置并在结束时恢复。在线额度测试需登录，并会联系 OpenAI：`dotnet run --project tests/Live/QuotaPet.Live.csproj`。

卸载自动启动：`codex plugin remove codex-quota-pet@quota-pet-local`。退出桌宠后，独立程序仍可手动运行。
