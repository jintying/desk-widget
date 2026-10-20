# 上传到 GitHub

> **当前进度**：本地已完成 90% —— git 已装、仓库已初始化、24 个文件已提交、SSH 远程已配好。
> **只剩两步**：① 在 GitHub 网页建仓库 ② 推送。

---

## 第一步：在 GitHub 创建仓库（1 分钟）

点这个链接（参数已填好）：

**https://github.com/new?name=desk-widget&description=Windows+桌面+Markdown+日程挂件：单文件+150KB，勾选双向同步，胶囊折叠&visibility=public**

要点：
- **Repository name**：`desk-widget`
- **Description**：填上面那段（可自行修改）
- 选 **Public**
- ❌ **不要**勾选 "Add a README file"（否则会冲突）
- 点 **Create repository**

---

## 第二步：推送

```powershell
cd C:\Users\yu\Documents\deepseek-harness\default-workspace\desktop-widget
git push -u origin main
```

你的 SSH 密钥已配好（`~/.ssh/id_ed25519_github`），认证账号是 **jintying**，无需密码。

---

## 第三步：发布第一个版本（触发自动构建）

```powershell
git tag v1.2.0
git push origin v1.2.0
```

推送 tag 后，GitHub Actions 自动执行：

1. 用 `tools/make-icon.ps1` 生成图标
2. 用 `build.ps1` 编译 `DeskWidget.exe`
3. 校验产物大小
4. 打包 `DeskWidget-1.2.0.zip`
5. 创建 Release，附上 exe 和 zip

在仓库 **Actions** 页看进度，**Releases** 页下载成品。

---

## 仓库信息

| 项 | 值 |
| :--- | :--- |
| 账号 | [jintying](https://github.com/jintying) |
| 仓库 | `jintying/desk-widget` |
| 远程地址 | `git@github.com:jintying/desk-widget.git` |
| 默认分支 | `main` |
| 已提交 | 24 个文件 |
| 版本 | v1.2.0 |

---

## 已完成的本地工作

- ✅ `git init` + `git branch -M main`
- ✅ 首次提交（24 文件，2876 行）
- ✅ SSH 远程 `origin` 已添加
- ✅ `.gitignore` 排除 exe / 日志 / 本机配置
- ✅ `.gitattributes` 统一换行符
- ✅ 徽章链接已指向 `jintying/desk-widget`
- ✅ `AssemblyInfo` 公司名 `jintying`，版本 1.2.0.0
- ✅ `app.manifest` 标识 `jintying.deskwidget`

---

## 上传前可选改动

| 文件 | 改什么 |
| :--- | :--- |
| `README.md` | 补充特色说明、使用场景 |
| `screenshots/` | 补 `pill.png`（胶囊）、`settings.png`（设置页） |
| `CHANGELOG.md` | 日期核对 |
| `app.manifest` | 如需更高 DPI 策略可调 |

---

## 补两张截图的方法

```powershell
# 用系统截图：Win + Shift + S，框选后保存
# 胶囊：启动挂件 → 点 ✕ → 框选蓝色小方块 → screenshots/pill.png
# 设置页：点 ⚙ → 框选设置窗口 → screenshots/settings.png
```

README 里已预留这两个文件名，补完自动生效。

---

## 常见问题

**Q：push 提示 Permission denied (publickey)？**
SSH 密钥没被 GitHub 认可。检查 `ssh -T git@github.com` 应返回 `Hi jintying!`。

**Q：想改用 HTTPS 推送？**
```powershell
git remote set-url origin https://github.com/jintying/desk-widget.git
```
需要 Personal Access Token 当密码。

**Q：CI 构建失败？**
看 Actions 日志。`build.ps1` 依赖 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，windows-latest 上默认存在。

**Q：不想提交 tag 但想下载 exe？**
Actions 页手动 Run workflow，产物在 Artifact 里，保留 90 天。