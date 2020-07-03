# ChordTest
Little fun implementation of the Chord protocol as C# .NET Core library with a point-blank websocket handle. 
The Chord library can be used by web services to establish a chord P2P network.
It offers a node handle class exposing all Chord tasks asynchronously.

For testing purposes there is also a test daemon executable that uses the Chord library. This executable
is attached to a docker image that can then be deployed as a kubernetes load-balanced service.

# About
This is just a test project for trying out the Chord protocol. 

# Disclaimer
There is still WIP, so be cautious when using the code.

# Roadmap
1. finish implementing the Chord protocol functionality
2. create a Dockerfile for hosting some chord daemon nodes
3. deploy at larger load of Chord nodes and start testing performance, etc.
4. think of a more useful task that involves storing key-value pairs
5. making the Chord lib easy-to-use for potential production purposes
