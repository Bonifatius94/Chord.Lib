
# Chord.Lib

## About
This project is a little fun implementation of the Chord Peer-to-Peer concept
as C# .NET library.

Protocols like Chord are especially useful in cloud computing because of their
very desirable properties for distributed services such as self-organization,
scalability and fault-tolerance for managing large amounts of data with
a very high service uptime.

By design, there's no single-point-of-failure due to the Peer-to-Peer
nature of Chord because the nodes manage routing to each other's resources.
Services like Apache Cassandra take a very similar approach, so this
topic seems to be worth exploring.

## Disclaimer
There is still WIP, so be cautious when using this code as it might not
work properly yet.

## Quickstart

### Install Docker

```sh
sudo apt-get update && sudo apt-get install -y docker.io
sudo usermod docker $USER && reboot
```

### Clone the Code

```sh
git clone https://github.com/Bonifatius94/Chord.Lib
cd Chord.Lib
```

### Build an Example Chord Node as Docker Image

```sh
docker build . -t chord
# TODO: add docker swarm config to launch a bunch of chord nodes
```

## License
This project is available under the terms of the MIT license.
