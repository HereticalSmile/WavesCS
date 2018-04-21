﻿using System;
using System.Collections.Generic;
using System.Linq;
using org.whispersystems.curve25519;
using System.IO;
using System.Text;

namespace WavesCS
{

    public enum TransactionType : byte
    {
        Issue = 3,
        Transfer = 4,
        Reissue = 5,
        Burn = 6,
        Lease = 8,
        LeaseCancel = 9,
        Alias = 10,
        DataTx = 12,    
    }
    
    public class Transaction
    {
        private static readonly int MinBufferSize = 300;
        private static readonly Curve25519 Cipher = Curve25519.getInstance(Curve25519.BEST);

        private Transaction(params object[] items)
        {
            var map = new Dictionary<string, object>();
            for (int i = 0; i < items.Length; i += 2)
            {
                Object value = items[i + 1];
                if (value != null)
                {
                    map[(string)items[i]] = value;
                }
            }
            //this.data = map.ToLookup(kv => kv.Key, kv => kv.Value);
            Data = map.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        
        private Transaction(Dictionary<string, object> data)
        {
            Data = data;
        }        

        public Dictionary<String, Object> Data { get; }


        public static string Sign(PrivateKeyAccount account, MemoryStream stream)
        {
            var bytesToSign = stream.ToArray();
            var signature = Cipher.calculateSignature(account.PrivateKey, bytesToSign);
            return Base58.Encode(signature);
        }

        private static void PutAsset(Stream stream, string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
            {
                stream.WriteByte(0);
            }
            else
            {
                stream.WriteByte(1);
                var decoded = Base58.Decode(assetId);
                stream.Write(decoded, 0, decoded.Length);
            }
        }        

        public static Transaction MakeIssueTransaction(PrivateKeyAccount account,
                string name, string description, long quantity, int decimals, bool reissuable, long fee)
        {
            long timestamp = Utils.CurrentTimestamp();

            var stream = new MemoryStream(MinBufferSize);
            var writer = new BinaryWriter(stream);
            writer.Write(TransactionType.Issue);
            writer.Write(account.PublicKey);
            writer.WriteShort(name.Length);
            writer.Write(Encoding.ASCII.GetBytes(name));

            int descriptionLegth = description?.Length ?? 0;
            writer.WriteShort((short)descriptionLegth);
            if (descriptionLegth > 0)
            {
                writer.Write(Encoding.ASCII.GetBytes(description));
            }
            writer.WriteLong(quantity);
            writer.Write((byte)decimals);
            writer.Write((byte)(reissuable ? 1 : 0));
            writer.WriteLong(fee);
            writer.WriteLong(timestamp);        

            string signature = Sign(account, stream);
            return new Transaction(
                "type", TransactionType.Issue,
                "senderPublicKey", Base58.Encode(account.PublicKey),
                "signature", signature,
                "name", name,
                "description", description,
                "quantity", quantity,
                "decimals", decimals,
                "reissuable", reissuable,
                "fee", fee,
                "timestamp", timestamp);
        }

        public static Transaction MakeReissueTransaction(PrivateKeyAccount account, string assetId, long quantity, bool reissuable, long fee)
        {
            long timestamp = Utils.CurrentTimestamp();
            var stream = new MemoryStream(MinBufferSize);
            var writer = new BinaryWriter(stream);
            writer.Write(TransactionType.Reissue);
            writer.Write(account.PublicKey);
            writer.Write(Base58.Decode(assetId));
            writer.WriteLong(quantity);
            writer.Write((byte)(reissuable ? 1 : 0));
            writer.WriteLong(fee);
            writer.WriteLong(timestamp);

            string signature = Sign(account, stream);
            return new Transaction(
                "type", TransactionType.Reissue,
                "senderPublicKey", Base58.Encode(account.PublicKey),
                "signature", signature,
                "assetId", assetId,
                "quantity", quantity,
                "reissuable", reissuable,
                "fee", fee,
                "timestamp", timestamp);
        }

        public static Transaction MakeTransferTransaction(PrivateKeyAccount account, String toAddress,
           long amount, String assetId, long fee, String feeAssetId, String attachment)
        {

            byte[] attachmentBytes = Encoding.UTF8.GetBytes(attachment ?? "");
            long timestamp = Utils.CurrentTimestamp();

            var stream = new MemoryStream(MinBufferSize);
            var writer = new BinaryWriter(stream);
            writer.Write(TransactionType.Transfer);
            writer.Write(account.PublicKey);
            PutAsset(stream, assetId);
            PutAsset(stream, feeAssetId);
            writer.WriteLong(timestamp);
            writer.WriteLong(amount);
            writer.WriteLong(fee);
            writer.Write(Base58.Decode(toAddress));
            //writer.Write((short)attachmentBytes.Length);
            writer.WriteShort((short)attachmentBytes.Length);
            writer.Write(attachmentBytes);
            string signature = Sign(account, stream);
            return new Transaction(
                "type", TransactionType.Transfer,
                "senderPublicKey", Base58.Encode(account.PublicKey),
                "signature", signature,
                "recipient", toAddress,
                "amount", amount,
                "assetId", assetId,
                "fee", fee,
                "feeAssetId", feeAssetId,
                "timestamp", timestamp,
                "attachment", Base58.Encode(attachmentBytes));
        }

        public static Transaction MakeBurnTransaction(PrivateKeyAccount account, String assetId, long amount, long fee)
        {
            long timestamp = Utils.CurrentTimestamp();
            var stream = new MemoryStream(MinBufferSize);
            var writer = new BinaryWriter(stream);
            writer.Write(TransactionType.Burn);
            writer.Write(account.PublicKey);
            writer.Write(Base58.Decode(assetId));
            writer.WriteLong(amount);
            writer.WriteLong(fee);
            writer.WriteLong(timestamp);

            string signature = Sign(account, stream);
            return new Transaction(
                "type", TransactionType.Burn,
                "senderPublicKey", Base58.Encode(account.PublicKey),
                "signature", signature,
                "assetId", assetId,
                "quantity", amount,
                "fee", fee,
                "timestamp", timestamp);
        }

        public static Transaction MakeLeaseTransaction(PrivateKeyAccount account, String toAddress, long amount, long fee)
        {
            long timestamp = Utils.CurrentTimestamp();
            var stream = new MemoryStream(MinBufferSize);
            var writer = new BinaryWriter(stream);
            writer.Write(TransactionType.Lease);
            writer.Write(account.PublicKey);
            writer.Write(Base58.Decode(toAddress));

            writer.WriteLong(amount);
            writer.WriteLong(fee);
            writer.WriteLong(timestamp);

            string signature = Sign(account, stream);
            return new Transaction(
                "type", TransactionType.Lease,
                "senderPublicKey", Base58.Encode(account.PublicKey),
                "signature", signature,
                "recipient", toAddress,
                "amount", amount,
                "fee", fee,
                "timestamp", timestamp);
        }

        public static Transaction MakeLeaseCancelTransaction(PrivateKeyAccount account, String TransactionId, long fee)
        {
            long timestamp = Utils.CurrentTimestamp();
            var stream = new MemoryStream(MinBufferSize);
            var writer = new BinaryWriter(stream);
            writer.Write(TransactionType.LeaseCancel);
            writer.Write(account.PublicKey);
            writer.WriteLong(fee);
            writer.WriteLong(timestamp);
            writer.Write(Base58.Decode(TransactionId));
            string signature = Sign(account, stream);
            return new Transaction(
                "type", TransactionType.LeaseCancel,
                "senderPublicKey", Base58.Encode(account.PublicKey),
                "signature", signature,
                "TransactionId", TransactionId,
                "fee", fee,
                "timestamp", timestamp);
        }

        public static Transaction MakeAliasTransaction(PrivateKeyAccount account, String alias, char scheme, long fee)
        {
            long timestamp = Utils.CurrentTimestamp();
            var stream = new MemoryStream(MinBufferSize);
            var writer = new BinaryWriter(stream);            
            writer.Write(TransactionType.Alias);
            writer.Write(account.PublicKey);
            writer.WriteShort((short)(alias.Length + 4));
            writer.Write(0x02);
            writer.Write((byte)scheme);
            writer.WriteShort((short)alias.Length);
            writer.Write(Encoding.ASCII.GetBytes(alias));

            writer.WriteLong(fee);
            writer.WriteLong(timestamp);            

            string signature = Sign(account, stream);
            return new Transaction(
                "type", TransactionType.Alias,
                "senderPublicKey", Base58.Encode(account.PublicKey),
                "signature", signature,
                "alias", alias,
                "fee", fee,
                "timestamp", timestamp);
        }

        public static Transaction MakeDataTransaction(PrivateKeyAccount account, Dictionary<string, object> entries,
            long fee)
        {
            long timestamp = Utils.CurrentTimestamp();

            var stream = new MemoryStream(MinBufferSize);
            var writer = new BinaryWriter(stream);

            const byte INTEGER = 0;
            const byte BOOLEAN = 1;
            const byte BINARY = 2;
            const byte version = 1;

            writer.Write(TransactionType.DataTx);
            writer.Write(version);
            writer.Write(account.PublicKey);
            writer.WriteShort((short) entries.Count);
            foreach (var pair in entries)
            {
                var key = Encoding.UTF8.GetBytes(pair.Key);
                writer.WriteShort((short) key.Length);
                writer.Write(key);
                switch (pair.Value)
                {
                    case long value:                        
                        writer.Write(INTEGER);
                        writer.WriteLong(value);
                        break;
                    case bool value:
                        writer.Write(BOOLEAN);
                        writer.Write(value ? (byte) 1 : (byte) 0);
                        break;
                    case byte[] value:
                        writer.Write(BINARY);
                        writer.WriteShort((short) value.Length);
                        writer.Write(value);
                        break;
                    default:
                        throw new ArgumentException("Only long, bool and byte[] entry values supported",
                            nameof(entries));
                }
            }

            writer.WriteLong(timestamp);
            writer.WriteLong(fee);
            string signature = Sign(account, stream);
            return new Transaction(new Dictionary<string, object>
            {
                {"type", TransactionType.DataTx},
                {"version", version},
                {"senderPublicKey", Base58.Encode(account.PublicKey)},
                {"data", entries.Select(pair => new Dictionary<string, object>
                {
                    {"key", pair.Key},
                    {"type", pair.Value is long ? "integer" : (pair.Value is bool ? "boolean" : "binary")},
                    {"value", pair.Value is byte[] bytes ? Base58.Encode(bytes) : pair.Value }                    
                })},
                {"fee", fee},
                {"timestamp", timestamp},
                {"proofs", new []{ signature }}
            });
        }

        public static Transaction MakeOrder(PrivateKeyAccount sender, string matcherKey, Order.OrderType orderType,
           string amountAssetId, string priceAssetId, long price, long amount, long expiration, long matcherFee)
        {
            long timestamp = Utils.CurrentTimestamp();

            var stream = new MemoryStream(MinBufferSize);
            var writer = new BinaryWriter(stream);
            writer.Write(sender.PublicKey);
            writer.Write(Base58.Decode(matcherKey));
            PutAsset(stream, amountAssetId);
            PutAsset(stream, priceAssetId);
            writer.Write((byte)orderType.Ordinal); 
            writer.WriteLong(price);
            writer.WriteLong(amount);
            writer.WriteLong(timestamp);
            writer.WriteLong(expiration);
            writer.WriteLong(matcherFee);
            string signature = Sign(sender, stream);

            return new Transaction(
                    "senderPublicKey", Base58.Encode(sender.PublicKey),
                    "matcherPublicKey", matcherKey,
                    "assetPair", new AssetPair(amountAssetId, priceAssetId).GetDictionary(),
                    "orderType", orderType.Json,
                    "price", price,
                    "amount", amount,
                    "timestamp", timestamp,
                    "expiration", expiration,
                    "matcherFee", matcherFee,
                    "signature", signature);
        }

        public class AssetPair
        {
            public AssetPair()
            {
            }
            
            public AssetPair(string amountAsset, string priceAsset)
            {
                AmountAsset = amountAsset;
                PriceAsset = priceAsset;
            }
            public string AmountAsset { get; }
            public string PriceAsset { get; }
            
            public Dictionary<string, string> GetDictionary()
            {
                Dictionary<String, String> assetPair = new Dictionary<String, String>
                {
                    ["amountAsset"] = AmountAsset,
                    ["priceAsset"] = PriceAsset
                };
                return assetPair;
            }
        }

        public static Transaction MakeOrderCancelTransaction(PrivateKeyAccount sender,
                string amountAssetId, string priceAssetId, string orderId, long fee)
        {
            var stream = new MemoryStream(MinBufferSize);
            var writer = new BinaryWriter(stream);
            writer.Write(sender.PublicKey);
            writer.Write(Base58.Decode(orderId));
            string signature = Sign(sender, stream);            
            return new Transaction(
                    "sender", Base58.Encode(sender.PublicKey), "orderId", orderId,
                    "signature", signature);
        }

        public class JsonTransaction
        {         
            public int Type { get; set; }
            public string Id { get; set; }
            public string Sender { get; set; }
            public string SenderPublicKey { get; set; }
            public long Fee { get; set; }
            public long Timestamp { get; set; }
            public string Signature { get; set; }
            public string Recipient { get; set; }
            public object AssetId { get; set; }
            public long Amount { get; set; }
            public object FeeAsset { get; set; }
            public string Attachment { get; set; }
            public AssetPair AssetPair { get; set; }
        }

        public class JsonTransactionError
        {
            public int Error { get; set; }
            public string Message { get; set; }
            public JsonTransaction Tx { get; set; }
        }

        public class JsonTransactionWithStatus
        {
            public string Status { get; set; }
            public JsonTransaction Message { get; set; }
        }
    }
}