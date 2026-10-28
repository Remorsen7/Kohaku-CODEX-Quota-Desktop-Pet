# 隐私与运行边界

桌宠启动一个专用本地 `codex app-server` 子进程，调用 `account/rateLimits/read` 查询额度，正常退出会清理该子进程。不读取聊天内容、历史或账号密钥文件，不向本项目维护者发送数据。

该 Codex 子进程使用本机已有登录状态，并可能连接 OpenAI；本工具不是离线查询，也不提供对 Codex 自身网络行为的独立审计。

Python 桥接只处理本地 MCP 标准输入输出，启动固定的桌宠执行文件，不接受调用者指定程序或启动参数。通过已运行的 Windows Explorer 桌面代理启动，避免插件断开时关闭桌宠。不创建开机启动项、计划任务或自动重启循环，不修改 Codex 客户端。

桌宠的位置和尺寸保存于 `%LOCALAPPDATA%/CodexQuotaPet/position.json`；不保存额度历史。异常时桌宠可能在程序旁写入 `error.log`，桥接可能向本地标准错误输出启动错误。不要把这些运行文件作为源码上传。

安装脚本读取目标电脑上的 Python、Codex、用户目录与 CODEX_HOME，生成本机 MCP 路径，并通过 Codex CLI 注册本地插件。构建和首次还原依赖可能联系 NuGet；在线额度测试联系 OpenAI。
