using System;
using System.Linq;
using System.Net;
using System.Numerics;
using Chord.Lib.Impl;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Chord.Lib.Test.IPv4EndpointTests;

public class IPv4EndpointGeneratorTest
{
    [Fact]
    public void Test_ShouldYieldAllValidHosts_WhenExploringIPv4TypeCNet()
    {
        const int keySpace = 100000;
        Func<BigInteger, ChordKey> newKey = (k) => new ChordKey(k, keySpace);
        var ipConfigMock = Substitute.For<IIpSettings>();
        ipConfigMock.IPv4NetworkId.Returns(IPAddress.Parse("192.168.178.0"));
        ipConfigMock.IPv4Broadcast.Returns(IPAddress.Parse("192.168.178.255"));

        var gen = new IPv4EndpointGenerator(ipConfigMock, newKey);
        var endpoints = gen.GenerateEndpoints().ToList();

        var expIPs = Enumerable.Range(1, 254).Select(i => $"192.168.178.{i}").ToHashSet();
        endpoints.Should().Match(x => x.All(x => expIPs.Contains(x.IpAddress)));
    }
}
