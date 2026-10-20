# 上传到 GitHub 操作手册

## 一、前置：安装 Git

你这台机器**目前没有 git**（之前的 winget / curl 都被网络拦截）。
请在你的终端里任选一种方式装：

**方式 1（推荐，最简单）**
```powershell
winget install --id Git.Git -e
```

**方式 2（官网下载）**
打开 https://git-scm.com/download/win 下载 64 位安装包，一路下一步。

装完**新开一个终端**，验证：
```powershell
git --version
```

---

## 二、创建仓库并首次提交

```powershell
cd C:\Users\yu\Documents\deepseek-harness\default-workspace\desktop-widget

git init
git add .
git commit -m "feat: 日程挂件 v1.1.0 初始提交"
```

> `.gitignore` 已经排除了 `*.exe`、`widget.log`、`widget-config.json`（含本机绝对路径）、`dist/`。
> 如果你想连 exe 一起提交，把 `.gitignore` 里的 `*.exe` 删掉即可（不推荐，CI 会构建）。

---

## 三、创建 GitHub 仓库并推送

1. 打开 https://github.com/new
2. Repository name：`desk-widget`
3. Description：`Windows 桌面 Markdown 日程挂件：单文件 150 KB，勾选双向同步，胶囊折叠`
4. 选 **Public**，**不要**勾选 "Add a README file"
5. Create repository

然后推送：

```powershell
git branch -M main
git remote add origin git@github.com:jintying/desk-widget.git
git push -u origin main
```

---

## 四、发布第一个版本（触发自动构建）

```powershell
git tag v1.1.0
git push origin v1.1.0
```

推送 tag 后，GitHub Actions 会自动：
1. 生成图标
2. 编译 `DeskWidget.exe`
3. 打包 zip
4. 创建 Release 并附上 exe 和 zip

在仓库的 **Actions** 页能看到构建进度，**Releases** 页能看到成品。

---

## 五、上传前建议改的地方

| 文件 | 改什么 |
| :--- | :--- |
| `README.md` | 顶部仓库地址、Demo 截图（已有 `screenshots/widget.png`） |
| `CHANGELOG.md` | 版本日期按实际改 |
| `src/AssemblyInfo.cs` | `AssemblyCompany` 改成你的名字/ID |
| `app.manifest` | `assemblyIdentity name` 可改成你的命名空间 |

---

## 六、常见问题

**Q：推送要密码？**
GitHub 已不支持密码，用 Personal Access Token：
Settings → Developer settings → Personal access tokens → Tokens (classic) → Generate new token，
勾选 `repo` 权限，用它当密码。

**Q：CI 编译失败？**
检查 `build.ps1` 里的 csc 路径在 windows-latest 上是否存在（默认存在）。

**Q：想发布打好的 exe 但不想追踪 tag？**
在 Actions 页手动 Run workflow，产物会出现在 Artifact 里。