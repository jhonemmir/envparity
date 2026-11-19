# Terminal demo for the README.
#
# Regenerate from the repository root with:
#   dotnet build -c Release
#   vhs docs/demo.tape
#
# The recording uses the locally built tool (Release) through a symlink, so it
# works without installing the NuGet package. Setup runs in a hidden section and
# clears the screen before the visible part starts.

Output docs/demo.gif

Set Shell "bash"
Set FontFamily "DejaVu Sans Mono"
Set FontSize 15
Set Width 1000
Set Height 620
Set Padding 24
Set MarginFill "#1e1e2e"
Set Theme "Catppuccin Mocha"

Hide
Set TypingSpeed 0ms
Type "unset NO_COLOR; export DOTNET_ROOT=$HOME/.dotnet; export PS1='$ '"
Enter
Type "mkdir -p /tmp/envparity-demo-bin && ln -sf $PWD/src/EnvParity/bin/Release/net10.0/EnvParity /tmp/envparity-demo-bin/envparity"
Enter
Type "export PATH=/tmp/envparity-demo-bin:$PATH"
Enter
Type "cd $(mktemp -d)"
Enter
Type "printf 'APP_NAME=envparity\nDATABASE_URL=\nDEBUG=true\n' > .env"
Enter
Type "printf 'APP_NAME=\nDATABASE_URL=postgres://localhost/app\nAPI_KEY=\nSECRET_KEY=\n' > .env.example"
Enter
Type "printf 'APP_NAME=envparity\nDATABASE_URL=postgres://localhost/app\nAPI_KEY=your-api-key\nSECRET_KEY=your-secret-key\n' > .env.fixed"
Enter
Type "clear"
Enter
Sleep 300ms
Show

Set TypingSpeed 60ms
Type "envparity"
Enter
Sleep 2.5s

Type "envparity --strict"
Enter
Sleep 2.5s

Type "cp .env.fixed .env"
Enter
Sleep 500ms

Type "envparity"
Enter
Sleep 3s
