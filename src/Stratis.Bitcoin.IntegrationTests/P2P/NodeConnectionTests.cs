﻿using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.P2P
{
    public sealed class NodeConnectionTests
    {
        [Fact]
        public async Task CanDiscoverAndConnectToPeersOnTheNetworkAsync()
        {
            var parameters = new NetworkPeerConnectionParameters();

            var testFolder = TestDirectory.Create("CanDiscoverAndConnectToPeersOnTheNetwork");

            var nodeSettings = new NodeSettings
            {
                DataDir = testFolder.FolderName
            };

            nodeSettings.DataFolder = new DataFolder(nodeSettings);

            var addressManager = new PeerAddressManager(nodeSettings.DataFolder);
            var addressManagerBehaviour = new PeerAddressManagerBehaviour(new DateTimeProvider(), addressManager)
            {
                PeersToDiscover = 3
            };

            parameters.TemplateBehaviors.Add(addressManagerBehaviour);

            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            INetworkPeerFactory networkPeerFactory = new NetworkPeerFactory(Network.Main, DateTimeProvider.Default, loggerFactory);
            var peerDiscoveryLoop = new PeerDiscoveryLoop(
                new AsyncLoopFactory(loggerFactory),
                Network.Main,
                parameters,
                new NodeLifetime(),
                addressManager,
                networkPeerFactory);

            peerDiscoveryLoop.DiscoverPeers();

            // Wait until we have discovered 3 peers.
            TestHelper.WaitLoop(() => addressManager.Peers.Count > 3);

            // Wait until at least one successful connection
            // has been made.
            while (true)
            {
                try
                {
                    var peerOne = addressManager.SelectPeerToConnectTo();
                    NetworkPeer peer = await networkPeerFactory.CreateConnectedNetworkPeerAsync(Network.Main, peerOne, parameters);
                    await peer.VersionHandshakeAsync();
                    peer.Disconnect();

                    break;
                }
                catch
                {
                }
            };
        }
    }
}