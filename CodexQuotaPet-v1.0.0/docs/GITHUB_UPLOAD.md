# 上传 GitHub

1. 解压 `CodexQuotaPet-v1.0.0-source.zip`，使用其中的 `CodexQuotaPet-v1.0.0` 文件夹作为仓库根目录。不要上传 ZIP 本身作为源码，也不要选择开发工作区或旧版目录。
2. 使用 GitHub Desktop 创建新仓库，或在该目录初始化 Git。提交前检查 README、代码许可状态及 THIRD_PARTY_NOTICES。当前新增代码未选定通用开源许可证；若希望正式开源，需要由发布者选择许可证，第三方代码与素材分别处理。
3. 提交文件。已提供 `.gitignore` 排除编译产物、日志、Python 缓存和机器专用安装配置。
4. 点击 Publish repository，并选择公开或私有；公开后任何人都可能下载源码与当前素材。本项目未替你执行该操作。
5. 在 GitHub 仓库的 Releases 页面创建版本，Tag 填写 `v1.0.0`，标题填写 `CodexQuotaPet v1.0.0`。
6. 把 `CodexQuotaPet-v1.0.0-windows-x64.zip` 和 `SHA256SUMS-v1.0.0.txt` 添加为 Release 附件。Windows 可执行文件不放进源码提交。

项目根目录应直接包含 README.md、VERSION、source、plugin、assets、scripts、tests、vendor 和 docs。发行版本统一为 v1.0.0；vendor 仍保留上游真实版本。

如果修改源码后重新打包，在源码目录运行 `scripts/build.ps1`，再运行 `python scripts/package.py`。打包器会生成干净源码 ZIP 与 Windows 下载 ZIP，并生成校验值；构建产物和目标电脑路径不会加入源码包。
