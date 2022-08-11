
# use the official Microsoft .NET build image
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env

# move to the src target dir
WORKDIR /app/src

# copy the solution and project files
ADD ./src/ChordLib.sln ./ChordLib.sln
ADD ./src/Chord.Lib/Chord.Lib.csproj ./Chord.Lib/Chord.Lib.csproj
ADD ./src/Chord.Lib.Test/Chord.Lib.Test.csproj ./Chord.Lib.Test/Chord.Lib.Test.csproj
ADD ./src/Chord.Config/Chord.Config.csproj ./Chord.Config/Chord.Config.csproj
ADD ./src/Chord.Api/Chord.Api.csproj ./Chord.Api/Chord.Api.csproj

# restore the NuGet packages (for caching)
RUN dotnet restore --runtime linux-x64

# copy the source code
ADD ./src/ ./

# run the unit / integration tests
RUN dotnet test --runtime linux-x64 --configuration Release --no-restore

# make a release build
RUN dotnet publish --runtime linux-x64 --configuration Release \
                   --output /app/bin/ --no-restore

# use the official Microsoft .NET runtime image
FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app/bin
COPY --from=build-env /app/bin .

# launch the daemon test service on container startup
ENTRYPOINT ["dotnet", "Chord.Api.dll"]
