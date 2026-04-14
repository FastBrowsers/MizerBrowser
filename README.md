[README.md](https://github.com/user-attachments/files/26711440/README.md)
# MizeR Browser 🚀

**MizeR** is a lightweight, modern web browser built with **C#** and **WinForms**, utilizing the powerful **WebView2** engine (Chromium). It features a completely custom, borderless user interface and a flexible theme system.

---

## ✨ Features

* **Custom UI/UX:** A sleek, frameless window design with custom-built controls and navigation.
* **Tab Management:** Smooth multi-tab support for efficient browsing.
* **Theme Engine:** * Support for **Dark** and **Light** modes.
    * Ability to set **custom backgrounds** from your PC.
    * Import/Export custom themes via `.mizer` files.
* **Localization:** Only English (maybe ill add Russian later)
* **Personalization:** Quick access shortcuts on the home screen and integrated browsing history.

---

## 🛠 Tech Stack

* **Language:** C#
* **Framework:** .NET Framework 4.7.2 (or .NET 6/8+)
* **Engine:** Microsoft.Web.WebView2 (Chromium-based)
* **Styling:** Custom GDI+ Rendering and WinForms

---

## 🚀 Getting Started

### Prerequisites
1.  **Visual Studio 2022** (with .NET desktop development workload).
2.  **WebView2 Runtime** installed on your system.

### Installation
1.  Clone the repository:
    ```bash
    git clone [https://github.com/YourUsername/MizerBrowser.git](https://github.com/YourUsername/MizerBrowser.git)
    ```
2.  Open `MizerBrowser.sln` in Visual Studio.
3.  Restore NuGet packages (especially `Microsoft.Web.WebView2`).
4.  Press **F5** to build and run the project.

---

## 📂 Project Structure

* `Form1.cs` - The main logic and custom UI rendering.
* `Program.cs` - Application entry point and process initialization.
* `MizerData/` - Directory for storing user session, history, and shortcuts.
* `Resources/` - Custom icons and graphical assets.

---

## 📜 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

---

## 🙌 Credits
Developed by **[FastBrowsers]**. 

Feel free to fork this project, report issues, or submit pull requests!
