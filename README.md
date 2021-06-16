
# Chord.Lib

## About
This project is a little fun implementation of the Chord P2P concept as C# .NET library
providing services like e.g. distributed key-value stores with self-organization features
making it very durable to system downtime etc. Most importantly there is no single-point-of-failure
by design which might be a very desirable system feature for cloud services worth exploring.

## Chord Fundamentals
The Chord system organizes a distributed service as a peer-to-peer network consisting of
multiple independently acting service instances, called nodes. Those nodes are created
equal and serve both payload and infrastructure tasks. All nodes are organized using a virtual
token-ring topology as the overlay network. The term overlay network means that the virtual
relations between nodes do not necessarily need to reflect the real-world network topology
bound to machines, wires and other hardware devices.

### Chord Nodes and Data Items
Now that it's clear how the Chord cluster looks like, let's have a look at the way the nodes 
organize the data to be stored. It's actually quite simple: Each node in the token-ring
and each data item is assigned a unique id that can be referred to as a lookup key.
By convention, each data item is managed by the node with the next higher id than the 
data item's id, so the data items get uniformly distributed over the token-ring's id range.
And not to mention the nodes obviously should know each of their neighbour's id like it
naturally should be in a token-ring.

### Key Lookup Query
When querying a data item, any node can serve as an entrypoint into the Chord cluster
such that it forwards the request clockwise until the first node with a higher id than
the queried data item's id is reached. The node responsible for the data item is found.

As a lookup in such a simple token-ring topology would require O(n) time for n nodes, there
has to be some kind of key lookup enhancement. Therefore each node additionally manages
a collection of O(log(n)) nodes with exponentially growing distances up to the node roughly
situated at the opposing side of the token-ring; those nodes are often referred to as finger
pointers or simply fingers being stored in a finger table.

Now, when forwarding to the maximum neighbour or finger
whose id is still smaller than the searched id, the distance is always roughly
halved resulting in a performance complexity of O(log(n)). Note that it's very important
not to forward requests to nodes with an id higher than the searched one as this may cause
infinite loops (the only exception is when an id is looked up that is smaller than the
node's id resulting into an id range overflow).

## The Core Functionality
The Chord Library exposes following elementary functions:

### 1) Lookup Key
As already described, the cluster can perform key lookups starting from any node being
forwarded to the node that's actually responsible for the looked up key. This can be
seen as some kind of routing protocol to find resources in the Chord token-ring.

### 2) Join Network
Make a node join the Chord cluster. This can be achieved by generating a random (unique!) node id
and performing a key lookup to find the new successor node. Then, request the new successor's
current predecessor which becomes the predecessor of the node to be joined. Now, the
nodes can perform a join sequence like inserting an item into a bidirectional linked list.
Next, exchange the finger table with the new neighbours and finalize the join procedure.

### 3) Leave Network
Allow a node to leave the Chord cluster gracefully. This can be achieved by copying the node's
data to the successor node that will be responsible for the data. Then, tell the predecessor
that it has a new successor (similar to removing an item from a linked list). Shut the node
down gracefully after finalizing the whole process.

### 4) Check Node Health
Monitor the health status of all nodes connected (neighbours and fingers) on a regular
time schedule. Initiate repair operations for nodes having a downtime (e.g. bridge the node
and recover the data from successor nodes that oftentimes share data with their direct neighbours).
Moreover, also re-create the finger tables on a regular basis as they might change over time.

### 5) Serve Payload Functionality
The actual functionality of the service should be organized such that each node can serve
any request. In case the node cannot access the data required to perform the task itself, it may
forward to the responsible node. This can be either achieved by telling the requester
which node to call instead or actually handing the results through (piggyback). Both policies
may be supported by the Chord.Lib and should rely onto the payload service design.

## Components and Deployment

### Chord.Lib Components
The Chord.Lib package can be used by web services to establish a Chord peer-to-peer (P2P) network.
Therefore it exposes all of Chord's core functionality asynchronously, facilitating 
high-performance parallel operations. In particular, a Chord.Lib node needs to be given a
callback enabling it to exchange messages with other nodes like key lookups or health checks, etc.
Each of those node instances may also be linked to an ASP.NET endpoint controller class providing
exactly that message submission. This approach should help integrating the Chord endpoint making it
obsolete to re-develop the ASP.NET endpoint controller again and again, but also allowing to
implement the endpoint individually when needed targeting maximum flexibility and convenience.

### Dockerized Chord
Those Chord functions are not only provided as a .NET package but also as an entire dockerized
container exposing the ASP.NET endpoint. This container can then be paired with another
container serving the actual payload functionality such that each pair of those two containers
can be seen as something like a Kubernetes pod gluing the containers together. Requesters can
now enter the Chord cluster from any node and get forwarded to the node serving the payload.
This should allow to attach the Chord protocol to basically any existing single-node service.

### Example Deployment
For demonstration purposes there is a very simple key-value store service using the Chord library.
Those dockerized service nodes can be deployed e.g. as a Kubernetes load-balanced service
like already described in the last section.

## Disclaimer
There is still WIP, so be cautious when using this code as it might not work properly yet.

## License
This project is available under the terms of the MIT license.
