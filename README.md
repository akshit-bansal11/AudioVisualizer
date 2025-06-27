# 🎵 Audio Visualizer (WASAPI Loopback with NAudio + WPF)

A real-time audio visualizer using NAudio and .NET WPF that captures system output (Spotify, YouTube, etc.) via **WASAPI Loopback** and displays smooth bar-based frequency bands.

## ✨ Features

- Real-time audio visualization
- Captures output from **any source** (headphones, speakers, etc.)
- Bars scale with frequency: 
  - Center bars show **low frequencies**
  - Left and right bars show **increasing higher frequencies**
- Clean WPF interface with dynamic visuals

---

## 🔧 How to use this?

### 1. 📦 Install .NET SDK

> Download and install the .NET SDK (any recent version):

- [.NET 8 SDK (Recommended)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

After installation, verify with:

```bash
dotnet --version
```

### 2. 📁 Clone this repo
```bash
git clone https://github.com/your-username/AudioVisualizer.git
cd AudioVisualizer
```

### 3. 📚 Install Dependencies
```bash
dotnet add package NAudio
```
You can also manually verify the following line exists in your .csproj:
```xml
<PackageReference Include="NAudio" Version="2.2.1" />
```

### 4. ▶️ Run the app
```bash
dotnet clean
dotnet build
dotnet run
```

### Remarks
- It's a bit slow, buggy and skips frames but it works great nonetheless.
- You may encounter build-time errors in MainWindow.xaml.cs such as:

  `The name 'InitializeComponent' does not exist in the current context`

  `The name 'VisualizerCanvas' does not exist in the current context`

  `Unknown x:Class type 'AudioVisualizer.App'`

  ➡️ These are XAML designer errors and can often be safely ignored — the app usually builds and runs just fine.

  ✅ If the app window launches and shows bars reacting to system sound — it’s working!

### 🧠 Tech Stack
- .NET 8.0
- C#
- XAML
- WPF
- NAudio

### 👨‍💻 Author
#### Akshit Bansal (artistbansal2004@gmail.com)
