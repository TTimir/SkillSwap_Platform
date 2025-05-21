# Use the official ASP.NET Framework 4.8 image
FROM mcr.microsoft.com/dotnet/framework/aspnet:4.8-windowsservercore-ltsc2019

# Set working directory
WORKDIR /inetpub/wwwroot

# Copy published output into the container
COPY bin/Release/PublishOutput/ .
