﻿using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Diagnostics.CodeAnalysis;

namespace DokkanDailyTests.Infra
{
    internal class MockBlobClient : BlobClient
    {
        public Dictionary<string, string> Tags { get; set; } = [];

        private string _name = "";
        public override string Name => _name;

        public MockBlobClient() { }

        public MockBlobClient(Dictionary<string, string> tags) { Tags = tags; }

        public void SetName(string name)
        {
            _name = name;
        }

        internal class MockResponse : Response
        {
            public override int Status => throw new NotImplementedException();

            public override string ReasonPhrase => throw new NotImplementedException();

            public override Stream ContentStream { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override string ClientRequestId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override void Dispose()
            {
                throw new NotImplementedException();
            }

            protected override bool ContainsHeader(string name)
            {
                throw new NotImplementedException();
            }

            protected override IEnumerable<HttpHeader> EnumerateHeaders()
            {
                throw new NotImplementedException();
            }

            protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string value)
            {
                throw new NotImplementedException();
            }

            protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string> values)
            {
                throw new NotImplementedException();
            }
        }

        public override Task<Response<BlobProperties>> GetPropertiesAsync(BlobRequestConditions conditions = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Response.FromValue<BlobProperties>(BlobsModelFactory.BlobProperties(metadata: Tags), new MockResponse()));
        }
    }
}
