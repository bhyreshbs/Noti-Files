# 🚀 Noti-Files

**Noti-Files** is a native Windows desktop utility that monitors file system activity in real time and displays smart, interactive notifications directly on the desktop.

It is designed to be lightweight, fast, and fully offline — no accounts, no cloud, just install and use.

---

## 🧠 Overview

NotiFiles runs in the background and tracks file activities such as saving, moving, renaming, and downloading. Whenever a meaningful file event occurs, it shows a clean floating notification with quick actions.

The goal is to provide instant awareness and control over file operations without interrupting your workflow.

---

## ✨ Features

* 📂 **Real-Time File Monitoring**

  * Detect file creation (save)
  * Detect file movement and renaming
  * Detect downloads (via monitored folders)

* 🧠 **Smart Filtering**

  * Ignores temporary/system files (`.tmp`, hidden files, etc.)
  * Focuses only on useful files (PDFs, images, docs, videos)

* 🔔 **Smart Notifications**

  * Floating toast UI on desktop (bottom-left)
  * Non-intrusive and minimal design
  * Auto-dismiss with hover pause

* ⚡ **Quick Actions**

  * Open File
  * Open File Location
  * Dismiss notification

* 🖥️ **Background Execution**

  * Runs silently as a system tray application
  * No main window required

---

## 🎨 UI Design

* 🧊 Glassmorphism (Acrylic / Fluent Design)
* 🌞 Light and 🌙 Dark themes
* Smooth animations (slide + fade)
* Stacked notifications for multiple events

---

## 🏗️ Architecture

```
FileSystemWatcher → Event Processor → Filter Engine → Notification Manager → UI Renderer
```

---

## 💻 Tech Stack

* **Language:** C#
* **Framework:** WPF (initial), WinUI (future)
* **File Monitoring:** FileSystemWatcher (.NET)

---

## 🗄️ Storage

* ❌ No database required
* 📁 Local storage only (JSON for settings if needed)

---

## 🔐 Authentication

* ❌ No login required
* ❌ No account system

> Future versions may include licensing or activation features.

---

## 🚀 Philosophy

* ⚡ Lightweight and fast
* 🔌 Fully offline
* 🖥️ Native Windows experience
* 🎯 Minimal and distraction-free

---

## 📌 Status

🚧 Currently in development (MVP phase)

---

## 💡 Future Scope

* Activity history dashboard
* Advanced filtering options
* AI-based file suggestions
* Licensing / activation system

---

## 📄 License

This project is currently open for development. License will be added in future.

---

## 🤝 Contributing

Contributions, ideas, and feedback are welcome!

---

## ⭐ Support

If you like this idea, consider giving it a ⭐ on GitHub!
