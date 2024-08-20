# THIS IS THE LEGACY BRANCH FOR THE H2M SERVER SCRAPER

The program will scrape the server list for H2M from https://master.iw4.zip/servers# and create a favourites.json file that can be used for the server browser in game.

## Instructions

1. Download the latest server scraper release from **[HERE](https://github.com/Bowhza/H2M-ServerScraper/releases/tag/v1.0.2).**

2. Paste the `ServerScraper.exe` inside the root of the game directory. It is the same directory where the `h2m-mod.exe` is located.

3. Run the `ServerScraper.exe` and it will scrape the server list, create a favourites.json and automatically place it inside the `/players2` folder.

4. The game should auto launch after the above is complete. Once the game loads, open the server browser and sort by favourites. The server list should now be populated.

## Compiling from Source

1. If you wish to compile the code from source, clone the repository and open the solution using Visual Studio or JetBrains Rider.

2. Press `Ctrl + B` to build the solution.

3. Open the terminal inside the project directory and run the following command to create a standalone executable:

```console
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
```

4. The executable should now be found under `bin/Release/net8.0/win-x64/publish` and you can copy it into your game directory from there.
