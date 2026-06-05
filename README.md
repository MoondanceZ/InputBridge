# InputBridge

这是参考 `Wuming155/InputSyncHelper` 改写的桌面端输入同步工具。手机不需要安装 App，只要和电脑处于同一 Wi-Fi，用浏览器打开主界面显示的地址或扫码即可把手机输入同步到电脑当前光标位置。

## 功能

- Avalonia 桌面主界面显示访问地址和二维码
- 手机浏览器输入框实时同步到电脑
- WebSocket 增量同步，支持手机端删除时同步退格
- 退格次数限制，避免误删过多电脑端内容
- 智能感知：电脑端手动输入或点击后，手机端自动锁定新输入段落
- 自动清空开关和清空时间设置
- 最小化到系统托盘

## 运行

```powershell
dotnet run
```

## Native AOT 发布

项目目标框架为 `.NET 10`，默认配置为 `win-x64` 自包含 Native AOT 发布。

```powershell
dotnet publish -c Release
```

发布产物位于：

```text
bin\Release\net10.0-windows\win-x64\publish
```

## MSI 安装包

项目使用 WiX Toolset 生成 MSI 安装包。推荐使用 WiX `5.0.2`，因为 WiX 7 构建时需要额外接受 OSMF/EULA。

手动安装 WiX：

```powershell
dotnet tool install --global wix --version 5.0.2
```

如果已经安装过 WiX 7，可以先卸载再安装 WiX 5：

```powershell
dotnet tool uninstall --global wix
dotnet tool install --global wix --version 5.0.2
```

一键生成 AOT 发布产物和 MSI：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1
```

生成的 MSI 位于：

```text
artifacts\InputBridge-<版本号>-x64.msi
```

MSI 安装向导支持选择安装目录。安装时会把目录写入注册表，后续升级或重新安装会默认沿用上次选择的路径。

如果已经手动执行过 `dotnet publish -c Release`，只想重新打包 MSI，可以跳过发布：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1 -SkipPublish
```

首次使用时如果手机无法访问，请检查：

- 手机和电脑是否在同一 Wi-Fi
- Windows 防火墙是否允许该程序监听端口
- 默认端口 `5505` 是否被其他程序占用

配置文件保存在：

```text
%APPDATA%\InputBridge\settings.json
```

<img width="488" height="650" alt="image" src="https://github.com/user-attachments/assets/9cc5e0c1-9f4f-4ac1-8282-79960d12d662" />
<br>
<br>
<img width="488" height="1114" alt="732efad8754d0d9af6d0db7a9c949b6" src="https://github.com/user-attachments/assets/a6523cf7-3e0d-4786-afdc-9c753703dbf6" />



