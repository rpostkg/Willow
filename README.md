# Willow



&#x20; To build the MSI:



&#x20; # 1. Publish the app (required first time or after code changes)

&#x20; dotnet publish Willow\\Willow.csproj -c Release -p:Platform=x64



&#x20; # 2. Build the installer

&#x20; dotnet build Installer\\Willow.Installer.wixproj -c Release

