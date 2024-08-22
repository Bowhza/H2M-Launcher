# H2M Launcher

**The H2M in-game server browser has a few bugs where it does not display the whole server list or does not join the correct server. The launcher aims to address these issues and provide more features.**

## Features

- Launch H2M-Mod.
- Displays the server count and the total players.
- Filter and sort servers by **name, map, mode, player count, and ping**.
- Utilizes the **[IW4MAdmin API](https://master.iw4.zip/instance/)** to get accurate and up to date server information.
- Option to store the servers to a favourites.json file inside the /players2 folder. Saves the whole server list or filtered if one is applied.
- Automatically sets H2M as foreground window and copies the connect string to clipboard on server join.
- Notify users when a new launcher version is available for download (click on the text to download new version from release page).
- Filters out dead/zombie servers that do not respond to UDP packets being sent.

<img src="./Images/H2MLauncher.png">

## Instructions

1. Download the latest release from **[HERE](https://github.com/Bowhza/H2M-Launcher/releases).**

2. Paste the `H2M-Launcher.exe` inside the root of the game directory and run it.

<img src="./Images/Directory.png">

4. Before using the server browser make sure H2M is running. You can press `Launch H2M` to run the game.

5. Find a server you want to play on, select it, and press the join button. The H2M window should automatically be set as the foreground window.

## Shortcuts

| Keyboard/Mouse | Description                             |
| :------------- | :-------------------------------------- |
| `TAB`          | Navigate between launcher controls.     |
| `F5`           | Refresh the server list.                |
| `ENTER`        | Join the selected server.               |
| `Right Click`  | Copies the server to clipboard.         |
| `CTRL + S`     | Save the server list to favourties.json |

## FAQ

This section will try to address most common issues people may encouter while using the browser. **The FAQ will be regurarly updated** while receiving feedback. **_Please read through this section before creating an issue_**.

### Log Files

The log files can be found in the following directory: `%localappdata%\BetterH2MLauncher`

Fastest way to access the directory is by pressing `Win + R` to open the run menu and paste the directory in the `Open` text box.

### Cannot connect to server or launcher does nothing when pressing join.

**Answer**: Either the server is not running or you need to change one or more windows settings.

- If you are on Windows 11, you need to change your default terminal to `Windows Console Host`. This is found under `Settings > System > For Developers`. Make sure to restart your game after changing the terminal.

<img src="./Images/Terminal.png">

- If the above does not apply to you or it still does not send a connect command, you may need to add/change your keyboard layout to `English US`.

### The launcher opens but does not display any servers.

**Answer**: Open the [Server List](https://master.iw4.zip/servers#) or [API Link](https://master.iw4.zip/instance/) in your browser and see if you are able to reach the domain. If not, then check if your ISP is blocking you from accessing the domain.

> **If you encounter any issues not addressed here, please create an issue so it can be resolved and added to the FAQ if needed.**

### Why is a specific server not showing up in the list?

**Answer**: The launcher only displays servers that are actually running. Many servers in the IW4MAdmin-Master Panel are offline and reporting false stats.

- Make sure that the server reports an IPv4 Address to the panel, as IPv6 servers are not supported and will not show up.

> **If you and others can connect to the server in game, but it does not show up, please report the concrete case!**

## Compiling from Source Code

1. If you wish to compile the code from source, clone the repository and open the `H2MLauncher.sln` solution using Visual Studio or JetBrains Rider.

2. Open the terminal inside the `H2MLauncher.UI` project directory and run the following command to create a standalone executable:

```powershell
dotnet publish -r win-x64 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained true
```

3. The executable should now be found under `bin/Release/net8.0/win-x64/publish` and you can copy it into your game directory from there.
