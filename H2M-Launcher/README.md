# H2M Launcher

**The H2M in-game server browser has a few bugs including not joining the correct server or not showing all of the servers.**

The launcher intends to fix these issues and more; here are the features:

- Launching H2M-Mod.
- Displays the server count and the total players.
- Scrapes all the servers from https://master.iw4.zip/servers and lets the user join them from the launcher instead of the in-game server browser.
- Store the servers to a favourites.json file inside the /players2 folder.

<img src="./Images/H2MLauncher.png">

## Instructions

1. Download the latest release from **[HERE](https://github.com/Bowhza/H2M-Launcher/releases).**

2. Paste the `H2M-Launcher.exe` inside the root of the game directory. It is the same directory where the `h2m-mod.exe` is located.

3. Run the `H2M-Launcher.exe` and the controls will be displayed in the top left corner of the application.

4. Launch H2M-Mod before using the launchers' server browser, you can do so by pressing `L` on your keyboard.

5. Find a server you want to join and simply double click it.

## Compiling from Source

1. If you wish to compile the code from source, clone the repository and open the solution using Visual Studio or JetBrains Rider.

2. Press `Ctrl + B` to build the solution.

3. Open the terminal inside the project directory and run the following command to create a standalone executable:

```console
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

4. The executable should now be found under `bin/Release/net8.0/win-x64/publish` and you can copy it into your game directory from there.
