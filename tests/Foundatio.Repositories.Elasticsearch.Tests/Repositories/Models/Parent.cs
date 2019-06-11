﻿using System;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Utility;
using Nest;

namespace Foundatio.Repositories.Elasticsearch.Tests.Repositories.Models {
    public interface IParentChildDocument : IIdentity {
        string ParentId { get; set; }
        JoinField Discriminator { get; set; }
    }
    
    public class Parent : IParentChildDocument, IHaveDates, ISupportSoftDeletes {
        public string Id { get; set; }
        string IParentChildDocument.ParentId { get; set; }
        JoinField IParentChildDocument.Discriminator { get; set; }
        public string ParentProperty { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public bool IsDeleted { get; set; }
    }
    
    public static class ParentGenerator {
        public static readonly string DefaultId = ObjectId.GenerateNewId().ToString();

        public static Parent Default => new Parent { Id = DefaultId };

        public static Parent Generate(string id = null) {
            return new Parent { Id = id };
        }
    }
}
