
# use the official Microsoft .NET 5 build image
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env

# move to the src target dir
WORKDIR /app/src

# copy the solution and project files
ADD ./src/ChordTest.sln ./ChordTest.sln
ADD ./src/Chord.Lib/Chord.Lib.csproj ./Chord.Lib/Chord.Lib.csproj
ADD ./src/Chord.Config/Chord.Config.csproj ./Chord.Config/Chord.Config.csproj
ADD ./src/Chord.Daemon/Chord.Daemon.csproj ./Chord.Daemon/Chord.Daemon.csproj

# TODO: check out the target distro / runtime, etc. 
#       and add it to the restore / build / test / release tasks

# restore the NuGet packages (for caching)
RUN dotnet restore --runtime linux-x64

# copy the source code
ADD ./src/ ./

# TODO: run the unit tests here ...

# make a release build
RUN dotnet publish  --runtime linux-x64 --configuration Release --output /app/bin/

# define the .NET 5 runtime image
FROM mcr.microsoft.com/dotnet/runtime:5.0
WORKDIR /app/bin
COPY --from=build-env /app/bin .

# launch the daemon test service on container startup
ENTRYPOINT ["dotnet", "Chord.Daemon.dll"]
