# ServerApp
Running a Uplay related servers/plugins.

## Titles
Per-title resources (`online_config.json`, FC5 REST data, ...) live in [ServerApp-Titles](https://github.com/Playforward/ServerApp-Titles). Clone it next to this repo; the build copies it into `Titles/` in the output. Point elsewhere with `-p:TitlesDir=/path/to/ServerApp-Titles`.

## Running without Playforward

Leave `TitleCatalog.Url` empty in `ServerAppSettings.json`. Each title in
`Configs/Titles.json` then takes whatever protocol facts it leaves out (its
service family, access key, online-config id) from the host's built-in table
(`DefaultTitles.cs`), and the Quazal plugin serves it standalone: open logins
by name, progress on local disk. See `uplay-server-builds/docs/standalone.md`.
