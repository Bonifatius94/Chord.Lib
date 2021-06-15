
# Chord.Lib

## About
This project is a little fun implementation of the Chord P2P concept as C# .NET Core library
providing services like e.g. distributed key-value stores with self-organization features
making it more durable to system downtime etc. Most importantly there is no single-point-of-failure
by design which might be a very desirable system feature for cloud services worth exploring.

## The Chord Concept
The Chord system organizes a distributed service as a peer-to-peer network consisting of
multiple independently acting service instances, called nodes. Those nodes are created
equal and serve both payload and infrastructure tasks. All nodes are organized as a virtual
token-ring topology which is commonly referred to as an overlay network. The term overlay
network means that the virtual relations between nodes don't necessarily need not reflect
the real-world network topology bound to machines, wires and other hardware devices.

Now that it's clear how the Chord cluster looks like, let's have a look at the way the nodes 
organize the data to be stored. It's actually quite simple: Each node in the token-ring
and each data item is assigned a unique id that can be referred to as a lookup key.
By convention, each data item is managed by the node with the next higher id than the 
data item's id, so the data items get uniformly distributed over the token-ring's id range.
And not to mention the nodes obviously should know each of their neighbour's id like it
naturally should be in a token-ring.

When querying a data item, any node can serve as an entrypoint into the Chord cluster
such that it forwards the request clockwise until the first node with a higher id than
the queried data item's id is reached. The node responsible for the data item is found.

As a lookup in such a simple token-ring topology would require O(n) time for n nodes, there
has to be some kind of key lookup enhancement. Therefore each node additionally manages
a collection of O(log(n)) nodes with exponentially growing distances up to the node roughly
situated at the opposing side of the token-ring; those nodes are often referred to as finger
pointers or simply fingers being stored in a finger table. Now, when forwarding to the maximum
connected node whose id is still lower than the searched id, the distance is always roughly
halved resulting in a performance complexity of O(log(n)). Note that it's very important
not to forward requests to nodes with an id higher than the searched one as this may cause
infinite loops (the only exception is when an id is looked up that is smaller than the
node's id resulting into an id overflow).

## Chord Library Functions
The Chord Library exposes following elementary functions:

### 1) Key Lookup
As already described, the cluster can perform key lookups starting from any node being
forwarded to the node that's actually responsible for the looked up key.

### 2) Join Network
Make a node join the P2P cluster. This can be achieved by generating a random node id
and performing a key lookup to find the new successor node. Then, request the new successor's
current predecessor which becomes the predecessor of the node to be joined. Now, the
nodes can perform a join sequence like inserting items into a linked list.
Next, exchange the finger table with the new neighbours and finalize the join procedure.

### 3) Leave Network
Allow a node to leave the P2P cluster gracefully. This can be achieved by copying the node's
data to the successor node that will be responsible for the data. Then, tell the predecessor
that it has a new successor (similar to removing an item from a linked list). Shut the node
down gracefully after finalizing the whole process.

### 4) Check Node Health
Monitor the health status of all nodes connected by the finger table on a regular
time schedule. Initiate repair operations for nodes having a downtime (e.g. bridge the node
and recover the data from successor nodes that oftentimes share data with their direct neighbours).

## Chord Endpoint
The Chord library can be used by web services to establish a Chord peer-to-peer (P2P) network.
It exposes all Chord tasks asynchronously facilityting high-performance parallel operations.

For testing purposes there is also a very simple key-value store service using the Chord library.
Those dockerized service nodes can be deployed as a kubernetes load-balanced service.

## Disclaimer
There is still WIP, so be cautious when using this code as it might not work properly yet.

## License
This project is available under the terms of the MIT license.
