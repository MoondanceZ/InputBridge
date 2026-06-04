# InputBridge

这是参考 `Wuming155/InputSyncHelper` 改写的 WinForms 版本。手机不需要安装 App，只要和电脑处于同一 Wi-Fi，用浏览器打开主界面显示的地址或扫码即可把手机输入同步到电脑当前光标位置。

## 功能

- WinForms 主界面显示访问地址和二维码
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

首次使用时如果手机无法访问，请检查：

- 手机和电脑是否在同一 Wi-Fi
- Windows 防火墙是否允许该程序监听端口
- 默认端口 `5505` 是否被其他程序占用

配置文件保存在：

```text
%APPDATA%\InputBridge\settings.json
```
<img width="362" height="524" alt="QQ20260604-200348" src="https://github.com/user-attachments/assets/aca11971-96a6-436a-a8fa-88e0dec7ee5d" />
<br>
<br>
<img width="362" height="664" alt="QQ20260604-200732" src="https://github.com/user-attachments/assets/0132300b-0b01-4e10-a830-dbf2f628beb9" />


