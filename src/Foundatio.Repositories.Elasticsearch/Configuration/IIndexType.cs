﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Utility;
using Nest;
using System.Linq;
using System.Linq.Expressions;
using Foundatio.Parsers.ElasticQueries.Visitors;
using Foundatio.Parsers.LuceneQueries.Visitors;

namespace Foundatio.Repositories.Elasticsearch.Configuration {
    public interface IIndexType : IDisposable {
        string Name { get; }
        Type Type { get; }
        IIndex Index { get; }
        IElasticConfiguration Configuration { get; }
        int DefaultCacheExpirationSeconds { get; set; }
        int BulkBatchSize { get; set; }
        ISet<string> AllowedSearchFields { get; }
        ISet<string> AllowedAggregationFields { get; }
        ISet<string> AllowedSortFields { get; }
        Task ConfigureAsync();
        AliasesDescriptor ConfigureIndexAliases(AliasesDescriptor aliases);
        MappingsDescriptor ConfigureIndexMappings(MappingsDescriptor mappings);
        IElasticQueryBuilder QueryBuilder { get; }
        string GetFieldName(Field field);
        string GetPropertyName(PropertyName property);
    }

    public static class IndexTypeExtensions {
        public static bool CanSortByField(this IIndexType indexType, string field) {
            // allow all fields if an allowed list isn't specified
            if (indexType.AllowedSortFields == null || indexType.AllowedSortFields.Count == 0)
                return true;

            return indexType.AllowedSortFields.Contains(field, StringComparer.OrdinalIgnoreCase);
        }

        public static bool CanSearchByField(this IIndexType indexType, string field) {
            // allow all fields if an allowed list isn't specified
            if (indexType.AllowedSearchFields == null || indexType.AllowedSearchFields.Count == 0)
                return true;

            return indexType.AllowedSearchFields.Contains(field, StringComparer.OrdinalIgnoreCase);
        }

        public static bool CanAggregateByField(this IIndexType indexType, string field) {
            // allow all fields if an allowed list isn't specified
            if (indexType.AllowedAggregationFields == null || indexType.AllowedAggregationFields.Count == 0)
                return true;

            return indexType.AllowedAggregationFields.Contains(field, StringComparer.OrdinalIgnoreCase);
        }
    }

    public interface IIndexType<T>: IIndexType where T : class {
        /// <summary>
        /// Creates a new document id. If a date can be resolved, it will be taken into account when creating a new id.
        /// </summary>
        string CreateDocumentId(T document);
        string GetFieldName(Expression<Func<T, object>> objectPath);
        string GetPropertyName(Expression<Func<T, object>> objectPath);
    }

    public abstract class IndexTypeBase<T> : IIndexType<T> where T : class {
        protected static readonly bool HasIdentity = typeof(IIdentity).IsAssignableFrom(typeof(T));
        protected static readonly bool HasCreatedDate = typeof(IHaveCreatedDate).IsAssignableFrom(typeof(T));
        private readonly string _typeName = typeof(T).Name.ToLower();
        private readonly Lazy<IElasticQueryBuilder> _queryBuilder;
        private readonly Lazy<AliasMap> _aliasMap;

        public IndexTypeBase(IIndex index, string name = null) {
            if (index == null)
                throw new ArgumentNullException(nameof(index));

            Name = name ?? _typeName;
            Index = index;
            Type = typeof(T);
            _queryBuilder = new Lazy<IElasticQueryBuilder>(CreateQueryBuilder);
            _aliasMap = new Lazy<AliasMap>(GetAliasMap);
        }

        protected virtual IElasticQueryBuilder CreateQueryBuilder() {
            var builder = new ElasticQueryBuilder();

            Configuration.ConfigureGlobalQueryBuilders(builder);
            ConfigureQueryBuilder(builder);

            return builder;
        }

        protected virtual void ConfigureQueryBuilder(ElasticQueryBuilder builder) {}

        public string Name { get; }
        public Type Type { get; }
        public IIndex Index { get; }
        public IElasticConfiguration Configuration => Index.Configuration;
        public ISet<string> AllowedSearchFields { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public ISet<string> AllowedAggregationFields { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public ISet<string> AllowedSortFields { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public virtual string CreateDocumentId(T document) {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (HasIdentity) {
                string id = ((IIdentity)document).Id;
                if (!String.IsNullOrEmpty(id))
                    return id;
            }

            if (HasCreatedDate) {
                var date = ((IHaveCreatedDate)document).CreatedUtc;
                if (date != DateTime.MinValue)
                    return ObjectId.GenerateNewId(date).ToString();
            }

            return ObjectId.GenerateNewId().ToString();
        }

        public virtual Task ConfigureAsync() {
            return Task.CompletedTask;
        }

        public virtual AliasesDescriptor ConfigureIndexAliases(AliasesDescriptor aliases) {
            return aliases;
        }

        public virtual MappingsDescriptor ConfigureIndexMappings(MappingsDescriptor mappings) {
            return mappings.Map<T>(Name, BuildMapping);
        }

        public virtual TypeMappingDescriptor<T> BuildMapping(TypeMappingDescriptor<T> map) {
            return map.Properties(p => p.SetupDefaults());
        }

        public IElasticQueryBuilder QueryBuilder => _queryBuilder.Value;
        public AliasMap AliasMap => _aliasMap.Value;

        public virtual void Dispose() {}

        public int DefaultCacheExpirationSeconds { get; set; } = RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS;
        public int BulkBatchSize { get; set; } = 1000;

        private AliasMap GetAliasMap() {
            var visitor = new AliasMappingVisitor(Configuration.Client.Infer);
            var walker = new MappingWalker(visitor);
            var descriptor = BuildMapping(new TypeMappingDescriptor<T>());
            walker.Accept(descriptor);

            return visitor.RootAliasMap;
        }

        public string GetFieldName(Field field) {
            var result = AliasMap?.Resolve(field.Name);
            return Configuration.Client.Infer.Field(!String.IsNullOrEmpty(result?.Name) ? result.Name : field);
        }

        public string GetFieldName(Expression<Func<T, object>> objectPath) => Configuration.Client.Infer.Field(objectPath);
        public string GetPropertyName(PropertyName property) => Configuration.Client.Infer.PropertyName(property);
        public string GetPropertyName(Expression<Func<T, object>> objectPath) => Configuration.Client.Infer.PropertyName(objectPath);
    }

    public interface IHavePipelinedIndexType {
        string Pipeline { get; }
    }
}