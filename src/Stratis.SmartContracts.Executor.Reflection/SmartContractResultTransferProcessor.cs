﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class SmartContractResultTransferProcessor : ISmartContractResultTransferProcessor
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;

        public SmartContractResultTransferProcessor(ILoggerFactory loggerFactory, Network network)
        {
            this.loggerFactory = loggerFactory;
            this.network = network;
        }

        /// <inheritdoc />
        public Transaction Process(SmartContractCarrier carrier,
            IContractStateRepository stateSnapshot,
            ISmartContractTransactionContext transactionContext,
            IList<TransferInfo> internalTransfers,
            bool reversionRequired)
        {
            if (reversionRequired)
            {
                // Send back funds
                if (carrier.Value > 0)
                {
                    return CreateRefundTransaction(transactionContext);
                }
            }

            // If contract received no funds and made no transfers, do nothing.
            if (carrier.Value == 0 && !internalTransfers.Any())
            {
                return null;
            }

            // TODO we should not be generating addresses in here!
            uint160 contractAddress = null;
            if (carrier.CallData.ContractAddress == uint160.Zero)
                contractAddress = carrier.GetNewContractAddress();
            else
                contractAddress = carrier.CallData.ContractAddress;

            // If contract had no balance, received funds, but made no transfers, assign the current UTXO.
            if (stateSnapshot.GetUnspent(contractAddress) == null && carrier.Value > 0 && !internalTransfers.Any())
            {
                stateSnapshot.SetUnspent(contractAddress, new ContractUnspentOutput
                {
                    Value = carrier.Value,
                    Hash = carrier.TransactionHash,
                    Nvout = carrier.Nvout
                });

                return null;
            }
            // All other cases we need a condensing transaction

            var transactionCondenser = new TransactionCondenser(contractAddress, this.loggerFactory, internalTransfers, stateSnapshot, this.network, transactionContext);
            return transactionCondenser.CreateCondensingTransaction();
        }

        /// <summary>
        /// Should contract execution fail, we need to send the money, that was
        /// sent to contract, back to the contract's sender.
        /// </summary>
        private Transaction CreateRefundTransaction(ISmartContractTransactionContext transactionContext)
        {
            Transaction tx = this.network.Consensus.ConsensusFactory.CreateTransaction();
            // Input from contract call
            var outpoint = new OutPoint(transactionContext.TransactionHash, transactionContext.Nvout);
            tx.AddInput(new TxIn(outpoint, new Script(new[] { (byte)ScOpcodeType.OP_SPEND })));
            // Refund output
            Script script = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId(transactionContext.Sender));
            var txOut = new TxOut(new Money(transactionContext.TxOutValue), script);
            tx.Outputs.Add(txOut);
            return tx;
        }
    }
}
