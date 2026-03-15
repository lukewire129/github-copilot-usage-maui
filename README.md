# GitHub Copilot Usage MAUI

[🇰🇷 한국어로 보기 (Read in Korean)](README.ko.md)

A cross-platform application built with .NET MAUI and MauiReactor to monitor and analyze your GitHub Copilot usage. It provides a dashboard for daily Copilot usage, model-specific statistics, remaining quota, and more.

## ⚠️ Prerequisites

This application requires the **GitHub CLI (`gh`) to be installed** on your system for user authentication and GitHub API integration.

### 1. Install GitHub CLI
Download and install the appropriate version for your operating system from [https://cli.github.com/](https://cli.github.com/).
- On Windows, you can also install it using the package manager in PowerShell:
  ```bash
  winget install --id GitHub.cli
  ```

### 2. Login to GitHub CLI (with Permissions)
After installation, open your terminal (or PowerShell) and **you must login using the command below**.
```bash
gh auth login -h github.com -s user -w
```
> 💡 **Important:** To access the Copilot Billing API used by this app, the `user` scope permission is strictly required in addition to the default permissions. Logging in with the `-s user` option ensures you can retrieve the correct information. The app also features an in-app permission refresh function (🔑 button).

### 3. Development Environment
- .NET 9.0 SDK or higher
- .NET MAUI workload (`dotnet workload install maui`)

---

## ✨ Key Features

- **Real-time Dashboard:** Check your total usage (Requests) for this month, remaining quota, and daily usage pace.
- **Goal vs. Trend:** Projects month-end usage based on current pace and estimates exactly when the quota will run out.
- **Model Usage Breakdown:** Analyzes the ratio (%) of models used in the backend, such as GPT-4 and GPT-3.5.
- **In-App Auth Management:** Supports the `gh auth refresh` workflow directly via an in-app panel when your token or permissions expire.

## 🚀 Build and Run

> 🔔 This project is currently developed and verified **exclusively for Windows**. Proper functionality on other platforms (Mac Catalyst, iOS, Android) is not guaranteed.

You can build and run the app from the project directory using the following commands:

### Windows
```bash
dotnet build
dotnet run -f net9.0-windows10.0.19041.0
```
