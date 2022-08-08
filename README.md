
# Chord.Lib

## About
This project is a little fun implementation of the Chord P2P concept as C# .NET library
providing services like e.g. distributed key-value stores with self-organization features
making it very durable to system downtime etc. Most importantly there is no single-point-of-failure
by design which might be a very desirable system feature for cloud services worth exploring.

## Disclaimer
There is still WIP, so be cautious when using this code as it might not work properly yet.

## Quickstart

```sh
sudo apt-get update && sudo apt-get install -y docker.io
sudo usermod docker $USER && reboot
```

```sh
git clone https://github.com/Bonifatius94/Chord.Lib
cd Chord.Lib
```

```sh
docker build . -t chord
# TODO: add docker swarm config to launch a bunch of chord nodes
```

## License
This project is available under the terms of the MIT license.
